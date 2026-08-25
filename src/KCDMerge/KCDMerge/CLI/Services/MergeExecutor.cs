using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KCDMerge.Core.Configuration;
using KCDMerge.Core.Data;
using KCDMerge.Core.Services;
using Serilog;
using Spectre.Console;

namespace KCDMerge.CLI.Services;

public class MergeExecutor
{
	private readonly IMergePipeline _mergePipeline;

	private readonly IModFileTracker _modFileTracker;

	private readonly IDeltaCacheService _deltaCacheService;

	private readonly IXmlWriter _xmlWriter;

	private readonly AppConfig _config;

	public MergeExecutor(IMergePipeline mergePipeline, IModFileTracker modFileTracker, IDeltaCacheService deltaCacheService, IXmlWriter xmlWriter, AppConfig config)
	{
		_mergePipeline = mergePipeline;
		_modFileTracker = modFileTracker;
		_deltaCacheService = deltaCacheService;
		_xmlWriter = xmlWriter;
		_config = config;
	}

	public async Task<MergeExecutorResult> ExecuteMergesAsync()
	{
		Log.Information("\u001b[33mMerging XML files...\u001b[0m");
		_deltaCacheService.LoadCache();
		IReadOnlyList<string> allTrackedFiles = _modFileTracker.GetAllTrackedFiles();
		int mergedCount = 0;
		int skippedCount = 0;
		HashSet<string> processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> ptfOutputFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> noVanillaFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		PtfGenerator ptfGenerator = new PtfGenerator();
		bool usePtfOutput = _config.OutputMode.Equals("PTF", StringComparison.OrdinalIgnoreCase);
		Dictionary<string, List<string>> fileGroups = GroupPtfFiles(allTrackedFiles);
		MergeReport mergeReport = _mergePipeline.GetReport();
		foreach (string baseFile in fileGroups.Keys.OrderBy((string f) => f))
		{
			if (processedFiles.Contains(baseFile))
			{
				continue;
			}
			try
			{
				List<string> patchFiles = fileGroups[baseFile];
				List<ModFileSource> allSources = new List<ModFileSource>();
				IReadOnlyList<ModFileSource> modSourcesForFile = _modFileTracker.GetModSourcesForFile(baseFile);
				allSources.AddRange(modSourcesForFile);
				foreach (string item in patchFiles)
				{
					IReadOnlyList<ModFileSource> modSourcesForFile2 = _modFileTracker.GetModSourcesForFile(item);
					allSources.AddRange(modSourcesForFile2);
				}
				if (allSources.Count == 0)
				{
					Log.Warning("No mod sources found for {FileName}", baseFile);
					skippedCount++;
					continue;
				}
				allSources = allSources.OrderBy((ModFileSource s) => s.Priority).ToList();
				string text = string.Join(", ", allSources.Select((ModFileSource s) => s.ModName).Distinct());
				string text2 = ((patchFiles.Count > 0) ? $" + {patchFiles.Count} PTF patch(es)" : "");
				Log.Information("[PIPELINE] {FileName}: Starting merge ({ModCount} mod(s): {ModNames}{PatchNote})", Path.GetFileName(baseFile), allSources.Select((ModFileSource s) => s.ModName).Distinct().Count(), text, text2);
				MergeResult mergeResult = await _mergePipeline.MergeFileAsync(baseFile, allSources);
				if (mergeResult.MergedDoc != null)
				{
					if (mergeResult.VanillaDoc == null)
					{
						noVanillaFiles.Add(baseFile);
						Log.Debug("[PIPELINE] {FileName}: No vanilla baseline - will skip .tbl creation", baseFile);
					}
					LogFileSummary(baseFile, mergeResult, allSources, await WriteOutput(baseFile, mergeResult, allSources, ptfGenerator, usePtfOutput, ptfOutputFiles), mergeReport);
					mergedCount++;
					processedFiles.Add(baseFile);
					foreach (string item2 in patchFiles)
					{
						processedFiles.Add(item2);
					}
				}
				else
				{
					Log.Warning("Merge failed for {FileName}", baseFile);
					skippedCount++;
				}
				foreach (ModFileSource item3 in allSources)
				{
					item3.XmlStream?.Dispose();
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error merging {FileName}", baseFile);
				AnsiConsole.MarkupLine($"[red]Error merging {baseFile}: {ex.Message}[/]");
				skippedCount++;
			}
		}
		Log.Information("Merged {MergedCount} XML files, skipped {SkippedCount}.", mergedCount, skippedCount);
		_deltaCacheService.SaveCache();
		return new MergeExecutorResult
		{
			MergedCount = mergedCount,
			SkippedCount = skippedCount,
			PtfOutputFiles = ptfOutputFiles,
			NoVanillaFiles = noVanillaFiles
		};
	}

	private Dictionary<string, List<string>> GroupPtfFiles(IReadOnlyList<string> trackedFiles)
	{
		Dictionary<string, List<string>> dictionary = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (string trackedFile in trackedFiles)
		{
			ModFileSource modFileSource = _modFileTracker.GetModSourcesForFile(trackedFile).FirstOrDefault();
			if ((object)modFileSource != null && modFileSource.IsPatchFile && !string.IsNullOrEmpty(modFileSource.BaseFileName))
			{
				if (!dictionary.ContainsKey(modFileSource.BaseFileName))
				{
					dictionary[modFileSource.BaseFileName] = new List<string>();
				}
				dictionary[modFileSource.BaseFileName].Add(trackedFile);
				Log.Debug("[PTF] {PatchFile} will be merged into base file {BaseFile}", trackedFile, modFileSource.BaseFileName);
			}
			else if (!dictionary.ContainsKey(trackedFile))
			{
				dictionary[trackedFile] = new List<string>();
			}
		}
		return dictionary;
	}

	private async Task<string> WriteOutput(string baseFile, MergeResult mergeResult, List<ModFileSource> allSources, PtfGenerator ptfGenerator, bool usePtfOutput, HashSet<string> ptfOutputFiles)
	{
		ModFileSource? modFileSource = allSources.FirstOrDefault();
		bool flag = modFileSource?.IsLocalization ?? false;
		string languageCode = modFileSource?.LanguageCode;
		string outputResult = "Full XML + .tbl";
		if (flag)
		{
			string? fileNameWithoutExtension = Path.GetFileNameWithoutExtension(baseFile);
			string extension = Path.GetExtension(baseFile);
			string text = fileNameWithoutExtension + "__KCDMerge" + extension;
			Log.Debug("[LOCALIZATION] Transformed output: {BaseFile} -> {OutputFile}", baseFile, text);
			await _xmlWriter.WriteMergedDocumentAsync(text, mergeResult.MergedDoc, flag, languageCode);
			outputResult = "Localization PTF";
		}
		else if (!usePtfOutput || !baseFile.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || mergeResult.VanillaDoc == null || mergeResult.PkColumns == null || mergeResult.PkColumns.Count <= 0 || !mergeResult.TableType.HasValue || mergeResult.TableType == DeltaType.NonTable || mergeResult.TableType == DeltaType.HashTable)
		{
			await _xmlWriter.WriteMergedDocumentAsync(baseFile, mergeResult.MergedDoc, isLocalization: false, null);
		}
		else
		{
			PtfResult ptfResult = ptfGenerator.GeneratePtf(mergeResult.MergedDoc, mergeResult.VanillaDoc, mergeResult.PkColumns, baseFile);
			if (!ptfResult.RequiresFullXmlFallback && ptfResult.PtfDoc != null)
			{
				string text2 = Path.GetDirectoryName(baseFile) ?? "";
				string fileNameWithoutExtension2 = Path.GetFileNameWithoutExtension(baseFile);
				string extension2 = Path.GetExtension(baseFile);
				string fileName = (string.IsNullOrEmpty(text2) ? (fileNameWithoutExtension2 + "__KCDMerge" + extension2) : Path.Combine(text2, fileNameWithoutExtension2 + "__KCDMerge" + extension2).Replace('\\', '/'));
				await _xmlWriter.WriteMergedDocumentAsync(fileName, ptfResult.PtfDoc, isLocalization: false, null);
				ptfOutputFiles.Add(baseFile);
				outputResult = $"PTF (+{ptfResult.AddedRows} ~{ptfResult.ModifiedRows} rows)";
			}
			else if (ptfResult.RequiresFullXmlFallback)
			{
				await _xmlWriter.WriteMergedDocumentAsync(baseFile, mergeResult.MergedDoc, isLocalization: false, null);
				outputResult = "Full XML + .tbl (PTF fallback: deletions)";
			}
			else
			{
				outputResult = "Skipped (no changes vs vanilla)";
			}
		}
		return outputResult;
	}

	private static void LogFileSummary(string baseFile, MergeResult mergeResult, List<ModFileSource> allSources, string outputResult, MergeReport mergeReport)
	{
		string fileName = Path.GetFileName(baseFile);
		string text = mergeResult.TableType?.ToString() ?? "Unknown";
		string text2 = ((mergeResult.PkColumns != null && mergeResult.PkColumns.Count > 0) ? string.Join(", ", mergeResult.PkColumns) : "none");
		List<string> list = allSources.Select((ModFileSource s) => s.ModName).Distinct().ToList();
		string propertyValue = new string('─', Math.Max(60, fileName.Length + 6));
		Log.Information("{Sep}", propertyValue);
		Log.Information("  {FileName} | Type: {Type} | PK: [{PK}] | Mods: {ModCount}", fileName, text, text2, list.Count);
		for (int i = 0; i < list.Count; i++)
		{
			string text3 = list[i];
			string key = baseFile + "|" + text3;
			string propertyValue2 = ((i < list.Count - 1) ? "├─" : "└─");
			if (mergeReport.FileStatistics.TryGetValue(key, out FileStats value))
			{
				List<string> list2 = new List<string>();
				if (value.Additions > 0)
				{
					list2.Add($"+{value.Additions} added");
				}
				if (value.Modifications > 0)
				{
					list2.Add($"~{value.Modifications} modified");
				}
				if (value.Deletions > 0)
				{
					list2.Add($"-{value.Deletions} deleted");
				}
				if (list2.Count == 0)
				{
					list2.Add("no changes");
				}
				Log.Information("  {Prefix} {ModName,-24} {Stats}", propertyValue2, text3, string.Join(", ", list2));
			}
			else
			{
				Log.Information("  {Prefix} {ModName,-24} (no stats)", propertyValue2, text3);
			}
		}
		Log.Information("  Result: {OutputResult}", outputResult);
		Log.Information("{Sep}", propertyValue);
	}
}
