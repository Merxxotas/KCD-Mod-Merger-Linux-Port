using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using KCDMerge.Core.Configuration;
using KCDMerge.Core.Services;
using Serilog;

namespace KCDMerge.Core.Data;

public class XDocumentMerger : IXDocumentMerger
{
	private record AttributeConflict(string Path, string AttrName, string PreviousMod, string PreviousValue, string CurrentMod, string CurrentValue, string VanillaValue, XElement MergedElement);

	private readonly IXPathIndexer _indexer;

	private readonly IConflictResolver? _conflictResolver;

	private readonly IModConflictRulesService? _rulesService;

	private readonly RowSetComparer _rowSetComparer;

	private Dictionary<string, (string ModName, string NewValue, string VanillaValue)>? _attributeChanges;

	public XDocumentMerger(IXPathIndexer indexer, IConflictResolver? conflictResolver = null, IModConflictRulesService? rulesService = null)
	{
		_indexer = indexer ?? throw new ArgumentNullException("indexer");
		_conflictResolver = conflictResolver;
		_rulesService = rulesService;
		_rowSetComparer = new RowSetComparer();
	}

	public XDocument Merge(XDocument baseDoc, XDocument modDoc, string fileName, string modName, int priority, MergeReport report, bool isPatchFile = false, XDocument? vanillaDoc = null)
	{
		if (baseDoc == null)
		{
			throw new ArgumentNullException("baseDoc");
		}
		if (modDoc == null)
		{
			throw new ArgumentNullException("modDoc");
		}
		if (report == null)
		{
			throw new ArgumentNullException("report");
		}
		XDocument xDocument = new XDocument(baseDoc);
		try
		{
			Log.Information("[MERGE] {FileName}: Starting merge from {ModName}", fileName, modName);
			Dictionary<string, XElement> dictionary = _indexer.BuildIndex(baseDoc, fileName + " (base)");
			Dictionary<string, XElement> dictionary2 = _indexer.BuildIndex(modDoc, fileName + " (" + modName + ")");
			Log.Debug("[MERGE] {FileName}: Base has {BaseCount} nodes, Mod has {ModCount} nodes", fileName, dictionary.Count, dictionary2.Count);
			if (_indexer.IsIdOnlyTable(fileName))
			{
				Log.Information("[MERGE] {FileName}: Detected ID-only table, using set-based merge", fileName);
				return MergeIdOnlyTable(baseDoc, modDoc, fileName, modName, priority, report, isPatchFile).GetAwaiter().GetResult();
			}
			Dictionary<string, XElement> dictionary3 = _indexer.BuildIndex(xDocument, fileName + " (merged)");
			Dictionary<string, XElement> dictionary4 = null;
			List<AttributeConflict> list = new List<AttributeConflict>();
			if (vanillaDoc != null && _conflictResolver != null)
			{
				if (_attributeChanges == null)
				{
					_attributeChanges = new Dictionary<string, (string, string, string)>();
				}
				dictionary4 = _indexer.BuildIndex(vanillaDoc, fileName + " (vanilla)");
			}
			int num = 0;
			int modifiedCount = 0;
			int num2 = 0;
			int count = report.Warnings.Count;
			HashSet<string> source = DetectAndReplaceSubtrees(dictionary, dictionary2, dictionary3, xDocument, fileName, modName, report, ref modifiedCount);
			foreach (string modPath in dictionary2.Keys.OrderBy((string p) => p.Length))
			{
				if (source.Any((string replaced) => modPath.StartsWith(replaced + "/") || modPath == replaced))
				{
					continue;
				}
				XElement xElement = dictionary2[modPath];
				if (dictionary.ContainsKey(modPath))
				{
					XElement xElement2 = dictionary[modPath];
					XElement value;
					if (ElementsAreEquivalent(xElement2, xElement))
					{
						num2++;
					}
					else if (dictionary3.TryGetValue(modPath, out value))
					{
						XElement value2 = null;
						dictionary4?.TryGetValue(modPath, out value2);
						if (MergeAttributes(value, xElement, modPath, fileName, modName, report, value2, list))
						{
							modifiedCount++;
							report.AddMergeDetail(fileName, modPath, modName, "Modified attributes");
							Log.Debug("[MERGE] {FileName}: Modified {Path} from {ModName}", fileName, modPath, modName);
						}
						if (!xElement.HasElements && xElement2.HasElements)
						{
							int num3 = value.Elements().Count();
							if (num3 > 0)
							{
								value.RemoveNodes();
								modifiedCount++;
								report.AddMergeDetail(fileName, modPath, modName, $"Cleared {num3} child element(s) (mod has empty parent)");
								Log.Information("[MERGE] {FileName}: Cleared {Count} children from {Path} (mod has empty parent)", fileName, num3, modPath);
							}
						}
						if (!value.HasElements && !xElement.HasElements && !string.IsNullOrWhiteSpace(xElement.Value) && xElement.Value != value.Value)
						{
							string value3 = value.Value;
							value.Value = xElement.Value;
							report.AddMergeDetail(fileName, modPath, modName, $"Changed text content from '{value3}' to '{xElement.Value}'");
							Log.Debug("[MERGE] {FileName}: Changed text content at {Path}", fileName, modPath);
						}
					}
					else
					{
						Log.Warning("[MERGE] {FileName}: Could not find merged element for path {Path}", fileName, modPath);
						report.AddWarning($"MERGE: {fileName}:{modPath} - Could not locate element in merged document");
					}
				}
				else if (AddNewElement(xDocument, dictionary3, modPath, xElement, fileName, modName, report))
				{
					num++;
					report.AddMergeDetail(fileName, modPath, modName, "Added new element");
					Log.Verbose("[ADDED] {FileName}: {Path} from {ModName}", fileName, modPath, modName);
					if (num % 50 == 0)
					{
						Console.Write(".");
					}
				}
			}
			if (list.Count > 0 && _conflictResolver != null)
			{
				ResolveAttributeConflicts(list, dictionary3, fileName, modName, report);
			}
			DetectDeletions(dictionary, dictionary2, fileName, modName, report, isPatchFile);
			if (num > 0 || modifiedCount > 0)
			{
				if (num > 0)
				{
					Console.WriteLine();
				}
				Console.WriteLine($"[MERGE] {fileName}: {modName} - Added: {num}, Modified: {modifiedCount}, Unchanged: {num2}");
				Log.Information("[MERGE] {FileName}: {ModName} - Added: {Added}, Modified: {Modified}, Unchanged: {Unchanged}", fileName, modName, num, modifiedCount, num2);
			}
			else
			{
				Log.Debug("[MERGE] {FileName}: {ModName} - No changes detected", fileName, modName);
			}
			int deletions = ((!isPatchFile) ? dictionary.Keys.Except(dictionary2.Keys).Count() : 0);
			int warnings = report.Warnings.Count - count;
			bool isIdOnlyTable = _indexer.IsIdOnlyTable(fileName);
			report.RecordFileStats(fileName, modName, priority, isPatchFile, isIdOnlyTable, isStandardTable: false, num, deletions, modifiedCount, warnings, 0);
			return xDocument;
		}
		catch (Exception ex)
		{
			string error = $"Failed to merge {fileName} from {modName}: {ex.Message}";
			Log.Error(ex, "[MERGE ERROR] {FileName}: {ModName}", fileName, modName);
			report.AddError(error);
			return new XDocument(baseDoc);
		}
	}

	private bool ElementsAreEquivalent(XElement baseElement, XElement modElement)
	{
		if (baseElement.ToString(SaveOptions.DisableFormatting) == modElement.ToString(SaveOptions.DisableFormatting))
		{
			return true;
		}
		List<XAttribute> list = (from a in baseElement.Attributes()
			orderby a.Name.LocalName
			select a).ToList();
		List<XAttribute> list2 = (from a in modElement.Attributes()
			orderby a.Name.LocalName
			select a).ToList();
		if (list.Count != list2.Count)
		{
			return false;
		}
		bool flag = list.All((XAttribute a) => a.Name.LocalName.EndsWith("_id", StringComparison.OrdinalIgnoreCase) || a.Name.LocalName.EndsWith("_key", StringComparison.OrdinalIgnoreCase));
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].Name != list2[i].Name)
			{
				return false;
			}
			if (flag)
			{
				if (!string.Equals(list[i].Value, list2[i].Value, StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
			}
			else if (list[i].Value != list2[i].Value)
			{
				return false;
			}
		}
		if (baseElement.Value != modElement.Value)
		{
			return false;
		}
		return true;
	}

	private bool MergeAttributes(XElement target, XElement source, string path, string fileName, string modName, MergeReport report, XElement? vanillaElement = null, List<AttributeConflict>? conflicts = null)
	{
		bool result = false;
		foreach (XAttribute item2 in source.Attributes())
		{
			XAttribute xAttribute = target.Attribute(item2.Name);
			string localName = item2.Name.LocalName;
			if (xAttribute == null)
			{
				target.Add(new XAttribute(item2.Name, item2.Value));
				result = true;
				Log.Debug("[MERGE] {FileName}: Added attribute {AttrName}='{AttrValue}' to {Path}", fileName, localName, item2.Value, path);
				if (_attributeChanges != null && vanillaElement != null)
				{
					string item = vanillaElement.Attribute(item2.Name)?.Value ?? "";
					string key = path + "|" + localName;
					_attributeChanges[key] = (modName, item2.Value, item);
				}
			}
			else
			{
				if (!(xAttribute.Value != item2.Value))
				{
					continue;
				}
				string value = xAttribute.Value;
				if (_attributeChanges != null && vanillaElement != null && conflicts != null)
				{
					string key2 = path + "|" + localName;
					string text = vanillaElement.Attribute(item2.Name)?.Value ?? "";
					if (_attributeChanges.TryGetValue(key2, out (string, string, string) value2) && value2.Item2 != item2.Value && value2.Item3 == text)
					{
						conflicts.Add(new AttributeConflict(path, localName, value2.Item1, value2.Item2, modName, item2.Value, text, target));
					}
					_attributeChanges[key2] = (modName, item2.Value, text);
				}
				xAttribute.Value = item2.Value;
				result = true;
				report.AddMergeDetail(fileName, path, modName, $"Changed attribute '{localName}' from '{value}' to '{item2.Value}'");
				Log.Debug("[MERGE] {FileName}: Changed attribute {AttrName} from '{OldValue}' to '{NewValue}' at {Path}", fileName, localName, value, item2.Value, path);
			}
		}
		return result;
	}

	private void ResolveAttributeConflicts(List<AttributeConflict> conflicts, Dictionary<string, XElement> mergedIndex, string fileName, string currentModName, MergeReport report)
	{
		foreach (IGrouping<string, AttributeConflict> item in (from c in conflicts
			group c by c.PreviousMod).ToList())
		{
			string key = item.Key;
			List<AttributeConflict> list = item.ToList();
			int count = list.Count;
			string conflictSummary = $"{count} attribute conflict(s) in {fileName}";
			Log.Information("[CONFLICT] {FileName}: {PrevMod} vs {CurrMod} - {Count} conflicting attribute(s)", fileName, key, currentModName, count);
			string text = _rulesService?.GetXmlConflictWinner(key, currentModName);
			string text2;
			if (text != null)
			{
				text2 = text;
				Log.Information("[CONFLICT] Using saved decision: {Winner} wins over {Loser}", text2, (text2 == key) ? currentModName : key);
			}
			else if (_conflictResolver != null)
			{
				text2 = _conflictResolver.ResolveModConflictAsync(key, currentModName, conflictSummary).GetAwaiter().GetResult();
				_rulesService?.SetXmlConflictWinner(text2, (text2 == key) ? currentModName : key);
			}
			else
			{
				text2 = key;
			}
			if (text2 == key)
			{
				int num = 0;
				foreach (AttributeConflict item2 in list)
				{
					XAttribute xAttribute = item2.MergedElement.Attribute(item2.AttrName);
					if (xAttribute != null)
					{
						xAttribute.Value = item2.PreviousValue;
						num++;
						Log.Debug("[CONFLICT RESOLVED] {FileName}: Reverted {Path}|{AttrName} to '{Value}' ({Winner} wins)", fileName, item2.Path, item2.AttrName, item2.PreviousValue, key);
						if (_attributeChanges != null)
						{
							string key2 = item2.Path + "|" + item2.AttrName;
							_attributeChanges[key2] = (key, item2.PreviousValue, item2.VanillaValue);
						}
					}
				}
				report.AddMergeDetail(fileName, "/", key, $"Won conflict vs {currentModName}: reverted {num} attribute(s)");
				Log.Information("[CONFLICT RESOLVED] {FileName}: {Winner} wins - reverted {Count} attribute(s) from {Loser}", fileName, key, num, currentModName);
			}
			else
			{
				report.AddMergeDetail(fileName, "/", currentModName, $"Won conflict vs {key}: kept {count} attribute change(s)");
				Log.Information("[CONFLICT RESOLVED] {FileName}: {Winner} wins - kept {Count} attribute change(s) over {Loser}", fileName, currentModName, count, key);
			}
		}
	}

	public void ResetAttributeTracking()
	{
		_attributeChanges?.Clear();
		_attributeChanges = null;
	}

	private HashSet<string> DetectAndReplaceSubtrees(Dictionary<string, XElement> baseIndex, Dictionary<string, XElement> modIndex, Dictionary<string, XElement> mergedIndex, XDocument mergedDoc, string fileName, string modName, MergeReport report, ref int modifiedCount)
	{
		HashSet<string> hashSet = new HashSet<string>();
		Regex regex = new Regex("^(.+)(/.+)\\[(\\d+)\\]$");
		Dictionary<string, List<string>> dictionary = new Dictionary<string, List<string>>();
		foreach (string key5 in baseIndex.Keys)
		{
			Match match = regex.Match(key5);
			if (match.Success)
			{
				string key = match.Groups[1].Value + match.Groups[2].Value;
				if (!dictionary.ContainsKey(key))
				{
					dictionary[key] = new List<string>();
				}
				dictionary[key].Add(key5);
			}
		}
		Dictionary<string, List<string>> dictionary2 = new Dictionary<string, List<string>>();
		foreach (string key6 in modIndex.Keys)
		{
			Match match2 = regex.Match(key6);
			if (match2.Success)
			{
				string key2 = match2.Groups[1].Value + match2.Groups[2].Value;
				if (!dictionary2.ContainsKey(key2))
				{
					dictionary2[key2] = new List<string>();
				}
				dictionary2[key2].Add(key6);
			}
		}
		foreach (KeyValuePair<string, List<string>> item in dictionary)
		{
			string key3 = item.Key;
			List<string> value = item.Value;
			if (!dictionary2.TryGetValue(key3, out var value2) || value.Count == value2.Count)
			{
				continue;
			}
			Match match3 = regex.Match(value[0]);
			if (!match3.Success)
			{
				continue;
			}
			string value3 = match3.Groups[1].Value;
			string text = match3.Groups[2].Value.TrimStart('/');
			if (!mergedIndex.TryGetValue(value3, out XElement value4))
			{
				Log.Warning("[SUBTREE] {FileName}: Parent {ParentPath} not found in merged document", fileName, value3);
				continue;
			}
			if (!modIndex.TryGetValue(value3, out XElement value5))
			{
				Log.Warning("[SUBTREE] {FileName}: Parent {ParentPath} not found in mod document", fileName, value3);
				continue;
			}
			string text2 = text;
			if (value2.Count > value.Count)
			{
				List<XElement> source = value5.Elements(text2).ToList();
				int num = value2.Count - value.Count;
				List<XElement> list = source.Skip(value.Count).ToList();
				Log.Information("[SUBTREE] {FileName}: Positional group '{Segment}' under {ParentPath} has count increase (base: {BaseCount}, mod: {ModCount}). Appending {SurplusCount} new element(s) from {ModName}.", fileName, text, value3, value.Count, value2.Count, num, modName);
				foreach (XElement item2 in list)
				{
					XElement xElement = new XElement(item2);
					value4.Add(xElement);
					int value6 = value.Count + list.IndexOf(item2);
					string key4 = $"{value3}/{text2}[{value6}]";
					mergedIndex[key4] = xElement;
				}
				modifiedCount++;
				report.AddMergeDetail(fileName, value3, modName, $"Positional append: kept {value.Count} existing '{text}' elements, appended {num} new from mod");
				foreach (string item3 in value2.Skip(value.Count))
				{
					hashSet.Add(item3);
				}
				continue;
			}
			Log.Information("[SUBTREE] {FileName}: Positional group '{Segment}' under {ParentPath} has count decrease (base: {BaseCount}, mod: {ModCount}). Using subtree replacement from {ModName}.", fileName, text, value3, value.Count, value2.Count, modName);
			foreach (XElement item4 in value4.Elements(text2).ToList())
			{
				item4.Remove();
			}
			foreach (XElement item5 in value5.Elements(text2))
			{
				value4.Add(new XElement(item5));
			}
			modifiedCount++;
			report.AddMergeDetail(fileName, value3, modName, $"Subtree replacement: replaced {value.Count} '{text}' elements with {value2.Count} from mod");
			hashSet.Add(value3);
			foreach (string item6 in value)
			{
				hashSet.Add(item6);
			}
			foreach (string item7 in value2)
			{
				hashSet.Add(item7);
			}
		}
		return hashSet;
	}

	private bool AddNewElement(XDocument mergedDoc, Dictionary<string, XElement> mergedIndex, string newPath, XElement newElement, string fileName, string modName, MergeReport report)
	{
		int num = newPath.LastIndexOf('/');
		if (num <= 0)
		{
			Log.Warning("[MERGE] {FileName}: Cannot add root-level element {Path} from {ModName}", fileName, newPath, modName);
			report.AddWarning($"MERGE: {fileName}:{newPath} - Cannot add root-level element");
			return false;
		}
		string text = newPath.Substring(0, num);
		if (!mergedIndex.TryGetValue(text, out XElement value))
		{
			Log.Warning("[MERGE] {FileName}: Parent path {ParentPath} not found for new element {Path} from {ModName}", fileName, text, newPath, modName);
			report.AddWarning($"MERGE: {fileName}:{newPath} - Parent element not found: {text}");
			return false;
		}
		XElement xElement = new XElement(newElement);
		value.Add(xElement);
		mergedIndex[newPath] = xElement;
		return true;
	}

	private void DetectDeletions(Dictionary<string, XElement> baseIndex, Dictionary<string, XElement> modIndex, string fileName, string modName, MergeReport report, bool isPatchFile)
	{
		List<string> deletedPaths = baseIndex.Keys.Except(modIndex.Keys).ToList();
		if (deletedPaths.Count == 0)
		{
			return;
		}
		if (isPatchFile)
		{
			Log.Debug("[MERGE] {FileName}: PTF file - ignoring {Count} apparent deletions (partial table)", fileName, deletedPaths.Count);
			return;
		}
		List<string> list = deletedPaths.Where((string path) => !deletedPaths.Any((string other) => other != path && path.StartsWith(other + "/"))).ToList();
		foreach (string deletedPath in list)
		{
			string localName = baseIndex[deletedPath].Name.LocalName;
			int num = deletedPaths.Count((string p) => p.StartsWith(deletedPath + "/"));
			string warning = ((num > 0) ? $"DELETED: {fileName}:{deletedPath} - Element '{localName}' and {num} children removed by {modName} (was present in base)" : $"DELETED: {fileName}:{deletedPath} - Element '{localName}' removed by {modName} (was present in base)");
			report.AddWarning(warning);
			Log.Warning("[DELETED] {FileName}: {Path} ({ElementName}) removed by {ModName} (including {ChildCount} children)", fileName, deletedPath, localName, modName, num);
		}
		if (list.Count > 0)
		{
			Log.Information("[MERGE] {FileName}: {ModName} deleted {Count} element(s) from base", fileName, modName, list.Count);
		}
	}

	private async Task<XDocument> MergeIdOnlyTable(XDocument baseDoc, XDocument modDoc, string fileName, string modName, int priority, MergeReport report, bool isPatchFile)
	{
		int warningCountBefore = report.Warnings.Count;
		XElement xElement = baseDoc.Root?.Descendants("rows").FirstOrDefault();
		XElement xElement2 = modDoc.Root?.Descendants("rows").FirstOrDefault();
		if (xElement == null || xElement2 == null)
		{
			Log.Warning("[MERGE] {FileName}: Missing <rows> element, falling back to standard merge", fileName);
			return new XDocument(baseDoc);
		}
		Path.GetFileNameWithoutExtension(fileName.Split(new string[1] { " (" }, StringSplitOptions.None)[0]);
		List<string> pkColumns = _indexer.DeriveTablePkSchema(baseDoc, fileName);
		if (pkColumns == null || pkColumns.Count == 0)
		{
			Log.Warning("[MERGE] {FileName}: No PK columns found, falling back to standard merge", fileName);
			return new XDocument(baseDoc);
		}
		RowSetDiff diff = _rowSetComparer.CompareRowSets(xElement, xElement2, pkColumns);
		if (isPatchFile)
		{
			int count = diff.RemovedRows.Count;
			Log.Information("[MERGE] {FileName}: PTF (Partial Table File) detected - ignoring {Removed} apparent removals", fileName, count);
			diff.RemovedRows.Clear();
		}
		Log.Debug("[MERGE] {FileName}: Row diff - Added: {Added}, Removed: {Removed}, Common: {Common}, PTF: {IsPTF}", fileName, diff.AddedRows.Count, diff.RemovedRows.Count, diff.CommonRows.Count, isPatchFile);
		if (diff.AddedRows.Count == 0 && diff.RemovedRows.Count == 0)
		{
			Log.Debug("[MERGE] {FileName}: No row changes from {ModName}", fileName, modName);
			return new XDocument(baseDoc);
		}
		RowMergeDecision rowMergeDecision = RowMergeDecision.IncludeBoth;
		if (_conflictResolver != null && diff.AddedRows.Count > 0 && diff.RemovedRows.Count > 0)
		{
			rowMergeDecision = await _conflictResolver.ResolveIdOnlyTableDifference(fileName, modName, diff.AddedRows.Count, diff.RemovedRows.Count, diff.AddedRows, diff.RemovedRows);
		}
		else if (diff.AddedRows.Count > 0 && diff.RemovedRows.Count == 0)
		{
			rowMergeDecision = RowMergeDecision.OnlyAdditions;
			Log.Information("[MERGE] {FileName}: Auto-applying {Added} additions from {ModName} (no removals)", fileName, diff.AddedRows.Count, modName);
		}
		else if (diff.AddedRows.Count == 0 && diff.RemovedRows.Count > 0)
		{
			rowMergeDecision = RowMergeDecision.OnlyRemovals;
			Log.Information("[MERGE] {FileName}: Auto-applying {Removed} removals from {ModName} (no additions)", fileName, diff.RemovedRows.Count, modName);
		}
		XDocument xDocument = new XDocument(baseDoc);
		XElement xElement3 = xDocument.Root?.Descendants("rows").FirstOrDefault();
		if (xElement3 == null)
		{
			report.RecordFileStats(fileName, modName, priority, isPatchFile, isIdOnlyTable: true, isStandardTable: false, 0, 0, 0, 0, 0);
			return xDocument;
		}
		switch (rowMergeDecision)
		{
		case RowMergeDecision.IncludeBoth:
			foreach (XElement removedRow in diff.RemovedRows)
			{
				string rowKey2 = GetRowKey(removedRow, pkColumns);
				xElement3.Elements("row").FirstOrDefault((XElement r) => GetRowKey(r, pkColumns) == rowKey2)?.Remove();
			}
			foreach (XElement addedRow in diff.AddedRows)
			{
				xElement3.Add(new XElement(addedRow));
			}
			Log.Information("[MERGE] {FileName}: Applied full changes from {ModName} (+{Added}, -{Removed})", fileName, modName, diff.AddedRows.Count, diff.RemovedRows.Count);
			break;
		case RowMergeDecision.OnlyAdditions:
			foreach (XElement addedRow2 in diff.AddedRows)
			{
				xElement3.Add(new XElement(addedRow2));
			}
			Log.Information("[MERGE] {FileName}: Applied additions from {ModName} (+{Added})", fileName, modName, diff.AddedRows.Count);
			break;
		case RowMergeDecision.OnlyRemovals:
			foreach (XElement removedRow2 in diff.RemovedRows)
			{
				string rowKey = GetRowKey(removedRow2, pkColumns);
				xElement3.Elements("row").FirstOrDefault((XElement r) => GetRowKey(r, pkColumns) == rowKey)?.Remove();
			}
			Log.Information("[MERGE] {FileName}: Applied removals from {ModName} (-{Removed})", fileName, modName, diff.RemovedRows.Count);
			break;
		case RowMergeDecision.SkipMod:
			Log.Information("[MERGE] {FileName}: Skipped changes from {ModName}", fileName, modName);
			break;
		}
		report.AddMergeDetail(fileName, "ID-only table", modName, $"Decision: {rowMergeDecision}, Added: {diff.AddedRows.Count}, Removed: {diff.RemovedRows.Count}");
		int additions = ((rowMergeDecision != RowMergeDecision.SkipMod) ? diff.AddedRows.Count : 0);
		int deletions = ((!isPatchFile) ? ((rowMergeDecision != RowMergeDecision.SkipMod && rowMergeDecision != RowMergeDecision.OnlyAdditions) ? diff.RemovedRows.Count : 0) : 0);
		int warnings = report.Warnings.Count - warningCountBefore;
		report.RecordFileStats(fileName, modName, priority, isPatchFile, isIdOnlyTable: true, isStandardTable: false, additions, deletions, 0, warnings, 0);
		return xDocument;
	}

	private string GetRowKey(XElement row, List<string>? pkColumns)
	{
		if (pkColumns == null || pkColumns.Count == 0)
		{
			return RowSetComparer.BuildRowHash(row);
		}
		List<string> list = new List<string>();
		foreach (string pkColumn in pkColumns)
		{
			string text = row.Attribute(pkColumn)?.Value ?? "";
			list.Add(pkColumn + "=" + text.ToLowerInvariant());
		}
		return string.Join("|", list);
	}

	public XDocument ApplyResolvedOps(XDocument vanillaDoc, ResolvedMergeOps ops, List<string>? pkColumns, string fileName, MergeReport report)
	{
		XDocument xDocument = new XDocument(vanillaDoc);
		XElement xElement = xDocument.Root?.Descendants("rows").FirstOrDefault();
		if (xElement == null)
		{
			Log.Warning("[APPLY] {FileName}: No <rows> element found", fileName);
			return xDocument;
		}
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		foreach (XElement item in ops.RowsToDelete)
		{
			string rowKey = GetRowKey(item, pkColumns);
			XElement xElement2 = xElement.Elements("row").FirstOrDefault((XElement r) => GetRowKey(r, pkColumns) == rowKey);
			if (xElement2 != null)
			{
				xElement2.Remove();
				num2++;
				num++;
				if (num % 50 == 0)
				{
					Console.Write(".");
				}
			}
		}
		Dictionary<string, (int, int, int)> dictionary = new Dictionary<string, (int, int, int)>();
		string key2;
		foreach (RowModification mod in ops.RowsToModify)
		{
			XElement xElement3 = xElement.Elements("row").FirstOrDefault((XElement r) => GetRowKey(r, pkColumns) == mod.RowKey);
			if (xElement3 == null)
			{
				continue;
			}
			foreach (KeyValuePair<string, string> attributeChange in mod.AttributeChanges)
			{
				attributeChange.Deconstruct(out var key, out key2);
				string text = key;
				string value = key2;
				XAttribute xAttribute = xElement3.Attribute(text);
				if (xAttribute != null)
				{
					xAttribute.Value = value;
				}
				else
				{
					xElement3.Add(new XAttribute(text, value));
				}
			}
			num3++;
			num++;
			if (num % 50 == 0)
			{
				Console.Write(".");
			}
			int count = mod.AttributeChanges.Count;
			if (dictionary.TryGetValue(mod.WinningMod, out var value2))
			{
				dictionary[mod.WinningMod] = (value2.Item1 + 1, Math.Min(value2.Item2, count), Math.Max(value2.Item3, count));
			}
			else
			{
				dictionary[mod.WinningMod] = (1, count, count);
			}
		}
		foreach (XElement item2 in ops.RowsToAdd)
		{
			string rowKey2 = GetRowKey(item2, pkColumns);
			if (xElement.Elements("row").FirstOrDefault((XElement r) => GetRowKey(r, pkColumns) == rowKey2) != null)
			{
				num5++;
				continue;
			}
			XElement xElement4 = new XElement(item2);
			xElement4.Attribute("action")?.Remove();
			xElement.Add(xElement4);
			num4++;
			num++;
			if (num % 50 == 0)
			{
				Console.Write(".");
			}
		}
		if (num > 0)
		{
			Console.WriteLine();
		}
		if (num2 > 0)
		{
			Log.Verbose("[APPLY] {FileName}: -{Deleted} deleted rows", fileName, num2);
		}
		foreach (KeyValuePair<string, (int, int, int)> item3 in dictionary)
		{
			item3.Deconstruct(out key2, out var value3);
			string text2 = key2;
			(int, int, int) tuple = value3;
			string text3 = ((tuple.Item2 == tuple.Item3) ? $"{tuple.Item2} attr each" : $"{tuple.Item2}-{tuple.Item3} attrs each");
			Log.Verbose("[APPLY] {FileName}: {ModName} → ~{Count} modified rows ({AttrRange})", fileName, text2, tuple.Item1, text3);
		}
		if (num4 > 0)
		{
			Log.Verbose("[APPLY] {FileName}: +{Added} added rows", fileName, num4);
		}
		if (num5 > 0)
		{
			Log.Verbose("[APPLY] {FileName}: {Skipped} duplicate additions skipped", fileName, num5);
		}
		return xDocument;
	}
}
