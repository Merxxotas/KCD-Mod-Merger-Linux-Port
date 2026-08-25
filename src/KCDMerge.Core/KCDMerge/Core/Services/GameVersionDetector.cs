using System;
using System.Diagnostics;
using System.IO;
using Serilog;

namespace KCDMerge.Core.Services;

public class GameVersionDetector
{
	public string? GetGameVersion(string gamePath)
	{
		try
		{
			string text = Path.Combine(gamePath, "Bin", "Win64", "KingdomCome.exe");
			if (!File.Exists(text))
			{
				Log.Warning("Game executable not found at: {ExePath}", text);
				return null;
			}
			FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(text);
			string text2 = versionInfo.FileVersion ?? versionInfo.ProductVersion;
			if (!string.IsNullOrWhiteSpace(text2))
			{
				Log.Information("Detected game version: {Version}", text2);
				return text2;
			}
			return null;
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Failed to detect game version");
			return null;
		}
	}
}
