using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Serilog;

namespace KCDMerge.Core.Data;

public class PtfGenerator : IPtfGenerator
{
	public PtfResult GeneratePtf(XDocument mergedDoc, XDocument vanillaDoc, List<string> pkColumns, string fileName, bool supportDeleteMarking = false)
	{
		if (mergedDoc?.Root == null || vanillaDoc?.Root == null)
		{
			Log.Warning("[PTF-GEN] {FileName}: Null document(s), cannot generate PTF", fileName);
			return new PtfResult
			{
				RequiresFullXmlFallback = true
			};
		}
		XElement xElement = vanillaDoc.Root.Descendants("rows").FirstOrDefault();
		XElement xElement2 = mergedDoc.Root.Descendants("rows").FirstOrDefault();
		if (xElement == null || xElement2 == null)
		{
			Log.Warning("[PTF-GEN] {FileName}: Missing <rows> element, cannot generate PTF", fileName);
			return new PtfResult
			{
				RequiresFullXmlFallback = true
			};
		}
		Dictionary<string, XElement> dictionary = BuildRowIndex(xElement, pkColumns);
		Dictionary<string, XElement> dictionary2 = BuildRowIndex(xElement2, pkColumns);
		List<XElement> list = new List<XElement>();
		List<XElement> list2 = new List<XElement>();
		int num = 0;
		string key;
		XElement value;
		foreach (KeyValuePair<string, XElement> item in dictionary2)
		{
			item.Deconstruct(out key, out value);
			string key2 = key;
			XElement xElement3 = value;
			if (!dictionary.TryGetValue(key2, out var value2))
			{
				list.Add(xElement3);
			}
			else if (!RowsAreEqual(value2, xElement3))
			{
				list2.Add(xElement3);
			}
			else
			{
				num++;
			}
		}
		int num2 = 0;
		List<XElement> list3 = new List<XElement>();
		foreach (KeyValuePair<string, XElement> item2 in dictionary)
		{
			item2.Deconstruct(out key, out value);
			string key3 = key;
			XElement xElement4 = value;
			if (dictionary2.ContainsKey(key3))
			{
				continue;
			}
			num2++;
			if (!supportDeleteMarking)
			{
				continue;
			}
			XElement xElement5 = new XElement("row");
			xElement5.SetAttributeValue("action", "delete");
			foreach (string pkColumn in pkColumns)
			{
				string text = xElement4.Attribute(pkColumn)?.Value;
				if (text != null)
				{
					xElement5.SetAttributeValue(pkColumn, text);
				}
			}
			list3.Add(xElement5);
		}
		if (num2 > 0 && !supportDeleteMarking)
		{
			Log.Information("[PTF-GEN] {FileName}: {DeleteCount} deletion(s) detected, falling back to full XML (KCD1 PTF cannot express deletions)", fileName, num2);
			return new PtfResult
			{
				RequiresFullXmlFallback = true,
				AddedRows = list.Count,
				ModifiedRows = list2.Count,
				DeletedRows = num2,
				UnchangedRows = num
			};
		}
		if (list.Count == 0 && list2.Count == 0 && list3.Count == 0)
		{
			Log.Information("[PTF-GEN] {FileName}: No changes vs vanilla, skipping", fileName);
			return new PtfResult
			{
				PtfDoc = null,
				RequiresFullXmlFallback = false,
				UnchangedRows = num
			};
		}
		XDocument ptfDoc = BuildPtfDocument(vanillaDoc, mergedDoc, list, list2, list3, fileName);
		Log.Information("[PTF-GEN] {FileName}: Generated PTF with +{Added} added, ~{Modified} modified, -{Deleted} deleted, ={Unchanged} unchanged rows", fileName, list.Count, list2.Count, list3.Count, num);
		return new PtfResult
		{
			PtfDoc = ptfDoc,
			RequiresFullXmlFallback = false,
			AddedRows = list.Count,
			ModifiedRows = list2.Count,
			DeletedRows = list3.Count,
			UnchangedRows = num
		};
	}

	private XDocument BuildPtfDocument(XDocument vanillaDoc, XDocument mergedDoc, List<XElement> addedRows, List<XElement> modifiedRows, List<XElement> deletedRows, string fileName)
	{
		XDocument xDocument = new XDocument(new XDeclaration("1.0", "us-ascii", null));
		XElement root = vanillaDoc.Root;
		XElement xElement = new XElement(root.Name);
		foreach (XAttribute item in root.Attributes())
		{
			xElement.Add(new XAttribute(item));
		}
		XElement xElement2 = root.Element("table");
		if (xElement2 != null)
		{
			XElement xElement3 = new XElement("table");
			foreach (XAttribute item2 in xElement2.Attributes())
			{
				if (item2.Name.LocalName == "name")
				{
					xElement3.Add(new XAttribute("name", item2.Value + "__KCDMerge"));
				}
				else
				{
					xElement3.Add(new XAttribute(item2));
				}
			}
			XElement xElement4 = xElement2.Element("header");
			if (xElement4 != null)
			{
				xElement3.Add(new XElement(xElement4));
			}
			XElement xElement5 = new XElement("rows");
			foreach (XElement addedRow in addedRows)
			{
				xElement5.Add(new XElement(addedRow));
			}
			foreach (XElement modifiedRow in modifiedRows)
			{
				xElement5.Add(new XElement(modifiedRow));
			}
			foreach (XElement deletedRow in deletedRows)
			{
				xElement5.Add(new XElement(deletedRow));
			}
			xElement3.Add(xElement5);
			xElement.Add(xElement3);
		}
		xDocument.Add(xElement);
		return xDocument;
	}

	private Dictionary<string, XElement> BuildRowIndex(XElement rowsElement, List<string> pkColumns)
	{
		Dictionary<string, XElement> dictionary = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
		foreach (XElement item in rowsElement.Elements("row"))
		{
			string key = BuildRowKey(item, pkColumns);
			dictionary.TryAdd(key, item);
		}
		return dictionary;
	}

	private string BuildRowKey(XElement row, List<string> pkColumns)
	{
		List<string> list = new List<string>(pkColumns.Count);
		foreach (string pkColumn in pkColumns)
		{
			string text = row.Attribute(pkColumn)?.Value ?? "";
			list.Add(pkColumn + "=" + text.ToLowerInvariant());
		}
		return string.Join("|", list);
	}

	private bool RowsAreEqual(XElement row1, XElement row2)
	{
		List<XAttribute> list = (from a in row1.Attributes()
			where a.Name.LocalName != "action"
			select a).OrderBy<XAttribute, string>((XAttribute a) => a.Name.LocalName, StringComparer.OrdinalIgnoreCase).ToList();
		List<XAttribute> list2 = (from a in row2.Attributes()
			where a.Name.LocalName != "action"
			select a).OrderBy<XAttribute, string>((XAttribute a) => a.Name.LocalName, StringComparer.OrdinalIgnoreCase).ToList();
		if (list.Count != list2.Count)
		{
			return false;
		}
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].Name != list2[i].Name || list[i].Value != list2[i].Value)
			{
				return false;
			}
		}
		return true;
	}
}
