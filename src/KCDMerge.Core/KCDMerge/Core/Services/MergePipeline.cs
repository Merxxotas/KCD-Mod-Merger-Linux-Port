using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using KCDMerge.Core.Configuration;
using KCDMerge.Core.Data;
using Serilog;

namespace KCDMerge.Core.Services;

public class MergePipeline : IMergePipeline
{
	private readonly IXDocumentLoader _loader;

	private readonly IXDocumentMerger _merger;

	private readonly IDeltaNormalizer _deltaNormalizer;

	private readonly IXPathIndexer _indexer;

	private readonly IConflictResolver? _conflictResolver;

	private readonly IModConflictRulesService _rulesService;

	private readonly IDeltaCacheService _deltaCache;

	private readonly MergeReport _report;

	private readonly AppConfig? _appConfig;

	public MergePipeline(IXDocumentLoader loader, IXDocumentMerger merger, IDeltaNormalizer deltaNormalizer, IXPathIndexer indexer, IConflictResolver? conflictResolver, IModConflictRulesService rulesService, IDeltaCacheService deltaCache, AppConfig? appConfig = null)
	{
		_loader = loader ?? throw new ArgumentNullException("loader");
		_merger = merger ?? throw new ArgumentNullException("merger");
		_deltaNormalizer = deltaNormalizer ?? throw new ArgumentNullException("deltaNormalizer");
		_indexer = indexer ?? throw new ArgumentNullException("indexer");
		_conflictResolver = conflictResolver;
		_rulesService = rulesService ?? throw new ArgumentNullException("rulesService");
		_deltaCache = deltaCache ?? throw new ArgumentNullException("deltaCache");
		_appConfig = appConfig;
		_report = new MergeReport();
	}

	public async Task<MergeResult> MergeFileAsync(string fileName, IReadOnlyList<ModFileSource> modSources)
	{
		if (string.IsNullOrEmpty(fileName))
		{
			Log.Warning("[PIPELINE] Cannot merge: fileName is null or empty");
			return new MergeResult();
		}
		if (modSources == null || modSources.Count == 0)
		{
			Log.Warning("[PIPELINE] {FileName}: No mod sources provided", fileName);
			return new MergeResult();
		}
		try
		{
			Log.Information("[PIPELINE] {FileName}: Starting merge with {ModCount} mod(s)", fileName, modSources.Count);
			XDocument vanillaDoc = await _loader.LoadVanillaAsync(fileName);
			if (vanillaDoc == null)
			{
				XDocument mergedDoc = await HandleModAddition(fileName, modSources);
				return new MergeResult
				{
					MergedDoc = mergedDoc,
					VanillaDoc = null,
					TableType = null,
					PkColumns = null,
					HasTableStructure = false
				};
			}
			List<string> pkColumns = _indexer.DeriveTablePkSchema(vanillaDoc, fileName);
			bool flag = pkColumns != null && pkColumns.Count > 0;
			bool hasTableStructure = _indexer.HasTableStructure(vanillaDoc);
			Log.Verbose("[PIPELINE] {FileName}: HasPK={HasPk} [{PkColumns}], HasTableStructure={HasTableStructure}", fileName, flag, flag ? string.Join(", ", pkColumns) : "", hasTableStructure);
			if (!flag && !hasTableStructure)
			{
				Log.Verbose("[PIPELINE] {FileName}: Routing to sequential XPath merge (Type C)", fileName);
				XDocument mergedDoc2 = await MergeSequential(vanillaDoc, fileName, modSources);
				return new MergeResult
				{
					MergedDoc = mergedDoc2,
					VanillaDoc = vanillaDoc,
					TableType = DeltaType.NonTable,
					PkColumns = null,
					HasTableStructure = false
				};
			}
			ModConflictTracker tracker = new ModConflictTracker(_conflictResolver, _rulesService);
			DateTime vanillaTimestamp = _loader.GetVanillaTimestamp(fileName);
			DateTime vanillaEntryTimestamp = _loader.GetVanillaEntryTimestamp(fileName);
			string stalenessMode = _appConfig?.StalenessCheckMode ?? "WarnOnly";
			List<string> stalenessCheckMods = _appConfig?.StalenessCheckMods ?? new List<string>();
			DeltaType? detectedType = null;
			foreach (ModFileSource mod in modSources.OrderBy((ModFileSource m) => m.Priority))
			{
				if (mod.Timestamp != DateTime.MinValue && vanillaEntryTimestamp != DateTime.MinValue && mod.Timestamp < vanillaEntryTimestamp)
				{
					int daysBehind = (int)(vanillaEntryTimestamp - mod.Timestamp).TotalDays;
					bool flag2 = stalenessCheckMods.Any((string m) => string.Equals(m, mod.ModName, StringComparison.OrdinalIgnoreCase));
					bool num = _conflictResolver != null && (flag2 || string.Equals(stalenessMode, "Prompt", StringComparison.OrdinalIgnoreCase));
					bool flag3 = !string.Equals(stalenessMode, "Off", StringComparison.OrdinalIgnoreCase);
					if (num)
					{
						StalenessDecision stalenessDecision = await _conflictResolver.ResolveStaleModAsync(fileName, mod.ModName, mod.Timestamp, vanillaEntryTimestamp);
						_report.AddStalenessWarning(fileName, mod.ModName, mod.Timestamp, vanillaEntryTimestamp, stalenessDecision.ToString());
						if (stalenessDecision == StalenessDecision.UseWholeXml)
						{
							XDocument xDocument = await _loader.LoadFromStreamAsync(mod.XmlStream, fileName);
							if (xDocument != null)
							{
								Log.Information("[PIPELINE] {FileName}: {ModName} is stale — using whole XML as-is (no merge)", fileName, mod.ModName);
								_report.RecordFileStats(fileName, mod.ModName, mod.Priority, mod.IsPatchFile, isIdOnlyTable: false, isStandardTable: false, 0, 0, 0, 1, 0);
								return new MergeResult
								{
									MergedDoc = xDocument,
									VanillaDoc = vanillaDoc,
									TableType = DeltaType.NonTable,
									PkColumns = null,
									HasTableStructure = false
								};
							}
							_report.AddError("PIPELINE: " + fileName + " - UseWholeXml: failed to load XML from " + mod.ModName);
						}
						Log.Warning("[PIPELINE] {FileName}: {ModName} is stale ({Days}d behind vanilla) — merging anyway", fileName, mod.ModName, daysBehind);
					}
					else if (flag3)
					{
						_report.AddStalenessWarning(fileName, mod.ModName, mod.Timestamp, vanillaEntryTimestamp, "WarnOnly");
						Log.Warning("[PIPELINE] {FileName}: {ModName} is stale ({Days}d behind vanilla) — warning only", fileName, mod.ModName, daysBehind);
					}
				}
				XDocument xDocument2 = await _loader.LoadFromStreamAsync(mod.XmlStream, fileName);
				if (xDocument2 == null)
				{
					_report.AddError("Failed to load " + fileName + " from " + mod.ModName);
					continue;
				}
				DateTime timestamp = mod.Timestamp;
				DeltaResult deltaResult = _deltaCache.TryGetCachedDelta(fileName, mod.ModName, timestamp, vanillaTimestamp, mod.PakPath);
				if (deltaResult == null)
				{
					deltaResult = _deltaNormalizer.NormalizeToDelta(vanillaDoc, xDocument2, fileName, mod.ModName, mod.IsPatchFile);
					_deltaCache.CacheDelta(fileName, mod.ModName, timestamp, vanillaTimestamp, deltaResult, mod.PakPath);
				}
				detectedType.GetValueOrDefault();
				if (!detectedType.HasValue)
				{
					DeltaType type = deltaResult.Type;
					detectedType = type;
				}
				Log.Verbose("[PIPELINE] {FileName}: {ModName} delta type: {DeltaType} (+{Added} -{Deleted} ~{Modified})", fileName, mod.ModName, deltaResult.Type, deltaResult.AddedRows, deltaResult.DeletedRows, deltaResult.ModifiedRows);
				tracker.RegisterDelta(fileName, mod.ModName, deltaResult);
				bool flag4 = deltaResult.Type == DeltaType.HashTable;
				_report.RecordFileStats(fileName, mod.ModName, mod.Priority, mod.IsPatchFile, deltaResult.Type == DeltaType.IdOnlyTable, deltaResult.Type == DeltaType.StandardTable || flag4, deltaResult.AddedRows, deltaResult.DeletedRows, deltaResult.ModifiedRows, 0, 0);
			}
			ResolvedMergeOps ops = await tracker.ResolveConflictsAsync(fileName);
			Log.Verbose("[PIPELINE] {FileName}: Applying resolved operations (final type: {DeltaType})", fileName, detectedType);
			XDocument mergedDoc3 = _merger.ApplyResolvedOps(vanillaDoc, ops, pkColumns, fileName, _report);
			Log.Information("[PIPELINE] {FileName}: Merge complete - processed {ModCount} mod(s), type: {DeltaType}", fileName, modSources.Count, detectedType);
			return new MergeResult
			{
				MergedDoc = mergedDoc3,
				VanillaDoc = vanillaDoc,
				TableType = detectedType,
				PkColumns = pkColumns,
				HasTableStructure = hasTableStructure
			};
		}
		catch (Exception ex)
		{
			string error = "PIPELINE: " + fileName + " - Unexpected error during merge: " + ex.Message;
			Log.Error(ex, "[PIPELINE ERROR] {FileName}", fileName);
			_report.AddError(error);
			return new MergeResult();
		}
	}

	private async Task<XDocument?> MergeModIntoBase(XDocument baseDoc, ModFileSource modSource, string fileName, XDocument? vanillaDoc = null)
	{
		try
		{
			XDocument xDocument = await _loader.LoadFromStreamAsync(modSource.XmlStream, fileName);
			if (xDocument == null)
			{
				string error = $"INVALID XML: {fileName} from mod '{modSource.ModName}' - File could not be parsed as XML. The mod may contain a corrupted or non-XML file at this path. This mod's changes for this file will NOT be included in the merge.";
				Log.Error("[PIPELINE ERROR] {FileName}: Failed to load XML from {ModName} - Invalid or corrupted XML file", fileName, modSource.ModName);
				_report.AddError(error);
				return baseDoc;
			}
			return _merger.Merge(baseDoc, xDocument, fileName, modSource.ModName, modSource.Priority, _report, modSource.IsPatchFile, vanillaDoc);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "[PIPELINE] {FileName}: Error merging {ModName}", fileName, modSource.ModName);
			_report.AddError($"PIPELINE: {fileName} - Error merging {modSource.ModName}: {ex.Message}");
			return baseDoc;
		}
	}

	public MergeReport GetReport()
	{
		return _report;
	}

	public void ResetReport()
	{
		_report.Clear();
	}

	private async Task<XDocument?> HandleModAddition(string fileName, IReadOnlyList<ModFileSource> modSources)
	{
		Log.Information("[PIPELINE] {FileName}: No vanilla baseline found - treating as mod addition", fileName);
		ModFileSource firstMod = modSources.OrderBy((ModFileSource m) => m.Priority).First();
		XDocument xDocument = await _loader.LoadFromStreamAsync(firstMod.XmlStream, fileName);
		if (xDocument == null)
		{
			_report.AddError("PIPELINE: " + fileName + " - Failed to load from first mod " + firstMod.ModName);
			return null;
		}
		Log.Information("[PIPELINE] {FileName}: Using {ModName} as baseline (new file)", fileName, firstMod.ModName);
		if (modSources.Any((ModFileSource m) => m.IsLocalization))
		{
			int additions = xDocument.Root?.Descendants("row").Count() ?? xDocument.Root?.Descendants("Row").Count() ?? 0;
			_report.RecordFileStats(fileName, firstMod.ModName, firstMod.Priority, firstMod.IsPatchFile, isIdOnlyTable: false, isStandardTable: false, additions, 0, 0, 0, 0);
			return await MergeLocalizationTable(fileName, xDocument, firstMod, modSources);
		}
		int additions2 = xDocument.Root?.Descendants("row").Count() ?? xDocument.Root?.Descendants("Row").Count() ?? 0;
		_report.RecordFileStats(fileName, firstMod.ModName, firstMod.Priority, firstMod.IsPatchFile, isIdOnlyTable: false, isStandardTable: false, additions2, 0, 0, 0, 0);
		List<ModFileSource> list = (from m in modSources
			where m.Priority > firstMod.Priority || (m.Priority == firstMod.Priority && m.ModName != firstMod.ModName)
			orderby m.Priority, m.ModName
			select m).ToList();
		foreach (ModFileSource modSource in list)
		{
			xDocument = await MergeModIntoBase(xDocument, modSource, fileName);
			if (xDocument == null)
			{
				_report.AddError("PIPELINE: " + fileName + " - Merge failed at mod " + modSource.ModName);
				return null;
			}
		}
		return xDocument;
	}

	private async Task<XDocument?> MergeLocalizationTable(string fileName, XDocument baseDoc, ModFileSource firstMod, IReadOnlyList<ModFileSource> allMods)
	{
		Log.Information("[LOCALIZATION] {FileName}: Merging localization table with first-Cell keying", fileName);
		Dictionary<string, (XElement Row, string ModName)> rowIndex = new Dictionary<string, (XElement, string)>(StringComparer.OrdinalIgnoreCase);
		if (baseDoc.Root?.Name.LocalName == "Table")
		{
			foreach (XElement item in baseDoc.Root.Elements("Row"))
			{
				string text = item.Elements("Cell").FirstOrDefault()?.Value;
				if (!string.IsNullOrEmpty(text))
				{
					rowIndex[text] = (item, firstMod.ModName);
				}
				else
				{
					Log.Warning("[LOCALIZATION] {FileName}: Row without key in {ModName}", fileName, firstMod.ModName);
				}
			}
			Log.Debug("[LOCALIZATION] {FileName}: Indexed {Count} rows from {ModName}", fileName, rowIndex.Count, firstMod.ModName);
			List<ModFileSource> list = (from m in allMods
				where m.Priority > firstMod.Priority || (m.Priority == firstMod.Priority && m.ModName != firstMod.ModName)
				orderby m.Priority, m.ModName
				select m).ToList();
			foreach (ModFileSource modSource in list)
			{
				XDocument xDocument = await _loader.LoadFromStreamAsync(modSource.XmlStream, fileName);
				if (xDocument == null)
				{
					_report.AddError("LOCALIZATION: " + fileName + " - Failed to load from " + modSource.ModName);
					continue;
				}
				if (xDocument.Root?.Name.LocalName != "Table")
				{
					_report.AddError($"LOCALIZATION: {fileName} - {modSource.ModName} has invalid root <{xDocument.Root?.Name.LocalName}>");
					continue;
				}
				int addedCount = 0;
				int conflictCount = 0;
				foreach (XElement modRow in xDocument.Root.Elements("Row"))
				{
					string key = modRow.Elements("Cell").FirstOrDefault()?.Value;
					(XElement, string) value;
					if (string.IsNullOrEmpty(key))
					{
						Log.Warning("[LOCALIZATION] {FileName}: Row without key in {ModName}", fileName, modSource.ModName);
					}
					else if (rowIndex.TryGetValue(key, out value))
					{
						XElement existingRow = value.Item1;
						string existingMod = value.Item2;
						string text2 = string.Join("|", from c in existingRow.Elements("Cell")
							select c.Value);
						string text3 = string.Join("|", from c in modRow.Elements("Cell")
							select c.Value);
						if (text2 != text3)
						{
							conflictCount++;
							string xmlConflictWinner = _rulesService.GetXmlConflictWinner(existingMod, modSource.ModName);
							string text4;
							if (xmlConflictWinner != null)
							{
								text4 = xmlConflictWinner;
								Log.Information("[LOCALIZATION] {FileName}: Using saved rule - {Winner} wins for key '{Key}'", fileName, text4, key);
							}
							else if (_conflictResolver != null)
							{
								string conflictSummary = "Key '" + key + "' exists in both mods with different translations";
								text4 = await _conflictResolver.ResolveModConflictAsync(existingMod, modSource.ModName, conflictSummary);
								_rulesService.SetXmlConflictWinner(text4, (text4 == existingMod) ? modSource.ModName : existingMod);
							}
							else
							{
								text4 = existingMod;
							}
							if (text4 == modSource.ModName)
							{
								XElement xElement = new XElement(modRow);
								existingRow.ReplaceWith(xElement);
								rowIndex[key] = (xElement, modSource.ModName);
								Log.Information("[LOCALIZATION] {FileName}: Conflict resolved - {Winner} wins for key '{Key}'", fileName, modSource.ModName, key);
							}
							else
							{
								Log.Information("[LOCALIZATION] {FileName}: Conflict resolved - {Winner} wins for key '{Key}'", fileName, existingMod, key);
							}
						}
					}
					else
					{
						baseDoc.Root.Add(new XElement(modRow));
						rowIndex[key] = (modRow, modSource.ModName);
						addedCount++;
					}
				}
				Log.Information("[LOCALIZATION] {FileName}: {ModName} - Added: {Added}, Conflicts: {Conflicts}", fileName, modSource.ModName, addedCount, conflictCount);
				_report.RecordFileStats(fileName, modSource.ModName, modSource.Priority, modSource.IsPatchFile, isIdOnlyTable: false, isStandardTable: false, addedCount, 0, conflictCount, 0, 0);
			}
			Log.Information("[LOCALIZATION] {FileName}: Merge complete - {TotalRows} total rows", fileName, rowIndex.Count);
			return baseDoc;
		}
		_report.AddError($"LOCALIZATION: {fileName} - Expected <Table> root, found <{baseDoc.Root?.Name.LocalName}>");
		return null;
	}

	private async Task<XDocument?> MergeSequential(XDocument baseDoc, string fileName, IReadOnlyList<ModFileSource> modSources)
	{
		_merger.ResetAttributeTracking();
		XDocument xDocument = baseDoc;
		foreach (ModFileSource mod in modSources.OrderBy((ModFileSource m) => m.Priority))
		{
			xDocument = await MergeModIntoBase(xDocument, mod, fileName, baseDoc);
			if (xDocument == null)
			{
				_report.AddError("PIPELINE: " + fileName + " - Merge failed at " + mod.ModName);
				return null;
			}
		}
		return xDocument;
	}
}
