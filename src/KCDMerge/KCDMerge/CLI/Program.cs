using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using KCDMerge.CLI.Services;
using KCDMerge.Core.Configuration;
using KCDMerge.Core.Data;
using KCDMerge.Core.Models;
using KCDMerge.Core.Services;
using Serilog;
using Spectre.Console;

namespace KCDMerge.CLI;

internal class Program
{
	private static async Task Main(string[] args)
	{
		string baseDirectory = AppContext.BaseDirectory;
		string logPath = LogManager.SetupLogger(baseDirectory);
		try
		{
			string fullPath = Path.GetFullPath("config.yaml");
			ConfigurationService configurationService = new ConfigurationService(fullPath);
			AppConfig config = configurationService.LoadConfiguration();
			if (config.LogRetentionCount > 0)
			{
				LogManager.CleanupOldLogs(baseDirectory, logPath, config.LogRetentionCount);
				LogManager.CleanupUserCfgLogs(config.GamePath, config.LogRetentionCount);
			}
			StagingManager stagingManager = new StagingManager(configurationService);
			new PakWriter(configurationService, stagingManager);
			ModConflictRulesService rulesService = new ModConflictRulesService();
			PreferenceService preferenceService = new PreferenceService(rulesService);
			ConsoleConflictResolver conflictResolver = new ConsoleConflictResolver(rulesService);
			ModScanner modScanner = new ModScanner(configurationService);
			PakManager pakManager = new PakManager();
			string tempBase = config.GetResolvedTempPath();
			string stagingPath = Path.Combine(tempBase, "Staging");
			VanillaPakIndexer vanillaPakIndexer = new VanillaPakIndexer(stagingPath);
			string gameDataPath = Path.Combine(config.GamePath, "Data");
			XPathIndexer indexer = new XPathIndexer();
			XDocumentMerger merger = new XDocumentMerger(indexer, conflictResolver, rulesService);
			XDocumentLoader loader = new XDocumentLoader(vanillaPakIndexer, pakManager, gameDataPath);
			DeltaNormalizer deltaNormalizer = new DeltaNormalizer(indexer);
			DeltaCacheService deltaCacheService = new DeltaCacheService(configurationService);
			MergePipeline mergePipeline = new MergePipeline(loader, merger, deltaNormalizer, indexer, conflictResolver, rulesService, deltaCacheService, config);
			ModFileTracker modFileTracker = new ModFileTracker(pakManager);
			XmlWriter xmlWriter = new XmlWriter(configurationService);
			IAssetMerger assetMerger = new AssetMerger(configurationService, pakManager, preferenceService, conflictResolver);
			BackupService backupService = new BackupService();
			ICfgMerger cfgMerger = new CfgMerger(backupService, rulesService, conflictResolver);
			AssetProcessor assetProcessor = new AssetProcessor(pakManager, assetMerger, modFileTracker, tempBase);
			XmlTracker xmlTracker = new XmlTracker(pakManager, modFileTracker, vanillaPakIndexer, tempBase);
			MergeExecutor mergeExecutor = new MergeExecutor(mergePipeline, modFileTracker, deltaCacheService, xmlWriter, config);
			AnsiConsole.Write(new FigletText("KCDMerge").Color(Color.Red));
			Log.Information("KCDMerge started");
			if (!new ConfigInitializer().InitializeConfig(config, fullPath, configurationService))
			{
				return;
			}
			Log.Information("Loading vanilla game data index...");
			AnsiConsole.MarkupLine("[bold yellow]Loading vanilla game data index...[/]");
			await vanillaPakIndexer.BuildIndexAsync(gameDataPath);
			Log.Information("Initializing...");
			string path = Path.Combine(tempBase, "delta_cache.yaml");
			if (File.Exists(path))
			{
				File.Delete(path);
				Log.Debug("Cleared delta cache for fresh merge");
			}
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			try
			{
				await stagingManager.CleanStagingAsync();
			}
			catch (IOException ex)
			{
				AnsiConsole.MarkupLine("[red bold]CRITICAL ERROR: " + ex.Message + "[/]");
				AnsiConsole.MarkupLine("[red]Please close any programs that may have files open in the staging directory and try again.[/]");
				return;
			}
			modFileTracker.Clear();
			ModScanPresenter modScanPresenter = new ModScanPresenter();
			List<ModInfo> mods = modScanPresenter.ScanAndDisplayMods(modScanner, pakManager);
			if (mods == null)
			{
				return;
			}
			MergeReport mergeReport = mergePipeline.GetReport();
			await assetProcessor.ProcessAssetsAsync(mods, mergeReport);
			xmlTracker.TrackXmlFiles(mods);
			MergeExecutorResult mergeExecResult = await mergeExecutor.ExecuteMergesAsync();
			int num = await cfgMerger.MergeCfgFilesAsync(config.GamePath, mods, mergeReport);
			if (num > 0)
			{
				AnsiConsole.MarkupLine($"[green]Merged {num} variable(s) into user.cfg[/]");
			}
			AnsiConsole.WriteLine();
			new MergeReportRenderer().Render(mergeReport, mods);
			Log.Information("\u001b[33mWriting merged output...\u001b[0m");
			await new ModOutputBuilder(new PakCreator(), backupService).BuildModOutputAsync(config, stagingPath, modFileTracker, mergeExecResult.PtfOutputFiles, mergeExecResult.NoVanillaFiles);
			Log.Information("Log written to: {LogPath}", logPath);
			if (config.OpenLogFileAfterMerge)
			{
				try
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = logPath,
						UseShellExecute = true
					});
					Log.Information("Opened log file in default application");
				}
				catch (Exception exception)
				{
					Log.Warning(exception, "Failed to open log file automatically");
				}
			}
			if (ConsoleEnvironment.IsStartedFromExplorer())
			{
				AnsiConsole.WriteLine();
				AnsiConsole.WriteLine();
				AnsiConsole.MarkupLine("[yellow]Press any key to exit...[/]");
				Console.ReadKey(intercept: true);
			}
			Log.Information("KCDMerge completed successfully");
		}
		catch (Exception ex2)
		{
			Log.Fatal(ex2, "Application terminated unexpectedly");
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine($"[red bold]{ex2.GetType().Name}:[/] [red]{ex2.Message.EscapeMarkup()}[/]");
			if (ex2.StackTrace != null)
			{
				string[] array = ex2.StackTrace.Split('\n');
				for (int i = 0; i < array.Length; i++)
				{
					string text = array[i].Trim();
					if (!string.IsNullOrEmpty(text))
					{
						AnsiConsole.MarkupLine("[red]   " + text.EscapeMarkup() + "[/]");
					}
				}
			}
			AnsiConsole.WriteLine();
			if (ConsoleEnvironment.IsStartedFromExplorer())
			{
				AnsiConsole.MarkupLine("[yellow]Press any key to exit...[/]");
				Console.ReadKey(intercept: true);
			}
			Environment.Exit(1);
		}
		finally
		{
			Log.CloseAndFlush();
		}
	}
}
