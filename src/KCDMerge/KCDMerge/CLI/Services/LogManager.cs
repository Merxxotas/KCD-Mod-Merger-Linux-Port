using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;
using Serilog.Events;

namespace KCDMerge.CLI.Services;

internal static class LogManager
{
	public static string SetupLogger(string exeDir)
	{
		string text = DateTime.Now.ToString("yyyy.MM.dd_HH.mm.ss");
		string text2 = Path.Combine(exeDir, "KCDMerge - (" + text + ").log");
		LoggerConfiguration loggerConfiguration = new LoggerConfiguration().MinimumLevel.Verbose();
		loggerConfiguration.WriteTo.Console(LogEventLevel.Information, "{Message:lj}{NewLine}{Exception}", null, null, null, null, applyThemeToRedirectedOutput: true);
		loggerConfiguration.WriteTo.File(text2, LogEventLevel.Verbose, "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}", null, 1073741824L, null, buffered: false, shared: false, null, RollingInterval.Infinite, rollOnFileSizeLimit: false, 31, null, null, null);
		Log.Logger = loggerConfiguration.CreateLogger();
		return text2;
	}

	public static void CleanupOldLogs(string exeDir, string currentLogPath, int keepCount)
	{
		if (keepCount <= 0)
		{
			Log.Debug("Log cleanup disabled (keepCount = {KeepCount})", keepCount);
			return;
		}
		try
		{
			List<FileInfo> list = (from f in Directory.GetFiles(exeDir, "KCDMerge*.log")
				where !f.Equals(currentLogPath, StringComparison.OrdinalIgnoreCase)
				select new FileInfo(f) into f
				orderby f.LastWriteTime descending
				select f).ToList().Skip(keepCount - 1).ToList();
			foreach (FileInfo item in list)
			{
				try
				{
					item.Delete();
					Log.Debug("Deleted old KCDMerge log: {FileName}", item.Name);
				}
				catch (Exception exception)
				{
					Log.Warning(exception, "Failed to delete old log file: {FileName}", item.Name);
				}
			}
			if (list.Count > 0)
			{
				Log.Information("Cleaned up {Count} old KCDMerge log(s), keeping {KeepCount} most recent", list.Count, keepCount);
			}
		}
		catch (Exception exception2)
		{
			Log.Warning(exception2, "Failed to clean up KCDMerge log files");
		}
	}

	public static void CleanupUserCfgLogs(string gamePath, int keepCount)
	{
		if (keepCount <= 0)
		{
			return;
		}
		try
		{
			string searchPattern = "user.cfg.log*";
			List<FileInfo> list = (from f in Directory.GetFiles(gamePath, searchPattern)
				select new FileInfo(f) into f
				orderby f.LastWriteTime descending
				select f).ToList();
			if (list.Count == 0)
			{
				return;
			}
			List<FileInfo> list2 = list.Skip(keepCount).ToList();
			foreach (FileInfo item in list2)
			{
				try
				{
					item.Delete();
					Log.Debug("Deleted old user.cfg.log: {FileName}", item.Name);
				}
				catch (Exception exception)
				{
					Log.Warning(exception, "Failed to delete user.cfg.log: {FileName}", item.Name);
				}
			}
			if (list2.Count > 0)
			{
				Log.Information("Cleaned up {Count} old user.cfg.log file(s), keeping {KeepCount} most recent", list2.Count, keepCount);
			}
		}
		catch (Exception exception2)
		{
			Log.Warning(exception2, "Failed to clean up user.cfg.log files");
		}
	}
}
