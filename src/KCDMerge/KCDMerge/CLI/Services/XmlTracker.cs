using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KCDMerge.Core.Models;
using KCDMerge.Core.Services;
using Serilog;
using Spectre.Console;

namespace KCDMerge.CLI.Services;

public class XmlTracker
{
	private readonly IPakManager _pakManager;

	private readonly IModFileTracker _modFileTracker;

	private readonly IVanillaPakIndexer _vanillaIndexer;

	private readonly string _tempBase;

	public XmlTracker(IPakManager pakManager, IModFileTracker modFileTracker, IVanillaPakIndexer vanillaIndexer, string tempBase)
	{
		_pakManager = pakManager;
		_modFileTracker = modFileTracker;
		_vanillaIndexer = vanillaIndexer;
		_tempBase = tempBase;
	}

	public int TrackXmlFiles(IReadOnlyList<ModInfo> mods)
	{
		Log.Information("\u001b[33mProcessing XML files...\u001b[0m");
		int num = 0;
		foreach (ModInfo item in mods.OrderBy((ModInfo m) => m.LoadPriority))
		{
			Log.Information("Processing XML files from {ModName}...", item.DisplayName);
			int num2 = 0;
			foreach (KeyValuePair<string, (string, string, DateTime)> item2 in PakIndexHelper.BuildIntraModFileIndex(item.PakFiles, _pakManager, item.DisplayName, _tempBase))
			{
				string key = item2.Key;
				var (text, text2, entryModifiedDate) = item2.Value;
				try
				{
					if (!key.EndsWith("/") && !key.EndsWith("\\") && key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
					{
						string deepPakPrefix = PakIndexHelper.GetDeepPakPrefix(text, item.RootPath);
						string fileName = ((PakIndexHelper.GetLocalizationPakPrefix(text) != null) ? text2.Replace('\\', '/') : (string.IsNullOrEmpty(deepPakPrefix) ? text2.Replace('\\', '/') : (deepPakPrefix + "/" + text2.Replace('\\', '/'))));
						_modFileTracker.TrackFile(fileName, item.DisplayName, item.LoadPriority, text, text2, entryModifiedDate);
						num2++;
					}
				}
				catch (Exception ex)
				{
					Log.Warning(ex, "Failed to process XML file {Entry} from {PakFile}", key, Path.GetFileName(text));
					AnsiConsole.MarkupLine($"[yellow]Warning: Could not process {key} - {ex.Message}[/]");
				}
			}
			Log.Information("{ModName}: {XmlCount} XML files tracked", item.DisplayName, num2);
			num += num2;
			if (item.LooseFileFolders.Any())
			{
				num += TrackLooseFiles(item);
			}
		}
		Log.Information("Tracked {XmlCount} XML files from {ModCount} mods", num, mods.Count);
		AnsiConsole.WriteLine();
		return num;
	}

	private int TrackLooseFiles(ModInfo mod)
	{
		Log.Information("Processing loose files from {ModName}...", mod.DisplayName);
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		foreach (KeyValuePair<string, List<string>> looseFileFolder in mod.LooseFileFolders)
		{
			string key = looseFileFolder.Key;
			foreach (string item in looseFileFolder.Value)
			{
				string pakPath = Path.Combine(mod.RootPath, key, item);
				List<string> list = _vanillaIndexer.FindByFilename(item);
				string text;
				if (list.Count == 1)
				{
					text = list[0];
					num2++;
					Log.Debug("[LOOSE-FILE] Resolved {FileName} -> {CanonicalPath}", item, text);
				}
				else
				{
					if (list.Count > 1)
					{
						Log.Warning("[LOOSE-FILE] Ambiguous match for {FileName} in {ModName}: found {Count} candidates. Skipping.", item, mod.DisplayName, list.Count);
						num3++;
						continue;
					}
					text = key;
					if (!key.StartsWith("Data/", StringComparison.OrdinalIgnoreCase) && !key.StartsWith("Libs/", StringComparison.OrdinalIgnoreCase) && !key.StartsWith("Levels/", StringComparison.OrdinalIgnoreCase))
					{
						text = "Levels/" + key;
					}
					text = Path.Combine(text, item).Replace('\\', '/');
					Log.Debug("[LOOSE-FILE] New file (not in vanilla): {FileName} -> {CanonicalPath}", item, text);
				}
				_modFileTracker.TrackFile(text, mod.DisplayName, mod.LoadPriority, pakPath, text, DateTime.MinValue);
				num++;
			}
		}
		Log.Information("{ModName}: {LooseCount} loose XML files tracked ({Resolved} resolved, {Skipped} skipped)", mod.DisplayName, num, num2, num3);
		return num;
	}
}
