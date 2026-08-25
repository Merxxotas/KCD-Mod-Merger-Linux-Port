using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KCDMerge.Core.Configuration;
using KCDMerge.Core.Services;
using Serilog;

namespace KCDMerge.Core.Data;

public class AssetMerger : IAssetMerger
{
	private readonly IConfigurationService _configService;

	private readonly IPakManager _pakManager;

	private readonly IPreferenceService _preferenceService;

	private readonly IConflictResolver _conflictResolver;

	private readonly string _stagingPath;

	private readonly Dictionary<string, string> _assetOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	public AssetMerger(IConfigurationService configService, IPakManager pakManager, IPreferenceService preferenceService, IConflictResolver conflictResolver)
	{
		_configService = configService;
		_pakManager = pakManager;
		_preferenceService = preferenceService;
		_conflictResolver = conflictResolver;
		string path = _configService.LoadConfiguration().GetResolvedTempPath();
		_stagingPath = Path.Combine(path, "Staging");
	}

	public async Task MergeAssetAsync(string pakPath, string entryName, string sourceMod, int priority, MergeReport report)
	{
		string targetPath = Path.Combine(_stagingPath, entryName);
		if (_assetOwners.TryGetValue(entryName, out string existingOwner))
		{
			Log.Warning("[ASSET-CONFLICT] {EntryName}: {ExistingOwner} vs {SourceMod}", entryName, existingOwner, sourceMod);
			_ = existingOwner;
			string preferredMod = _preferenceService.GetPreferredMod(sourceMod, existingOwner);
			string text;
			if (preferredMod != null)
			{
				text = preferredMod;
				Log.Debug("[ASSET-CONFLICT] Using saved preference: {Winner} wins", text);
			}
			else
			{
				string conflictSummary = "Asset conflict: " + entryName;
				text = await _conflictResolver.ResolveModConflictAsync(existingOwner, sourceMod, conflictSummary);
				_preferenceService.SetPreference(text, (text == existingOwner) ? sourceMod : existingOwner);
				Log.Information("[ASSET-CONFLICT] User chose: {Winner} wins", text);
			}
			if (text == sourceMod)
			{
				await _pakManager.ExtractFileAsync(pakPath, entryName, targetPath);
				_assetOwners[entryName] = sourceMod;
				report.RecordFileStats("(Assets)", sourceMod, priority, isPatchFile: false, isIdOnlyTable: false, isStandardTable: false, 0, 0, 1, 0, 0);
				Log.Debug("[ASSET] {SourceMod} overwrote {EntryName}", sourceMod, entryName);
			}
			else
			{
				Log.Debug("[ASSET] {ExistingOwner} kept {EntryName}, skipping {SourceMod}", existingOwner, entryName, sourceMod);
			}
		}
		else
		{
			await _pakManager.ExtractFileAsync(pakPath, entryName, targetPath);
			_assetOwners[entryName] = sourceMod;
			report.RecordFileStats("(Assets)", sourceMod, priority, isPatchFile: false, isIdOnlyTable: false, isStandardTable: false, 1, 0, 0, 0, 0);
			Log.Debug("[ASSET] {SourceMod} added {EntryName}", sourceMod, entryName);
		}
	}
}
