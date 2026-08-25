using System;
using System.IO;
using System.Linq;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KCDMerge.Core.Configuration;

public class ModConflictRulesService : IModConflictRulesService
{
	private readonly string _rulesPath;

	private readonly ISerializer _serializer;

	private readonly IDeserializer _deserializer;

	public ModConflictRulesService(string rulesPath = "ModConflictRules.yaml")
	{
		_rulesPath = Path.GetFullPath(rulesPath);
		_serializer = new SerializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance).Build();
		_deserializer = new DeserializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();
	}

	public ModConflictRules LoadRules()
	{
		if (!File.Exists(_rulesPath))
		{
			return new ModConflictRules();
		}
		try
		{
			string input = File.ReadAllText(_rulesPath);
			return _deserializer.Deserialize<ModConflictRules>(input) ?? new ModConflictRules();
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Failed to load ModConflictRules.yaml, using empty rules");
			return new ModConflictRules();
		}
	}

	public void SaveRules(ModConflictRules rules)
	{
		try
		{
			string contents = _serializer.Serialize(rules);
			File.WriteAllText(_rulesPath, contents);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Failed to save ModConflictRules.yaml");
		}
	}

	public string? GetXmlConflictWinner(string modA, string modB)
	{
		ModConflictRules modConflictRules = LoadRules();
		string key = GetKey(modA, modB);
		if (modConflictRules.XmlConflicts.TryGetValue(key, out string value))
		{
			return value;
		}
		return null;
	}

	public void SetXmlConflictWinner(string winner, string loser)
	{
		ModConflictRules modConflictRules = LoadRules();
		string key = GetKey(winner, loser);
		modConflictRules.XmlConflicts[key] = winner;
		SaveRules(modConflictRules);
	}

	public string? GetAssetConflictWinner(string modA, string modB)
	{
		ModConflictRules modConflictRules = LoadRules();
		string key = GetKey(modA, modB);
		if (modConflictRules.AssetConflicts.TryGetValue(key, out string value))
		{
			return value;
		}
		return null;
	}

	public void SetAssetConflictWinner(string winner, string loser)
	{
		ModConflictRules modConflictRules = LoadRules();
		string key = GetKey(winner, loser);
		modConflictRules.AssetConflicts[key] = winner;
		SaveRules(modConflictRules);
	}

	public string? GetRowAdditionStrategy(string tableName, string modName)
	{
		ModConflictRules modConflictRules = LoadRules();
		string key = tableName + "|" + modName;
		if (modConflictRules.RowAdditionRules.TryGetValue(key, out string value))
		{
			return value;
		}
		return null;
	}

	public void SetRowAdditionStrategy(string tableName, string modName, string strategy)
	{
		ModConflictRules modConflictRules = LoadRules();
		string key = tableName + "|" + modName;
		modConflictRules.RowAdditionRules[key] = strategy;
		SaveRules(modConflictRules);
	}

	public string? GetCfgConflictWinner(string variableName, string modA, string modB)
	{
		ModConflictRules modConflictRules = LoadRules();
		string cfgKey = GetCfgKey(variableName, modA, modB);
		if (modConflictRules.CfgConflicts.TryGetValue(cfgKey, out string value))
		{
			return value;
		}
		return null;
	}

	public void SetCfgConflictWinner(string variableName, string winner, string loser)
	{
		ModConflictRules modConflictRules = LoadRules();
		string cfgKey = GetCfgKey(variableName, winner, loser);
		modConflictRules.CfgConflicts[cfgKey] = winner;
		SaveRules(modConflictRules);
	}

	public string? GetStalenessOverride(string fileName, string modName)
	{
		ModConflictRules modConflictRules = LoadRules();
		string key = fileName + "|" + modName;
		if (!modConflictRules.StalenessOverrides.TryGetValue(key, out string value))
		{
			return null;
		}
		return value;
	}

	public void SetStalenessOverride(string fileName, string modName, string decision)
	{
		ModConflictRules modConflictRules = LoadRules();
		string key = fileName + "|" + modName;
		modConflictRules.StalenessOverrides[key] = decision;
		SaveRules(modConflictRules);
	}

	private string GetKey(string modA, string modB)
	{
		string[] array = new string[2] { modA, modB }.OrderBy((string s) => s).ToArray();
		return array[0] + "|" + array[1];
	}

	private string GetCfgKey(string variableName, string modA, string modB)
	{
		string[] array = new string[2] { modA, modB }.OrderBy((string s) => s).ToArray();
		return $"{variableName}|{array[0]}|{array[1]}";
	}
}
