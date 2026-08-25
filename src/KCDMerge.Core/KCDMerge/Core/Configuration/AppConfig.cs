using System;
using System.Collections.Generic;
using System.IO;

namespace KCDMerge.Core.Configuration;

public class AppConfig
{
	public string GamePath { get; set; } = string.Empty;

	public string ModsPath { get; set; } = string.Empty;

	public string OutputPath { get; set; } = "KCDMerge";

	public string TempPath { get; set; } = "%TEMP%\\KCDMerge";

	public bool TempCleanUp { get; set; } = true;

	public bool OverwriteOutput { get; set; } = true;

	public bool OpenLogFileAfterMerge { get; set; }

	public int LogRetentionCount { get; set; } = 3;

	public string FileLogLevel { get; set; } = "Information";

	public string OutputMode { get; set; } = "PTF";

	public string StalenessCheckMode { get; set; } = "WarnOnly";

	public List<string> StalenessCheckMods { get; set; } = new List<string>();

	public string GetResolvedTempPath()
	{
		return ResolvePath(TempPath, isTemp: true);
	}

	public static string ResolvePath(string? path, bool isTemp = false)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return isTemp ? Path.Combine(Path.GetTempPath(), "KCDMerge") : string.Empty;
		}

		path = path.Trim('"', '\'', ' ');

		if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
		{
			string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			path = Path.Combine(home, path.Substring(2));
		}
		else if (path == "~")
		{
			path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		}

		if (isTemp && (path.StartsWith("%TEMP%", StringComparison.OrdinalIgnoreCase) || path.StartsWith("$TMP", StringComparison.OrdinalIgnoreCase) || path.StartsWith("$TMPDIR", StringComparison.OrdinalIgnoreCase)))
		{
			if (!OperatingSystem.IsWindows())
			{
				if (path.StartsWith("%TEMP%\\", StringComparison.OrdinalIgnoreCase) || path.StartsWith("%TEMP%/", StringComparison.OrdinalIgnoreCase))
				{
					string sub = path.Substring(7);
					path = Path.Combine(Path.GetTempPath(), sub);
				}
				else if (path.Equals("%TEMP%", StringComparison.OrdinalIgnoreCase))
				{
					path = Path.GetTempPath();
				}
			}
		}

		path = Environment.ExpandEnvironmentVariables(path);
		return path;
	}
}
