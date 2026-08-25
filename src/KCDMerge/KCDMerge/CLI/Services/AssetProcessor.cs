using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KCDMerge.Core.Data;
using KCDMerge.Core.Models;
using KCDMerge.Core.Services;
using Serilog;
using Spectre.Console;

namespace KCDMerge.CLI.Services;

public class AssetProcessor
{
	private readonly IPakManager _pakManager;

	private readonly IAssetMerger _assetMerger;

	private readonly IModFileTracker _modFileTracker;

	private readonly string _tempBase;

	public AssetProcessor(IPakManager pakManager, IAssetMerger assetMerger, IModFileTracker modFileTracker, string tempBase)
	{
		_pakManager = pakManager;
		_assetMerger = assetMerger;
		_modFileTracker = modFileTracker;
		_tempBase = tempBase;
	}

	public async Task<int> ProcessAssetsAsync(IReadOnlyList<ModInfo> mods, MergeReport mergeReport)
	{
		Log.Information("\u001b[33mProcessing assets...\u001b[0m");
		int totalAssets = 0;
		AssetXmlCacheService assetXmlCache = new AssetXmlCacheService();
		foreach (ModInfo mod in mods.OrderBy((ModInfo m) => m.LoadPriority))
		{
			int modAssetXmlCount = 0;
			Dictionary<string, (string, string, DateTime)> dictionary = PakIndexHelper.BuildIntraModFileIndex(mod.PakFiles, _pakManager, mod.DisplayName, _tempBase);
			if (mod.PakFiles.Count > 1)
			{
				Log.Information("Processing {ModName} - {PakCount} PAKs (using newest version of each file)", mod.DisplayName, mod.PakFiles.Count);
			}
			else
			{
				Log.Information("Processing {ModName} - {PakFile}", mod.DisplayName, Path.GetFileName(mod.PakFiles.FirstOrDefault() ?? ""));
			}
			foreach (KeyValuePair<string, (string, string, DateTime)> item in dictionary)
			{
				string entry = item.Key;
				var (pakPath, entryName, modifiedDate) = item.Value;
				try
				{
					string deepPakPrefix = PakIndexHelper.GetDeepPakPrefix(pakPath, mod.RootPath);
					if (!entry.EndsWith("/") && !entry.EndsWith("\\") && !entry.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !entry.EndsWith(".tbl", StringComparison.OrdinalIgnoreCase))
					{
						string ext = Path.GetExtension(entry).ToLowerInvariant();
						bool? flag = assetXmlCache.IsExtensionXml(ext);
						if (!flag.HasValue)
						{
							flag = await _pakManager.IsXmlContentAsync(pakPath, entryName);
							assetXmlCache.SetExtensionXml(ext, flag.Value);
							Log.Debug("[ASSET-XML] Detected {Ext} as {Type}", ext, flag.Value ? "XML" : "binary");
						}
						if (flag != true)
						{
							await _assetMerger.MergeAssetAsync(pakPath, entryName, mod.DisplayName, mod.LoadPriority, mergeReport);
							continue;
						}
						string fileName = (string.IsNullOrEmpty(deepPakPrefix) ? entry : (deepPakPrefix + "/" + entry));
						_modFileTracker.TrackFile(fileName, mod.DisplayName, mod.LoadPriority, pakPath, entryName, modifiedDate);
						modAssetXmlCount++;
						Log.Debug("[ASSET-XML] Routing {Entry} to XML pipeline", entry);
					}
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Error processing file {Entry} from {PakFile}", entry, Path.GetFileName(pakPath));
					AnsiConsole.MarkupLine($"[red]Error processing {entry}: {ex.Message}[/]");
				}
			}
			int num = mergeReport.FileStatistics.Values.Where((FileStats s) => s.ModName == mod.DisplayName && s.FileName == "(Assets)").Sum((FileStats s) => s.Additions + s.Modifications);
			if (modAssetXmlCount > 0)
			{
				Log.Information("{ModName}: {AssetCount} assets copied, {AssetXmlCount} asset files detected as XML", mod.DisplayName, num, modAssetXmlCount);
			}
			else
			{
				Log.Information("{ModName}: {AssetCount} assets copied", mod.DisplayName, num);
			}
			totalAssets += num;
		}
		assetXmlCache.SaveCache();
		Log.Information("Copied {AssetCount} assets from {ModCount} mods", totalAssets, mods.Count);
		AnsiConsole.WriteLine();
		return totalAssets;
	}
}
