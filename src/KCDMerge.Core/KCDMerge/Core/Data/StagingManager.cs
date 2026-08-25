using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KCDMerge.Core.Configuration;
using Serilog;

namespace KCDMerge.Core.Data;

public class StagingManager : IStagingManager
{
	private readonly IConfigurationService _configService;

	private readonly string _stagingPath;

	private readonly string _localizationStagingPath;

	public StagingManager(IConfigurationService configService)
	{
		_configService = configService;
		string text = _configService.LoadConfiguration().GetResolvedTempPath();
		_stagingPath = Path.Combine(text, "Staging");
		_localizationStagingPath = Path.Combine(text, "Staging_localization");
		Log.Debug("[STAGING] TempPath: {TempBase}", text);
		Log.Debug("[STAGING] Staging: {StagingPath}", _stagingPath);
		Log.Debug("[STAGING] Localization: {LocalizationPath}", _localizationStagingPath);
	}

	public string GetStagingPath()
	{
		return _stagingPath;
	}

	public Task CleanStagingAsync()
	{
		return Task.Run(delegate
		{
			List<string> list = new List<string>();
			CleanDirectory(_stagingPath, list);
			CleanDirectory(_localizationStagingPath, list);
			if (list.Count > 0)
			{
				Log.Error("CRITICAL: Staging cleanup incomplete. {FailedCount} file(s) could not be deleted. This WILL cause stale data corruption.", list.Count);
				Log.Error("Failed files: {FailedItems}", string.Join(", ", list));
				throw new IOException($"CRITICAL: Failed to clean staging directory. {list.Count} file(s) could not be deleted. Manual cleanup required: Delete all files in staging directory before next run.");
			}
		});
	}

	private void CleanDirectory(string path, List<string> failedDeletions)
	{
		if (!Directory.Exists(path))
		{
			Directory.CreateDirectory(path);
			return;
		}
		DirectoryInfo directoryInfo = new DirectoryInfo(path);
		FileInfo[] files = directoryInfo.GetFiles();
		foreach (FileInfo fileInfo in files)
		{
			try
			{
				fileInfo.Delete();
			}
			catch (Exception exception)
			{
				Log.Error(exception, "Failed to delete file {FileName}", fileInfo.Name);
				failedDeletions.Add(fileInfo.Name);
			}
		}
		DirectoryInfo[] directories = directoryInfo.GetDirectories();
		foreach (DirectoryInfo directoryInfo2 in directories)
		{
			DeleteFilesRecursively(directoryInfo2, failedDeletions);
			directoryInfo2.Refresh();
			if (!TryDeleteDirectory(directoryInfo2, failedDeletions))
			{
				Log.Error("Failed to delete directory {DirName} after retries", directoryInfo2.Name);
			}
		}
	}

	private void DeleteFilesRecursively(DirectoryInfo dir, List<string> failedDeletions)
	{
		FileInfo[] files = dir.GetFiles();
		foreach (FileInfo fileInfo in files)
		{
			try
			{
				if (fileInfo.IsReadOnly)
				{
					fileInfo.IsReadOnly = false;
				}
				fileInfo.Delete();
			}
			catch (Exception exception)
			{
				Log.Error(exception, "Failed to delete file {FilePath}", fileInfo.FullName);
				failedDeletions.Add(fileInfo.FullName);
			}
		}
		DirectoryInfo[] directories = dir.GetDirectories();
		foreach (DirectoryInfo directoryInfo in directories)
		{
			DeleteFilesRecursively(directoryInfo, failedDeletions);
			try
			{
				directoryInfo.Refresh();
				if (directoryInfo.Exists)
				{
					if ((directoryInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
					{
						directoryInfo.Attributes &= ~FileAttributes.ReadOnly;
					}
					directoryInfo.Delete(recursive: false);
				}
			}
			catch (Exception exception2)
			{
				Log.Debug(exception2, "Could not delete subdirectory {SubDirPath}", directoryInfo.FullName);
			}
		}
	}

	private bool TryDeleteDirectory(DirectoryInfo dir, List<string> failedDeletions)
	{
		for (int i = 0; i < 3; i++)
		{
			try
			{
				if (i > 0)
				{
					GC.Collect();
					GC.WaitForPendingFinalizers();
					Thread.Sleep(100 * i);
				}
				dir.Refresh();
				int propertyValue = dir.GetFiles("*", SearchOption.AllDirectories).Length;
				int propertyValue2 = dir.GetDirectories("*", SearchOption.AllDirectories).Length;
				Log.Debug("Attempting to delete directory {DirPath}: Files={FileCount}, Subdirs={DirCount}", dir.FullName, propertyValue, propertyValue2);
				if ((dir.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
				{
					dir.Attributes &= ~FileAttributes.ReadOnly;
				}
				dir.Delete(recursive: true);
				return true;
			}
			catch (IOException ex) when (i < 2)
			{
				Log.Debug(ex, "Retry {Retry}/{MaxRetries} for directory {DirName}: {ExceptionType} - {Message}", i + 1, 3, dir.Name, ex.GetType().Name, ex.Message);
			}
			catch (Exception ex2)
			{
				Log.Error(ex2, "Failed to delete directory {DirPath} - Exception Type: {ExceptionType}, Message: {Message}, HResult: {HResult}", dir.FullName, ex2.GetType().Name, ex2.Message, ex2.HResult);
				failedDeletions.Add(dir.FullName);
				return false;
			}
		}
		Log.Error("Failed to delete directory {DirPath} after {MaxRetries} retries", dir.FullName, 3);
		failedDeletions.Add(dir.FullName);
		return false;
	}
}
