using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;

namespace KCDMerge.Core.Services;

public class ModFileTracker : IModFileTracker
{
	private class TrackedFile
	{
		public required string ModName { get; init; }

		public required int Priority { get; init; }

		public required string PakPath { get; init; }

		public required string EntryName { get; init; }

		public DateTime EntryModifiedDate { get; init; }
	}

	private readonly Dictionary<string, List<TrackedFile>> _fileToMods;

	private readonly IPakManager _pakManager;

	private readonly object _lock = new object();

	public ModFileTracker(IPakManager pakManager)
	{
		_pakManager = pakManager ?? throw new ArgumentNullException("pakManager");
		_fileToMods = new Dictionary<string, List<TrackedFile>>(StringComparer.OrdinalIgnoreCase);
	}

	public void TrackFile(string fileName, string modName, int priority, string pakPath, string entryName, DateTime entryModifiedDate)
	{
		if (string.IsNullOrEmpty(fileName))
		{
			Log.Warning("[TRACKER] Cannot track file: fileName is null or empty");
			return;
		}
		if (string.IsNullOrEmpty(modName))
		{
			Log.Warning("[TRACKER] Cannot track file {FileName}: modName is null or empty", fileName);
			return;
		}
		string text = fileName.Replace('\\', '/');
		if (IsLocalizationPak(pakPath))
		{
			string text2 = ExtractLanguageCode(pakPath);
			if (!string.IsNullOrEmpty(text2))
			{
				text = text2 + "/text.xml";
			}
		}
		lock (_lock)
		{
			if (!_fileToMods.ContainsKey(text))
			{
				_fileToMods[text] = new List<TrackedFile>();
			}
			TrackedFile item = new TrackedFile
			{
				ModName = modName,
				Priority = priority,
				PakPath = pakPath,
				EntryName = entryName,
				EntryModifiedDate = entryModifiedDate
			};
			_fileToMods[text].Add(item);
			Log.Debug("[TRACKER] {TrackingKey}: Tracked from {ModName} (priority {Priority}) in {PakPath}", text, modName, priority, Path.GetFileName(pakPath));
		}
	}

	public IReadOnlyList<string> GetAllTrackedFiles()
	{
		lock (_lock)
		{
			return _fileToMods.Keys.OrderBy((string f) => f).ToList();
		}
	}

	public IReadOnlyList<ModFileSource> GetModSourcesForFile(string fileName)
	{
		if (string.IsNullOrEmpty(fileName))
		{
			return Array.Empty<ModFileSource>();
		}
		string text = fileName.Replace('\\', '/');
		lock (_lock)
		{
			if (!_fileToMods.TryGetValue(text, out List<TrackedFile> value))
			{
				return Array.Empty<ModFileSource>();
			}
			List<ModFileSource> list = new List<ModFileSource>();
			foreach (TrackedFile item in value.OrderBy((TrackedFile t) => t.Priority))
			{
				Stream stream = ReadFromPak(item.PakPath, item.EntryName);
				if (stream != null)
				{
					bool flag = IsLocalizationPak(item.PakPath);
					string languageCode = (flag ? ExtractLanguageCode(item.PakPath) : null);
					bool flag2 = IsPatchTableFile(text);
					string baseFileName = (flag2 ? GetBaseFileName(text) : null);
					DateTime timestamp = ((item.EntryModifiedDate != DateTime.MinValue) ? item.EntryModifiedDate : (File.Exists(item.PakPath) ? File.GetLastWriteTimeUtc(item.PakPath) : DateTime.UtcNow));
					list.Add(new ModFileSource
					{
						ModName = item.ModName,
						XmlStream = stream,
						Priority = item.Priority,
						PakPath = item.PakPath,
						EntryName = item.EntryName,
						IsLocalization = flag,
						LanguageCode = languageCode,
						IsPatchFile = flag2,
						BaseFileName = baseFileName,
						Timestamp = timestamp
					});
				}
			}
			return list;
		}
	}

	public void Clear()
	{
		lock (_lock)
		{
			_fileToMods.Clear();
			Log.Debug("[TRACKER] Cleared all tracked files");
		}
	}

	public Dictionary<string, int> GetStatistics()
	{
		lock (_lock)
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			foreach (List<TrackedFile> value in _fileToMods.Values)
			{
				foreach (TrackedFile item in value)
				{
					if (!dictionary.ContainsKey(item.ModName))
					{
						dictionary[item.ModName] = 0;
					}
					dictionary[item.ModName]++;
				}
			}
			return dictionary;
		}
	}

	private Stream? ReadFromPak(string pakPath, string entryName)
	{
		try
		{
			if (!File.Exists(pakPath))
			{
				Log.Warning("[TRACKER] PAK file not found: {PakPath}", pakPath);
				return null;
			}
			MemoryStream? result = _pakManager.ReadFileToMemoryAsync(pakPath, entryName).GetAwaiter().GetResult();
			if (result == null)
			{
				Log.Warning("[TRACKER] Entry {EntryName} not found in PAK {PakPath}", entryName, Path.GetFileName(pakPath));
			}
			return result;
		}
		catch (Exception exception)
		{
			Log.Error(exception, "[TRACKER] Failed to read {EntryName} from PAK {PakPath}", entryName, Path.GetFileName(pakPath));
			return null;
		}
	}

	private bool IsLocalizationPak(string pakPath)
	{
		if (!pakPath.Contains("Localization", StringComparison.OrdinalIgnoreCase) && !pakPath.Contains("/Localization/", StringComparison.OrdinalIgnoreCase))
		{
			return pakPath.Contains("\\Localization\\", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private string? ExtractLanguageCode(string pakPath)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(pakPath);
		if (fileNameWithoutExtension.EndsWith("_xml", StringComparison.OrdinalIgnoreCase))
		{
			return fileNameWithoutExtension.Substring(0, fileNameWithoutExtension.Length - 4).ToLowerInvariant();
		}
		return null;
	}

	private bool IsPatchTableFile(string fileName)
	{
		return Path.GetFileNameWithoutExtension(fileName).Contains("__");
	}

	private string GetBaseFileName(string patchFileName)
	{
		string text = Path.GetDirectoryName(patchFileName) ?? "";
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(patchFileName);
		string extension = Path.GetExtension(patchFileName);
		int num = fileNameWithoutExtension.IndexOf("__");
		if (num > 0)
		{
			string text2 = fileNameWithoutExtension.Substring(0, num) + extension;
			if (!string.IsNullOrEmpty(text))
			{
				return Path.Combine(text, text2).Replace('\\', '/');
			}
			return text2;
		}
		return patchFileName;
	}
}
