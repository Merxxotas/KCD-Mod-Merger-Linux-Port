using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Serilog;

namespace KCDMerge.Core.Data;

public class RowSetComparer
{
	public RowSetDiff CompareRowSets(XElement baseRows, XElement modRows, List<string> pkColumns)
	{
		HashSet<string> hashSet = ExtractRowKeys(baseRows, pkColumns);
		HashSet<string> hashSet2 = ExtractRowKeys(modRows, pkColumns);
		List<string> keys = hashSet2.Except<string>(hashSet, StringComparer.OrdinalIgnoreCase).ToList();
		List<string> keys2 = hashSet.Except<string>(hashSet2, StringComparer.OrdinalIgnoreCase).ToList();
		List<string> keys3 = hashSet.Intersect<string>(hashSet2, StringComparer.OrdinalIgnoreCase).ToList();
		return new RowSetDiff
		{
			AddedRows = GetRowsByKeys(modRows, keys, pkColumns),
			RemovedRows = GetRowsByKeys(baseRows, keys2, pkColumns),
			CommonRows = GetRowsByKeys(baseRows, keys3, pkColumns)
		};
	}

	public RowSetDiffDetailed CompareRowSetsDetailed(XElement baseRows, XElement modRows, List<string> pkColumns)
	{
		RowSetDiffDetailed rowSetDiffDetailed = new RowSetDiffDetailed();
		Dictionary<string, XElement> dictionary = BuildRowDictionary(baseRows, pkColumns);
		Dictionary<string, XElement> dictionary2 = BuildRowDictionary(modRows, pkColumns);
		string key;
		XElement value;
		foreach (KeyValuePair<string, XElement> item3 in dictionary2)
		{
			item3.Deconstruct(out key, out value);
			string key2 = key;
			XElement item = value;
			if (!dictionary.ContainsKey(key2))
			{
				rowSetDiffDetailed.AddedRows.Add(item);
			}
		}
		foreach (KeyValuePair<string, XElement> item4 in dictionary)
		{
			item4.Deconstruct(out key, out value);
			string key3 = key;
			XElement item2 = value;
			if (!dictionary2.ContainsKey(key3))
			{
				rowSetDiffDetailed.RemovedRows.Add(item2);
			}
		}
		foreach (KeyValuePair<string, XElement> item5 in dictionary)
		{
			item5.Deconstruct(out key, out value);
			string text = key;
			XElement xElement = value;
			if (dictionary2.TryGetValue(text, out var value2))
			{
				Dictionary<string, (string, string)> dictionary3 = DetectAttributeChanges(xElement, value2, pkColumns);
				if (dictionary3.Count > 0)
				{
					rowSetDiffDetailed.ModifiedRows.Add(new RowModificationDetail
					{
						RowKey = text,
						BaseRow = xElement,
						ModRow = value2,
						ChangedAttributes = dictionary3
					});
				}
				else
				{
					rowSetDiffDetailed.UnchangedRows.Add(xElement);
				}
			}
		}
		return rowSetDiffDetailed;
	}

	private Dictionary<string, XElement> BuildRowDictionary(XElement rowsElement, List<string> pkColumns)
	{
		Dictionary<string, XElement> dictionary = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
		foreach (XElement item in rowsElement.Elements("row"))
		{
			string key = BuildRowKey(item, pkColumns);
			dictionary[key] = item;
		}
		return dictionary;
	}

	private string BuildRowKey(XElement row, List<string> pkColumns)
	{
		List<string> list = new List<string>();
		foreach (string pkColumn in pkColumns)
		{
			string text = row.Attribute(pkColumn)?.Value ?? "";
			list.Add(pkColumn + "=" + text.ToLowerInvariant());
		}
		return string.Join("|", list);
	}

	private Dictionary<string, (string OldValue, string NewValue)> DetectAttributeChanges(XElement baseRow, XElement modRow, List<string> pkColumns)
	{
		Dictionary<string, (string, string)> dictionary = new Dictionary<string, (string, string)>();
		Dictionary<string, string> dictionary2 = baseRow.Attributes().ToDictionary((XAttribute a) => a.Name.LocalName, (XAttribute a) => a.Value);
		Dictionary<string, string> dictionary3 = modRow.Attributes().ToDictionary((XAttribute a) => a.Name.LocalName, (XAttribute a) => a.Value);
		string value;
		string key;
		foreach (KeyValuePair<string, string> item2 in dictionary3)
		{
			item2.Deconstruct(out value, out key);
			string text = value;
			string text2 = key;
			if (pkColumns.Contains<string>(text, StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}
			if (dictionary2.TryGetValue(text, out var value2))
			{
				if (value2 != text2)
				{
					dictionary[text] = (value2, text2);
				}
			}
			else
			{
				dictionary[text] = ("", text2);
			}
		}
		foreach (KeyValuePair<string, string> item3 in dictionary2)
		{
			item3.Deconstruct(out key, out value);
			string text3 = key;
			string item = value;
			if (!pkColumns.Contains<string>(text3, StringComparer.OrdinalIgnoreCase) && !dictionary3.ContainsKey(text3))
			{
				dictionary[text3] = (item, "");
			}
		}
		return dictionary;
	}

	public static string BuildRowHash(XElement row)
	{
		return string.Join("|", from a in (from a in row.Attributes()
				where a.Name.LocalName != "action"
				select a).OrderBy<XAttribute, string>((XAttribute a) => a.Name.LocalName, StringComparer.OrdinalIgnoreCase)
			select a.Name.LocalName + "=" + a.Value);
	}

	public RowSetDiff CompareRowSetsByHash(XElement baseRows, XElement modRows)
	{
		int num = baseRows.Elements("row").Count();
		int num2 = modRows.Elements("row").Count();
		Dictionary<string, XElement> baseRowMap = new Dictionary<string, XElement>(StringComparer.Ordinal);
		foreach (XElement item in baseRows.Elements("row"))
		{
			string key = BuildRowHash(item);
			baseRowMap.TryAdd(key, item);
		}
		Dictionary<string, XElement> modRowMap = new Dictionary<string, XElement>(StringComparer.Ordinal);
		foreach (XElement item2 in modRows.Elements("row"))
		{
			string key2 = BuildRowHash(item2);
			modRowMap.TryAdd(key2, item2);
		}
		List<string> list = modRowMap.Keys.Except(baseRowMap.Keys).ToList();
		List<string> list2 = baseRowMap.Keys.Except(modRowMap.Keys).ToList();
		List<string> list3 = baseRowMap.Keys.Intersect(modRowMap.Keys).ToList();
		Log.Verbose("[HASH-COMPARE] Base: {BaseCount} rows, Mod: {ModCount} rows → +{Added} -{Removed} ={Common}", num, num2, list.Count, list2.Count, list3.Count);
		return new RowSetDiff
		{
			AddedRows = list.Select((string h) => modRowMap[h]).ToList(),
			RemovedRows = list2.Select((string h) => baseRowMap[h]).ToList(),
			CommonRows = new List<XElement>()
		};
	}

	private HashSet<string> ExtractRowKeys(XElement rowsElement, List<string> pkColumns)
	{
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (XElement item in rowsElement.Elements("row"))
		{
			List<string> list = new List<string>();
			foreach (string pkColumn in pkColumns)
			{
				string text = item.Attribute(pkColumn)?.Value ?? "";
				list.Add(pkColumn + "=" + text.ToLowerInvariant());
			}
			hashSet.Add(string.Join("|", list));
		}
		return hashSet;
	}

	private List<XElement> GetRowsByKeys(XElement rowsElement, List<string> keys, List<string> pkColumns)
	{
		List<XElement> list = new List<XElement>();
		foreach (XElement item in rowsElement.Elements("row"))
		{
			List<string> list2 = new List<string>();
			foreach (string pkColumn in pkColumns)
			{
				string text = item.Attribute(pkColumn)?.Value ?? "";
				list2.Add(pkColumn + "=" + text.ToLowerInvariant());
			}
			string value = string.Join("|", list2);
			if (keys.Contains<string>(value, StringComparer.OrdinalIgnoreCase))
			{
				list.Add(item);
			}
		}
		return list;
	}
}
