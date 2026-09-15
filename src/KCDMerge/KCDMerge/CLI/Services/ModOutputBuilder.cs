using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KCDMerge.Core.Configuration;
using KCDMerge.Core.Services;
using Serilog;

namespace KCDMerge.CLI.Services;

public class ModOutputBuilder
{
	private readonly PakCreator _pakCreator;

	private readonly ModManifestGenerator _manifestGenerator;

	private readonly ModOrderManager _modOrderManager;

	public ModOutputBuilder(PakCreator pakCreator, IBackupService backupService)
	{
		_pakCreator = pakCreator;
		_manifestGenerator = new ModManifestGenerator();
		_modOrderManager = new ModOrderManager(backupService);
	}

	public async Task BuildModOutputAsync(AppConfig config, string stagingPath, IModFileTracker modFileTracker, IReadOnlySet<string>? ptfOutputFiles = null, IReadOnlySet<string>? noVanillaFiles = null)
	{
		string outputPath = config.OutputPath;
		string modFolderPath = Path.Combine(config.ModsPath, outputPath);
		string text = Path.Combine(modFolderPath, "Data");
		string localizationFolderPath = Path.Combine(modFolderPath, "Localization");
		string localizationStagingPath = Path.Combine(config.GetResolvedTempPath(), "Staging_localization");
		Log.Information("\u001b[33mCreating mod folder structure...\u001b[0m");
		if (!Directory.Exists(text))
		{
			Directory.CreateDirectory(text);
		}
		_manifestGenerator.GenerateManifest(modFolderPath, config.GamePath, outputPath);
		_modOrderManager.UpdateModOrder(config.ModsPath, outputPath);
		CreateTblFiles(stagingPath, modFileTracker, ptfOutputFiles, noVanillaFiles);
		Log.Information("\u001b[33mCreating PAK archives...\u001b[0m");
		await _pakCreator.CreateDataPaksAsync(stagingPath, text, outputPath);
		if (Directory.Exists(localizationStagingPath))
		{
			string[] directories = Directory.GetDirectories(localizationStagingPath);
			foreach (string text2 in directories)
			{
				string languageCode = Path.GetFileName(text2);
				string outputPakPath = Path.Combine(localizationFolderPath, languageCode + "_xml.pak");
				if (!Directory.Exists(localizationFolderPath))
				{
					Directory.CreateDirectory(localizationFolderPath);
				}
				await _pakCreator.CreatePakFromFolderAsync(text2, outputPakPath);
				Log.Information("Created Localization PAK: {Language}", languageCode + "_xml.pak");
			}
		}
		Log.Information("\u001b[32mSuccess! Mod created at: {ModPath}\u001b[0m", modFolderPath);
	}

	private void CreateTblFiles(string stagingPath, IModFileTracker modFileTracker, IReadOnlySet<string>? ptfOutputFiles, IReadOnlySet<string>? noVanillaFiles = null)
	{
		Log.Debug("Creating TBL override files...");
		IReadOnlyList<string> allTrackedFiles = modFileTracker.GetAllTrackedFiles();
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		foreach (string item in allTrackedFiles)
		{
			if (!item.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			ModFileSource modFileSource = modFileTracker.GetModSourcesForFile(item).FirstOrDefault();
			if ((object)modFileSource != null && modFileSource.IsLocalization)
			{
				continue;
			}
			string text = modFileSource?.BaseFileName ?? item;
			string normalized = text.Replace('\\', '/');
			if (!normalized.StartsWith("Libs/Tables/", StringComparison.OrdinalIgnoreCase))
			{
				Log.Debug("Skipped TBL for non-table XML: {FileName}", text);
				continue;
			}
			if (ptfOutputFiles != null && ptfOutputFiles.Contains(text))
			{
				num2++;
				Log.Debug("Skipped TBL for PTF output: {FileName}", text);
				continue;
			}
			if (noVanillaFiles != null && noVanillaFiles.Contains(text))
			{
				num3++;
				Log.Debug("Skipped TBL for new file (no vanilla baseline): {FileName}", text);
				continue;
			}
			string text2 = Path.ChangeExtension(Path.Combine(stagingPath, text), ".tbl");
			string directoryName = Path.GetDirectoryName(text2);
			if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			File.WriteAllBytes(text2, new byte[0]);
			num++;
			Log.Debug("Created zero-byte TBL: {TblPath}", text2);
		}
		Log.Information("Created {Count} TBL override files (skipped {PtfSkipCount} PTF files, {NoVanillaSkipCount} new files)", num, num2, num3);
	}
}
