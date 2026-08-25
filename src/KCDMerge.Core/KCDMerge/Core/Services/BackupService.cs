using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;

namespace KCDMerge.Core.Services;

public class BackupService : IBackupService
{
	private static readonly Regex BackupPattern = new Regex("\\.(\\d{4}\\.\\d{2}\\.\\d{2}_\\d{2}\\.\\d{2}\\.\\d{2})\\.backup$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public string? BackupFile(string filePath, int maxBackups = 5)
	{
		if (!File.Exists(filePath))
		{
			Log.Debug("[BACKUP] File does not exist, skipping backup: {FilePath}", filePath);
			return null;
		}
		try
		{
			string text = DateTime.Now.ToString("yyyy.MM.dd_HH.mm.ss");
			string text2 = filePath + ".(" + text + ").backup";
			File.Copy(filePath, text2, overwrite: true);
			Log.Information("[BACKUP] Created backup: {BackupPath}", Path.GetFileName(text2));
			RotateBackups(filePath, maxBackups);
			return text2;
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "[BACKUP] Failed to create backup for {FilePath}", filePath);
			return null;
		}
	}

	private void RotateBackups(string originalFilePath, int maxBackups)
	{
		try
		{
			string directoryName = Path.GetDirectoryName(originalFilePath);
			if (string.IsNullOrEmpty(directoryName))
			{
				return;
			}
			string fileName = Path.GetFileName(originalFilePath);
			string searchPattern = fileName + ".*.backup";
			var list = (from f in Directory.GetFiles(directoryName, searchPattern)
				select new
				{
					Path = f,
					Match = BackupPattern.Match(f)
				} into f
				where f.Match.Success
				orderby f.Match.Groups[1].Value descending
				select f).ToList();
			if (list.Count <= maxBackups)
			{
				return;
			}
			var list2 = list.Skip(maxBackups).ToList();
			foreach (var item in list2)
			{
				File.Delete(item.Path);
				Log.Debug("[BACKUP] Rotated old backup: {FileName}", Path.GetFileName(item.Path));
			}
			Log.Information("[BACKUP] Rotated {Count} old backup(s) for {FileName}", list2.Count, fileName);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "[BACKUP] Failed to rotate backups for {FilePath}", originalFilePath);
		}
	}
}
