using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KCDMerge.Core.Configuration;
using KCDMerge.Core.Models;
using KCDMerge.Core.Services;
using Serilog;

namespace KCDMerge.Core.Data;

public class CfgMerger : ICfgMerger
{
	private readonly IBackupService _backupService;

	private readonly IModConflictRulesService _rulesService;

	private readonly IConflictResolver? _conflictResolver;

	public CfgMerger(IBackupService backupService, IModConflictRulesService rulesService, IConflictResolver? conflictResolver)
	{
		_backupService = backupService ?? throw new ArgumentNullException("backupService");
		_rulesService = rulesService ?? throw new ArgumentNullException("rulesService");
		_conflictResolver = conflictResolver;
	}

	public async Task<int> MergeCfgFilesAsync(string gamePath, IReadOnlyList<ModInfo> mods, MergeReport report)
	{
		List<ModInfo> modsWithCfg = (from m in mods
			where m.CfgFiles.Any()
			orderby m.LoadPriority
			select m).ToList();
		if (!modsWithCfg.Any())
		{
			Log.Debug("[CFG] No mods with .cfg files found");
			return 0;
		}
		Log.Information("[CFG] Found {ModCount} mod(s) with .cfg files", modsWithCfg.Count);
		string userCfgPath = Path.Combine(gamePath, "user.cfg");
		_backupService.BackupFile(userCfgPath);
		CfgFile mergedCfg = CfgParser.LoadFromFile(userCfgPath);
		Log.Information("[CFG] Loaded user.cfg with {Count} existing variable(s)", mergedCfg.Entries.Count);
		Dictionary<string, (string ModName, string Value)> variableOwners = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
		foreach (CfgEntry entry in mergedCfg.Entries)
		{
			variableOwners[entry.Variable] = ("user.cfg", entry.Value);
		}
		int totalMerged = 0;
		Dictionary<string, (int Additions, int Changes)> modStats = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
		foreach (ModInfo mod in modsWithCfg)
		{
			if (!modStats.ContainsKey(mod.DisplayName))
			{
				modStats[mod.DisplayName] = (0, 0);
			}
			foreach (string cfgFile2 in mod.CfgFiles)
			{
				Log.Information("[CFG] Processing {CfgFile} from {ModName}", Path.GetFileName(cfgFile2), mod.DisplayName);
				CfgFile cfgFile;
				try
				{
					cfgFile = CfgParser.LoadFromFile(cfgFile2);
				}
				catch (Exception exception)
				{
					Log.Warning(exception, "[CFG] Failed to parse {CfgFile} from {ModName}, skipping", Path.GetFileName(cfgFile2), mod.DisplayName);
					continue;
				}
				foreach (CfgEntry modEntry in cfgFile.Entries)
				{
					CfgEntry existing = mergedCfg.FindEntry(modEntry.Variable);
					if (existing == null)
					{
						CfgEntry item = new CfgEntry
						{
							Variable = modEntry.Variable,
							Value = modEntry.Value,
							Comments = new List<string>(modEntry.Comments),
							SourceMod = mod.DisplayName
						};
						mergedCfg.Entries.Add(item);
						variableOwners[modEntry.Variable] = (mod.DisplayName, modEntry.Value);
						totalMerged++;
						(int, int) tuple = modStats[mod.DisplayName];
						modStats[mod.DisplayName] = (tuple.Item1 + 1, tuple.Item2);
						Log.Debug("[CFG] Added {Variable} = {Value} from {ModName}", modEntry.Variable, modEntry.Value, mod.DisplayName);
						continue;
					}
					if (string.Equals(existing.Value, modEntry.Value, StringComparison.Ordinal))
					{
						existing.SourceMod = mod.DisplayName;
						variableOwners[modEntry.Variable] = (mod.DisplayName, modEntry.Value);
						Log.Debug("[CFG] Skipped {Variable} from {ModName} (same value: {Value})", modEntry.Variable, mod.DisplayName, modEntry.Value);
						continue;
					}
					object obj;
					if (variableOwners.TryGetValue(modEntry.Variable, out (string, string) value))
					{
						(obj, _) = value;
					}
					else
					{
						obj = "user.cfg";
					}
					string previousOwner = (string)obj;
					Log.Warning("[CFG-CONFLICT] {Variable}: {PreviousOwner} ({OldValue}) vs {ModName} ({NewValue})", modEntry.Variable, previousOwner, existing.Value, mod.DisplayName, modEntry.Value);
					if (await ResolveCfgConflictAsync(modEntry.Variable, existing.Value, previousOwner, modEntry.Value, mod.DisplayName) == mod.DisplayName)
					{
						existing.Value = modEntry.Value;
						existing.SourceMod = mod.DisplayName;
						if (modEntry.Comments.Any())
						{
							existing.Comments = new List<string>(modEntry.Comments);
						}
						variableOwners[modEntry.Variable] = (mod.DisplayName, modEntry.Value);
						totalMerged++;
						(int, int) tuple3 = modStats[mod.DisplayName];
						modStats[mod.DisplayName] = (tuple3.Item1, tuple3.Item2 + 1);
						Log.Information("[CFG-CONFLICT] {ModName} wins: {Variable} = {Value}", mod.DisplayName, modEntry.Variable, modEntry.Value);
					}
					else
					{
						if (modStats.ContainsKey(previousOwner))
						{
							(int, int) tuple4 = modStats[previousOwner];
							modStats[previousOwner] = (tuple4.Item1, tuple4.Item2 + 1);
						}
						Log.Information("[CFG-CONFLICT] {PreviousOwner} wins: {Variable} = {Value}", previousOwner, modEntry.Variable, existing.Value);
					}
				}
			}
		}
		foreach (ModInfo item2 in modsWithCfg)
		{
			int num;
			int num2;
			if (modStats.TryGetValue(item2.DisplayName, out (int, int) value2))
			{
				(num, num2) = value2;
			}
			else
			{
				num = 0;
				num2 = 0;
			}
			if (num > 0 || num2 > 0)
			{
				report.RecordFileStats("(CFG)", item2.DisplayName, item2.LoadPriority, isPatchFile: false, isIdOnlyTable: false, isStandardTable: false, num, 0, num2, 0, 0);
			}
		}
		CfgParser.SaveToFile(mergedCfg, userCfgPath);
		Log.Information("[CFG] Saved merged user.cfg with {Count} variable(s) ({Merged} from mods)", mergedCfg.Entries.Count, totalMerged);
		return totalMerged;
	}

	private async Task<string> ResolveCfgConflictAsync(string variableName, string valueA, string modA, string valueB, string modB)
	{
		string cfgConflictWinner = _rulesService.GetCfgConflictWinner(variableName, modA, modB);
		if (cfgConflictWinner != null)
		{
			Log.Debug("[CFG-CONFLICT] Using saved decision for {Variable}: {Winner} wins", variableName, cfgConflictWinner);
			return cfgConflictWinner;
		}
		if (_conflictResolver != null)
		{
			string conflictSummary = $"user.cfg variable conflict: {variableName}\n  {modA}: {variableName} = {valueA}\n  {modB}: {variableName} = {valueB}";
			string text = await _conflictResolver.ResolveModConflictAsync(modA, modB, conflictSummary);
			string loser = ((text == modA) ? modB : modA);
			_rulesService.SetCfgConflictWinner(variableName, text, loser);
			return text;
		}
		Log.Warning("[CFG-CONFLICT] No conflict resolver available, keeping {ModA}'s value for {Variable}", modA, variableName);
		return modA;
	}
}
