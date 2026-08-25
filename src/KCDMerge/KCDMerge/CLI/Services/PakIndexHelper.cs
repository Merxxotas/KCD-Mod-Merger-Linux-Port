using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KCDMerge.Core.Services;
using Serilog;

namespace KCDMerge.CLI.Services;

internal static class PakIndexHelper
{
	public static Dictionary<string, (string PakPath, string EntryName, DateTime ModifiedDate)> BuildIntraModFileIndex(List<string> pakFiles, IPakManager pakManager, string modName, string tempBase)
	{
		Dictionary<string, (string, string, DateTime)> dictionary = new Dictionary<string, (string, string, DateTime)>(StringComparer.OrdinalIgnoreCase);
		List<string> list = pakFiles.OrderBy((string p) => Path.GetFileName(p)).ToList();
		foreach (string item in list)
		{
			try
			{
				string localizationPakPrefix = GetLocalizationPakPrefix(item);
				foreach (var (text, dateTime) in pakManager.GetPakEntriesWithTimestamps(item))
				{
					if (text.EndsWith("/") || text.EndsWith("\\"))
					{
						continue;
					}
					string text2 = text.Replace('\\', '/');
					string text3 = (string.IsNullOrEmpty(localizationPakPrefix) ? text2 : (localizationPakPrefix + "/" + text2));
					if (dictionary.TryGetValue(text3, out var value))
					{
						if (dateTime > value.Item3)
						{
							dictionary[text3] = (item, text, dateTime);
							Log.Debug("[INTRA-MOD] {Entry}: Overridden by newer version from {PakFile} ({OldDate} -> {NewDate})", text3, Path.GetFileName(item), value.Item3, dateTime);
						}
					}
					else
					{
						dictionary[text3] = (item, text, dateTime);
					}
				}
			}
			catch (Exception exception)
			{
				Log.Warning(exception, "Failed to index PAK {PakFile}", Path.GetFileName(item));
			}
		}
		WritePakIndexToFile(modName, dictionary, list, tempBase);
		return dictionary;
	}

	public static string? GetDeepPakPrefix(string pakPath, string modRootPath)
	{
		string directoryName = Path.GetDirectoryName(pakPath);
		if (string.IsNullOrEmpty(directoryName))
		{
			return null;
		}
		string text = Path.GetRelativePath(modRootPath, directoryName).Replace('\\', '/');
		if (text.StartsWith("Data/", StringComparison.OrdinalIgnoreCase))
		{
			return text.Substring(5);
		}
		return null;
	}

	public static string? GetLocalizationPakPrefix(string pakPath)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(pakPath);
		if (fileNameWithoutExtension.EndsWith("_xml", StringComparison.OrdinalIgnoreCase))
		{
			string directoryName = Path.GetDirectoryName(pakPath);
			if (!string.IsNullOrEmpty(directoryName) && directoryName.Contains("Localization", StringComparison.OrdinalIgnoreCase))
			{
				return fileNameWithoutExtension.Substring(0, fileNameWithoutExtension.Length - 4).ToLowerInvariant();
			}
		}
		return null;
	}

	public static void WritePakIndexToFile(string modName, Dictionary<string, (string PakPath, string EntryName, DateTime ModifiedDate)> index, List<string> pakFiles, string tempBase)
	{
		try
		{
			string text = Path.Combine(tempBase, "pak_index");
			Directory.CreateDirectory(text);
			string text2 = Path.Combine(text, modName + ".txt");
			using StreamWriter streamWriter = new StreamWriter(text2, append: false, Encoding.UTF8);
			streamWriter.WriteLine("# PAK Index for: " + modName);
			streamWriter.WriteLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			streamWriter.WriteLine($"# Total PAK files: {pakFiles.Count}");
			streamWriter.WriteLine($"# Total unique files: {index.Count}");
			streamWriter.WriteLine();
			streamWriter.WriteLine("## PAK Files (in processing order):");
			for (int i = 0; i < pakFiles.Count; i++)
			{
				streamWriter.WriteLine($"{i + 1}. {Path.GetFileName(pakFiles[i])}");
			}
			streamWriter.WriteLine();
			streamWriter.WriteLine("## File Index (filepath -> source PAK + timestamp):");
			streamWriter.WriteLine("# Format: [filepath] | [PAK file] | [modified date]");
			streamWriter.WriteLine();
			foreach (KeyValuePair<string, (string, string, DateTime)> item in index.OrderBy<KeyValuePair<string, (string, string, DateTime)>, string>((KeyValuePair<string, (string PakPath, string EntryName, DateTime ModifiedDate)> x) => x.Key))
			{
				var (path, _, value) = item.Value;
				streamWriter.WriteLine($"{item.Key} | {Path.GetFileName(path)} | {value:yyyy-MM-dd HH:mm:ss}");
			}
			Log.Debug("Wrote PAK index to {IndexFile}", text2);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Failed to write PAK index for {ModName}", modName);
		}
	}
}
