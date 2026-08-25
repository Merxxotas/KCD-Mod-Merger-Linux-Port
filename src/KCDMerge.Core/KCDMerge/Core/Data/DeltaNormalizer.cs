using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Serilog;

namespace KCDMerge.Core.Data;

public class DeltaNormalizer : IDeltaNormalizer
{
	private readonly IXPathIndexer _indexer;

	private readonly RowSetComparer _rowSetComparer;

	public DeltaNormalizer(IXPathIndexer indexer)
	{
		_indexer = indexer ?? throw new ArgumentNullException("indexer");
		_rowSetComparer = new RowSetComparer();
	}

	public DeltaResult NormalizeToDelta(XDocument vanillaDoc, XDocument modDoc, string fileName, string modName, bool isPatchFile)
	{
		List<string> list = _indexer.DeriveTablePkSchema(vanillaDoc, fileName);
		Log.Verbose("[DELTA] {FileName}: Derived PK columns: [{PkColumns}]", fileName, (list != null) ? string.Join(", ", list) : "null");
		if (list != null && list.Count > 0)
		{
			if (!_indexer.ValidatePkUniqueness(vanillaDoc, list))
			{
				Log.Information("[DELTA] {FileName}: PK [{PkColumns}] is non-unique, using hash-based comparison", fileName, string.Join(", ", list));
				return NormalizeHashTable(vanillaDoc, modDoc, fileName, modName, isPatchFile);
			}
			bool flag = _indexer.IsIdOnlyTable(fileName);
			Log.Verbose("[DELTA] {FileName}: IsIdOnly={IsIdOnly}", fileName, flag);
			if (flag)
			{
				Log.Verbose("[DELTA] {FileName}: Routing to ID-only table normalization", fileName);
				return NormalizeIdOnlyTable(vanillaDoc, modDoc, fileName, modName, isPatchFile, list);
			}
			Log.Verbose("[DELTA] {FileName}: Routing to standard table normalization", fileName);
			return NormalizeStandardTable(vanillaDoc, modDoc, fileName, modName, isPatchFile, list);
		}
		bool flag2 = _indexer.HasTableStructure(vanillaDoc);
		Log.Verbose("[DELTA] {FileName}: HasTableStructure={HasTableStructure}", fileName, flag2);
		if (flag2)
		{
			Log.Information("[DELTA] {FileName}: No PK columns but has table structure, using hash-based comparison", fileName);
			return NormalizeHashTable(vanillaDoc, modDoc, fileName, modName, isPatchFile);
		}
		Log.Verbose("[DELTA] {FileName}: Non-table XML, passthrough", fileName);
		return new DeltaResult
		{
			Type = DeltaType.NonTable,
			DeltaDoc = new XDocument(modDoc),
			PkColumns = null
		};
	}

	private DeltaResult NormalizeHashTable(XDocument vanillaDoc, XDocument modDoc, string fileName, string modName, bool isPatchFile)
	{
		XElement xElement = vanillaDoc.Root?.Descendants("rows").FirstOrDefault();
		XElement xElement2 = modDoc.Root?.Descendants("rows").FirstOrDefault();
		if (xElement == null || xElement2 == null)
		{
			Log.Warning("[DELTA] {FileName}: Missing <rows> element, passthrough", fileName);
			return new DeltaResult
			{
				Type = DeltaType.NonTable,
				DeltaDoc = new XDocument(modDoc),
				PkColumns = null
			};
		}
		RowSetDiff rowSetDiff = _rowSetComparer.CompareRowSetsByHash(xElement, xElement2);
		XDocument xDocument = new XDocument(vanillaDoc);
		XElement xElement3 = xDocument.Root?.Descendants("rows").FirstOrDefault();
		if (xElement3 == null)
		{
			Log.Warning("[DELTA] {FileName}: Could not find <rows> in delta doc", fileName);
			return new DeltaResult
			{
				Type = DeltaType.HashTable,
				DeltaDoc = xDocument,
				PkColumns = null
			};
		}
		xElement3.RemoveAll();
		foreach (XElement addedRow in rowSetDiff.AddedRows)
		{
			xElement3.Add(new XElement(addedRow));
		}
		int num = 0;
		if (!isPatchFile)
		{
			foreach (XElement removedRow in rowSetDiff.RemovedRows)
			{
				XElement xElement4 = new XElement(removedRow);
				xElement4.SetAttributeValue("action", "delete");
				xElement3.Add(xElement4);
				num++;
			}
		}
		Log.Debug("[DELTA] {FileName}: {ModName} - Hash table delta: +{Added}, -{Deleted}, PTF={IsPTF}", fileName, modName, rowSetDiff.AddedRows.Count, num, isPatchFile);
		return new DeltaResult
		{
			Type = DeltaType.HashTable,
			DeltaDoc = xDocument,
			PkColumns = null,
			AddedRows = rowSetDiff.AddedRows.Count,
			DeletedRows = num,
			ModifiedRows = 0,
			UnchangedRows = 0
		};
	}

	private DeltaResult NormalizeIdOnlyTable(XDocument vanillaDoc, XDocument modDoc, string fileName, string modName, bool isPatchFile, List<string> pkColumns)
	{
		XElement xElement = vanillaDoc.Root?.Descendants("rows").FirstOrDefault();
		XElement xElement2 = modDoc.Root?.Descendants("rows").FirstOrDefault();
		if (xElement == null || xElement2 == null)
		{
			Log.Warning("[DELTA] {FileName}: Missing <rows> element, passthrough", fileName);
			return new DeltaResult
			{
				Type = DeltaType.NonTable,
				DeltaDoc = new XDocument(modDoc),
				PkColumns = pkColumns
			};
		}
		RowSetDiff rowSetDiff = _rowSetComparer.CompareRowSets(xElement, xElement2, pkColumns);
		XDocument xDocument = new XDocument(vanillaDoc);
		XElement xElement3 = xDocument.Root?.Descendants("rows").FirstOrDefault();
		if (xElement3 == null)
		{
			Log.Warning("[DELTA] {FileName}: Could not find <rows> in delta doc", fileName);
			return new DeltaResult
			{
				Type = DeltaType.IdOnlyTable,
				DeltaDoc = xDocument,
				PkColumns = pkColumns
			};
		}
		xElement3.RemoveAll();
		foreach (XElement addedRow in rowSetDiff.AddedRows)
		{
			xElement3.Add(new XElement(addedRow));
		}
		int num = 0;
		if (!isPatchFile)
		{
			foreach (XElement removedRow in rowSetDiff.RemovedRows)
			{
				XElement xElement4 = new XElement(removedRow);
				xElement4.SetAttributeValue("action", "delete");
				xElement3.Add(xElement4);
				num++;
			}
		}
		Log.Debug("[DELTA] {FileName}: {ModName} - ID-only table delta: +{Added}, -{Deleted}, ={Unchanged}, PTF={IsPTF}", fileName, modName, rowSetDiff.AddedRows.Count, num, rowSetDiff.CommonRows.Count, isPatchFile);
		return new DeltaResult
		{
			Type = DeltaType.IdOnlyTable,
			DeltaDoc = xDocument,
			PkColumns = pkColumns,
			AddedRows = rowSetDiff.AddedRows.Count,
			DeletedRows = num,
			ModifiedRows = 0,
			UnchangedRows = rowSetDiff.CommonRows.Count
		};
	}

	private DeltaResult NormalizeStandardTable(XDocument vanillaDoc, XDocument modDoc, string fileName, string modName, bool isPatchFile, List<string> pkColumns)
	{
		XElement xElement = vanillaDoc.Root?.Descendants("rows").FirstOrDefault();
		XElement xElement2 = modDoc.Root?.Descendants("rows").FirstOrDefault();
		if (xElement == null || xElement2 == null)
		{
			Log.Warning("[DELTA] {FileName}: Missing <rows> element, passthrough", fileName);
			return new DeltaResult
			{
				Type = DeltaType.NonTable,
				DeltaDoc = new XDocument(modDoc),
				PkColumns = pkColumns
			};
		}
		RowSetDiffDetailed rowSetDiffDetailed = _rowSetComparer.CompareRowSetsDetailed(xElement, xElement2, pkColumns);
		XDocument xDocument = new XDocument(vanillaDoc);
		XElement xElement3 = xDocument.Root?.Descendants("rows").FirstOrDefault();
		if (xElement3 == null)
		{
			Log.Warning("[DELTA] {FileName}: Could not find <rows> in delta doc", fileName);
			return new DeltaResult
			{
				Type = DeltaType.StandardTable,
				DeltaDoc = xDocument,
				PkColumns = pkColumns
			};
		}
		xElement3.RemoveAll();
		foreach (XElement addedRow in rowSetDiffDetailed.AddedRows)
		{
			xElement3.Add(new XElement(addedRow));
		}
		int num = 0;
		if (!isPatchFile)
		{
			foreach (XElement removedRow in rowSetDiffDetailed.RemovedRows)
			{
				XElement xElement4 = new XElement("row");
				xElement4.SetAttributeValue("action", "delete");
				foreach (string pkColumn in pkColumns)
				{
					string text = removedRow.Attribute(pkColumn)?.Value;
					if (text != null)
					{
						xElement4.SetAttributeValue(pkColumn, text);
					}
				}
				xElement3.Add(xElement4);
				num++;
			}
		}
		foreach (RowModificationDetail modifiedRow in rowSetDiffDetailed.ModifiedRows)
		{
			XElement xElement5 = new XElement("row");
			xElement5.SetAttributeValue("action", "modify");
			foreach (string pkColumn2 in pkColumns)
			{
				string text2 = modifiedRow.ModRow.Attribute(pkColumn2)?.Value;
				if (text2 != null)
				{
					xElement5.SetAttributeValue(pkColumn2, text2);
				}
			}
			foreach (KeyValuePair<string, (string, string)> changedAttribute in modifiedRow.ChangedAttributes)
			{
				changedAttribute.Deconstruct(out var key, out var value);
				(string, string) tuple = value;
				string text3 = key;
				string item = tuple.Item2;
				xElement5.SetAttributeValue(text3, item);
			}
			xElement3.Add(xElement5);
		}
		Log.Debug("[DELTA] {FileName}: {ModName} - Standard table delta: +{Added}, -{Deleted}, ~{Modified}, ={Unchanged}, PTF={IsPTF}", fileName, modName, rowSetDiffDetailed.AddedRows.Count, num, rowSetDiffDetailed.ModifiedRows.Count, rowSetDiffDetailed.UnchangedRows.Count, isPatchFile);
		return new DeltaResult
		{
			Type = DeltaType.StandardTable,
			DeltaDoc = xDocument,
			PkColumns = pkColumns,
			AddedRows = rowSetDiffDetailed.AddedRows.Count,
			DeletedRows = num,
			ModifiedRows = rowSetDiffDetailed.ModifiedRows.Count,
			UnchangedRows = rowSetDiffDetailed.UnchangedRows.Count
		};
	}
}
