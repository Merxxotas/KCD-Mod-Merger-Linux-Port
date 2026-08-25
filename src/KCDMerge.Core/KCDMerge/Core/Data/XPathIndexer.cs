using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using KCDMerge.Core.Models;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KCDMerge.Core.Data;

public class XPathIndexer : IXPathIndexer
{
	private readonly string _pkSchemaCacheFile;

	private readonly string _currentVersion;

	private PkSchemaCache _pkSchemaCache = new PkSchemaCache();

	private readonly HashSet<string> _loggedPkSchemas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public XPathIndexer()
	{
		_pkSchemaCacheFile = Path.Combine(".temp", "pk_schema_cache.yaml");
		_currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
		LoadPkSchemaCache();
	}

	private void LoadPkSchemaCache()
	{
		if (!File.Exists(_pkSchemaCacheFile))
		{
			_pkSchemaCache = new PkSchemaCache
			{
				CacheVersion = _currentVersion
			};
			return;
		}
		try
		{
			string input = File.ReadAllText(_pkSchemaCacheFile);
			IDeserializer deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
			_pkSchemaCache = deserializer.Deserialize<PkSchemaCache>(input);
			if (_pkSchemaCache.CacheVersion != _currentVersion)
			{
				Log.Information("PK schema cache version mismatch (cached: {CachedVersion}, current: {CurrentVersion}). Invalidating cache.", _pkSchemaCache.CacheVersion, _currentVersion);
				_pkSchemaCache = new PkSchemaCache
				{
					CacheVersion = _currentVersion
				};
			}
		}
		catch
		{
			_pkSchemaCache = new PkSchemaCache
			{
				CacheVersion = _currentVersion
			};
		}
	}

	private void SavePkSchemaCache()
	{
		try
		{
			_pkSchemaCache.CacheVersion = _currentVersion;
			Directory.CreateDirectory(Path.GetDirectoryName(_pkSchemaCacheFile) ?? ".temp");
			string contents = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build().Serialize(_pkSchemaCache);
			File.WriteAllText(_pkSchemaCacheFile, contents);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Failed to save PK schema cache");
		}
	}

	public List<string>? DeriveTablePkSchema(XDocument doc, string fileName)
	{
		string path = fileName.Split(new string[1] { " (" }, StringSplitOptions.None)[0];
		string text = Path.GetFileNameWithoutExtension(Path.GetFileName(path));
		int num = text.IndexOf("__");
		if (num > 0)
		{
			text = text.Substring(0, num);
		}
		if (_pkSchemaCache.Tables.TryGetValue(text, out TableMetadata value))
		{
			if (_loggedPkSchemas.Add(text))
			{
				Log.Debug("PK schema for {TableName} loaded from cache: [{PkColumns}], ID-only: {IsIdOnly}", text, string.Join(", ", value.PkColumns), value.HasOnlyPkColumns);
			}
			return value.PkColumns;
		}
		XElement xElement = doc.Root?.Descendants("header").FirstOrDefault();
		if (xElement == null)
		{
			Log.Debug("No <header> found in {FileName}, skipping PK derivation", fileName);
			return null;
		}
		List<string> list = (from r in xElement.Elements("column")
			select r.Attribute("name")?.Value into name
			where name != null && (name.EndsWith("_id", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_key", StringComparison.OrdinalIgnoreCase))
			select name).ToList();
		if (list.Count == 0)
		{
			Log.Debug("No _id/_key columns found in {FileName} header", fileName);
			return null;
		}
		List<string> list2 = (from r in xElement.Elements("column")
			select r.Attribute("name")?.Value into name
			where name != null
			select name).ToList();
		bool flag = list2.Count > 0 && list2.All((string col) => col.EndsWith("_id", StringComparison.OrdinalIgnoreCase) || col.EndsWith("_key", StringComparison.OrdinalIgnoreCase));
		Log.Debug("Found {Count} _id/_key columns in {FileName}: [{Columns}], ID-only: {IsIdOnly}", list.Count, fileName, string.Join(", ", list), flag);
		List<string> list3 = new List<string>();
		foreach (string item in list)
		{
			string text3;
			if (item.EndsWith("_id", StringComparison.OrdinalIgnoreCase))
			{
				string text2 = item;
				int length = "_id".Length;
				text3 = text2.Substring(0, text2.Length - length);
			}
			else
			{
				if (!item.EndsWith("_key", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				string text2 = item;
				int length = "_key".Length;
				text3 = text2.Substring(0, text2.Length - length);
			}
			if (text.Contains(text3, StringComparison.OrdinalIgnoreCase))
			{
				list3.Add(item);
				Log.Debug("  ✓ {ColName} matches filename (stripped: {Stripped})", item, text3);
			}
			else
			{
				Log.Debug("  ✗ {ColName} does NOT match filename (stripped: {Stripped})", item, text3);
			}
		}
		if (list3.Count == 0)
		{
			string fileName2 = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
			if (!string.IsNullOrEmpty(fileName2))
			{
				string parentPkName = fileName2 + "_id";
				string text4 = list.FirstOrDefault((string c) => string.Equals(c, parentPkName, StringComparison.OrdinalIgnoreCase));
				if (text4 != null)
				{
					list3.Add(text4);
					Log.Debug("  ✓ {ColName} matches parent directory fallback (dir: {ParentDir})", text4, fileName2);
				}
			}
		}
		if (list3.Count == 0)
		{
			Log.Debug("No _id columns matched filename or directory pattern for {FileName}", fileName);
			return null;
		}
		_pkSchemaCache.Tables[text] = new TableMetadata
		{
			PkColumns = list3,
			HasOnlyPkColumns = flag
		};
		SavePkSchemaCache();
		Log.Information("Derived PK schema for {TableName}: [{PkColumns}], ID-only: {IsIdOnly}", text, string.Join(", ", list3), flag);
		return list3;
	}

	public bool IsIdOnlyTable(string fileName)
	{
		string text = Path.GetFileNameWithoutExtension(Path.GetFileName(fileName.Split(new string[1] { " (" }, StringSplitOptions.None)[0]));
		int num = text.IndexOf("__");
		if (num > 0)
		{
			text = text.Substring(0, num);
		}
		if (_pkSchemaCache.Tables.TryGetValue(text, out TableMetadata value))
		{
			return value.HasOnlyPkColumns;
		}
		return false;
	}

	public bool ValidatePkUniqueness(XDocument doc, List<string> pkColumns)
	{
		XElement xElement = doc.Root?.Descendants("rows").FirstOrDefault();
		if (xElement == null)
		{
			return true;
		}
		int num = xElement.Elements("row").Count();
		Log.Verbose("[PK-VALIDATE] Checking {RowCount} rows with PK columns: [{PkColumns}]", num, string.Join(", ", pkColumns));
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (XElement row in xElement.Elements("row"))
		{
			string text = string.Join("|", pkColumns.Select((string pk) => pk + "=" + row.Attribute(pk)?.Value?.ToLowerInvariant()));
			if (!hashSet.Add(text))
			{
				Log.Information("[PK-VALIDATE] PK non-unique for [{PkColumns}]: duplicate key '{Key}' found (table has {RowCount} rows)", string.Join(", ", pkColumns), text, num);
				return false;
			}
		}
		Log.Verbose("[PK-VALIDATE] PK uniqueness validated: {UniqueKeys} unique keys for {RowCount} rows", hashSet.Count, num);
		return true;
	}

	public bool HasTableStructure(XDocument doc)
	{
		XElement? obj = doc.Root?.Descendants("header").FirstOrDefault();
		XElement xElement = doc.Root?.Descendants("rows").FirstOrDefault();
		IEnumerable<XElement> enumerable = obj?.Elements("column");
		if (obj != null && xElement != null && enumerable != null)
		{
			return enumerable.Any();
		}
		return false;
	}

	public Dictionary<string, XElement> BuildIndex(XDocument doc, string fileName)
	{
		Dictionary<string, XElement> dictionary = new Dictionary<string, XElement>();
		if (doc.Root == null)
		{
			Log.Warning("XPathIndexer: Document has no root element for {FileName}", fileName);
			return dictionary;
		}
		List<string> pkColumns = DeriveTablePkSchema(doc, fileName);
		string currentPath = "/" + doc.Root.Name.LocalName;
		TraverseAndIndex(doc.Root, currentPath, dictionary, fileName, pkColumns);
		return dictionary;
	}

	private void TraverseAndIndex(XElement element, string currentPath, Dictionary<string, XElement> index, string fileName, List<string>? pkColumns)
	{
		if (!index.ContainsKey(currentPath))
		{
			index[currentPath] = element;
		}
		else
		{
			Log.Warning("XPathIndexer: Duplicate path detected: {Path} in {FileName}", currentPath, fileName);
		}
		List<XElement> list = element.Elements().ToList();
		List<string> list2 = new List<string>(list.Count);
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		Dictionary<string, int> siblingCounts = new Dictionary<string, int>();
		foreach (XElement item in list)
		{
			string localName = item.Name.LocalName;
			string text = BuildPathSegment(item, localName, siblingCounts, fileName, pkColumns);
			list2.Add(text);
			if (!dictionary.ContainsKey(text))
			{
				dictionary[text] = 0;
			}
			dictionary[text]++;
		}
		Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
		for (int i = 0; i < list.Count; i++)
		{
			string text2 = list2[i];
			string currentPath2;
			if (dictionary[text2] > 1)
			{
				if (!dictionary2.ContainsKey(text2))
				{
					dictionary2[text2] = 0;
				}
				int num = dictionary2[text2];
				dictionary2[text2]++;
				currentPath2 = currentPath + text2 + $"[{num}]";
				Log.Debug("XPathIndexer: Disambiguated duplicate segment {Segment} -> [{Position}] in {FileName}", text2, num, fileName);
			}
			else
			{
				currentPath2 = currentPath + text2;
			}
			TraverseAndIndex(list[i], currentPath2, index, fileName, pkColumns);
		}
	}

	private string BuildPathSegment(XElement element, string elementName, Dictionary<string, int> siblingCounts, string fileName, List<string>? pkColumns)
	{
		if (pkColumns != null && pkColumns.Count > 0 && elementName == "row")
		{
			List<string> list = new List<string>();
			foreach (string pkColumn in pkColumns)
			{
				string text = element.Attribute(pkColumn)?.Value;
				if (text != null)
				{
					string value = text.ToLowerInvariant();
					list.Add($"@{pkColumn}='{EscapeXPathValue(value)}'");
				}
			}
			if (list.Count > 0)
			{
				return $"/{elementName}[{string.Join(" and ", list)}]";
			}
			Log.Warning("XPathIndexer: PK columns defined ({PkColumns}) but no matching attributes found on row element in {FileName}", string.Join(", ", pkColumns), fileName);
		}
		XAttribute xAttribute = element.Attribute("Id");
		if (xAttribute != null)
		{
			return $"/{elementName}[@Id='{EscapeXPathValue(xAttribute.Value)}']";
		}
		XAttribute xAttribute2 = element.Attribute("name");
		if (xAttribute2 != null)
		{
			string text2 = xAttribute2.Value;
			if (elementName == "table")
			{
				int num = text2.IndexOf("__");
				if (num > 0)
				{
					text2 = text2.Substring(0, num);
				}
			}
			return $"/{elementName}[@name='{EscapeXPathValue(text2)}']";
		}
		string text3 = BuildCompositeKey(element, elementName);
		if (text3 != null)
		{
			return text3;
		}
		XAttribute xAttribute3 = element.Attribute("command");
		if (xAttribute3 != null)
		{
			return $"/{elementName}[@command='{EscapeXPathValue(xAttribute3.Value)}']";
		}
		if (!siblingCounts.ContainsKey(elementName))
		{
			siblingCounts[elementName] = 0;
		}
		int num2 = siblingCounts[elementName];
		siblingCounts[elementName]++;
		if (num2 <= 0)
		{
			XElement? parent = element.Parent;
			if (parent == null || parent.Elements(element.Name).Count() <= 1)
			{
				goto IL_0324;
			}
		}
		Log.Debug("XPathIndexer: Using position-based index for {ElementName}[{Position}] in {FileName} (no unique identifier found)", elementName, num2, fileName);
		goto IL_0324;
		IL_0324:
		return $"/{elementName}[{num2}]";
	}

	private string? BuildCompositeKey(XElement element, string elementName)
	{
		if (elementName == "conflict" && element.Attribute("name") == null)
		{
			List<string> list = (from r in element.Elements().Select(delegate(XElement child)
				{
					string localName = child.Name.LocalName;
					string text = child.Attribute("name")?.Value;
					string text2 = child.Attribute("conflict")?.Value;
					if (text != null)
					{
						return localName + "=" + text;
					}
					return (text2 != null) ? ("inc=" + text2) : localName;
				})
				orderby r
				select r).ToList();
			if (list.Count > 0)
			{
				string value = string.Join(",", list);
				return $"/{elementName}[refs='{EscapeXPathValue(value)}']";
			}
		}
		if (elementName == "QuestObjectiveGate")
		{
			string text3 = element.Attribute("questName")?.Value;
			string text4 = element.Attribute("objectiveName")?.Value;
			if (text3 != null && text4 != null)
			{
				return $"/{elementName}[@questName='{EscapeXPathValue(text3)}' and @objectiveName='{EscapeXPathValue(text4)}']";
			}
		}
		if (elementName == "Edge")
		{
			string text5 = element.Attribute("nodeIn")?.Value;
			string text6 = element.Attribute("nodeOut")?.Value;
			string text7 = element.Attribute("portIn")?.Value;
			string text8 = element.Attribute("portOut")?.Value;
			if (text5 != null && text6 != null)
			{
				List<string> list2 = new List<string>
				{
					"@nodeIn='" + EscapeXPathValue(text5) + "'",
					"@nodeOut='" + EscapeXPathValue(text6) + "'"
				};
				if (text7 != null)
				{
					list2.Add("@portIn='" + EscapeXPathValue(text7) + "'");
				}
				if (text8 != null)
				{
					list2.Add("@portOut='" + EscapeXPathValue(text8) + "'");
				}
				return $"/{elementName}[{string.Join(" and ", list2)}]";
			}
		}
		return null;
	}

	private string EscapeXPathValue(string value)
	{
		if (value.Contains("'") && value.Contains("\""))
		{
			List<string> values = (from p in value.Split('\'')
				select "'" + p + "'").ToList();
			return "concat(" + string.Join(", \"'\", ", values) + ")";
		}
		if (value.Contains("'"))
		{
			return value;
		}
		return value.Replace("'", "&apos;");
	}
}
