using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using KCDMerge.Core.Models;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KCDMerge.Core.Services;

public class VanillaPakIndexer : IVanillaPakIndexer
{
	private readonly string _timestampCacheFile;

	private readonly string _pakIndexDir;

	private Dictionary<string, string> _index;

	private Dictionary<string, DateTime> _fileTimestamps;

	private PakTimestampCache _timestampCache;

	private string _gameDataPath = string.Empty;

	public VanillaPakIndexer(string stagingPath)
	{
		string text = Path.GetDirectoryName(stagingPath) ?? ".temp";
		Directory.CreateDirectory(text);
		_timestampCacheFile = Path.Combine(text, "pak_timestamps.yaml");
		_pakIndexDir = Path.Combine(text, "pak_index");
		Directory.CreateDirectory(_pakIndexDir);
		_index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		_fileTimestamps = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
		_timestampCache = new PakTimestampCache();
	}

	private PakTimestampCache LoadTimestampCache()
	{
		if (!File.Exists(_timestampCacheFile))
		{
			return new PakTimestampCache();
		}
		try
		{
			string input = File.ReadAllText(_timestampCacheFile);
			return new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build().Deserialize<PakTimestampCache>(input);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Failed to load timestamp cache, rebuilding");
			return new PakTimestampCache();
		}
	}

	private void SaveTimestampCache()
	{
		try
		{
			string contents = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build().Serialize(_timestampCache);
			File.WriteAllText(_timestampCacheFile, contents);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Failed to save timestamp cache");
		}
	}

	private bool NeedsScan(string pakPath, string relativePath)
	{
		DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(pakPath);
		if (!_timestampCache.Paks.TryGetValue(relativePath, out var value))
		{
			return true;
		}
		if (lastWriteTimeUtc > value)
		{
			return true;
		}
		string text = relativePath.Replace('/', '_').Replace('\\', '_');
		if (!File.Exists(Path.Combine(_pakIndexDir, text + ".txt")))
		{
			return true;
		}
		return false;
	}

	private void ExportPakIndex(string pakPath, string indexKey, List<string> entries)
	{
		string text = Path.Combine(_pakIndexDir, indexKey + ".txt");
		List<string> list = new List<string>
		{
			"ARCHIVE: " + indexKey,
			"PATH:    " + pakPath,
			$"MODIFIED: [{File.GetLastWriteTimeUtc(pakPath):yyyy-MM-dd HH:mm:ss}]",
			"--------------------------------------------------"
		};
		list.AddRange(entries);
		list.Add($"{entries.Count} files");
		list.Add("");
		File.WriteAllLines(text, list);
		Log.Debug("Exported PAK index: {IndexFile} ({Count} files)", text, entries.Count);
	}

	public async Task BuildIndexAsync(string gameDataPath)
	{
		_gameDataPath = gameDataPath;
		if (!Directory.Exists(gameDataPath))
		{
			throw new DirectoryNotFoundException("Game Data directory not found: " + gameDataPath);
		}
		string[] files = Directory.GetFiles(gameDataPath, "*.pak", SearchOption.AllDirectories);
		if (files.Length == 0)
		{
			throw new FileNotFoundException("No .pak files found in: " + gameDataPath);
		}
		Log.Information("Scanning {PakCount} vanilla PAK files...", files.Length);
		_timestampCache = LoadTimestampCache();
		_index.Clear();
		_fileTimestamps.Clear();
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		string[] array = files;
		foreach (string text in array)
		{
			string text2 = Path.GetRelativePath(gameDataPath, text).Replace('\\', '/');
			string fileName = Path.GetFileName(text);
			string text3 = text2.Replace('/', '_').Replace('\\', '_');
			if (!NeedsScan(text, text2))
			{
				foreach (string item in File.ReadAllLines(Path.Combine(_pakIndexDir, text3 + ".txt")).Skip(4))
				{
					if (!string.IsNullOrWhiteSpace(item) && !item.EndsWith(" files"))
					{
						string key = item.Replace('\\', '/');
						_index[key] = text2;
						num3++;
					}
				}
				num2++;
				Log.Debug("Skipped {PakFileName} (unchanged)", fileName);
				continue;
			}
			Log.Information("  Indexing: {RelativePath}", text2);
			List<string> list = new List<string>();
			try
			{
				using FileStream file = new FileStream(text, FileMode.Open, FileAccess.Read, FileShare.Read);
				using ZipFile zipFile = new ZipFile(file);
				foreach (ZipEntry item2 in zipFile)
				{
					if (!item2.IsDirectory)
					{
						string text4 = item2.Name.Replace('\\', '/');
						_index[text4] = text2;
						_fileTimestamps[text4] = item2.DateTime;
						list.Add(text4);
						num3++;
					}
				}
				ExportPakIndex(text, text3, list);
				_timestampCache.Paks[text2] = File.GetLastWriteTimeUtc(text);
				num++;
			}
			catch (InvalidDataException ex)
			{
				Log.Warning("Skipping corrupted PAK {RelativePath}: {Message}", text2, ex.Message);
				Log.Warning("This PAK file may be damaged. Verify game files if issues occur.");
				num2++;
			}
			catch (Exception exception)
			{
				Log.Error(exception, "Failed to index PAK {RelativePath}", text2);
				num2++;
			}
		}
		_timestampCache.LastScanTime = DateTime.UtcNow;
		SaveTimestampCache();
		ExportMasterIndex(num3);
		Log.Information("Indexed {TotalEntries} files from {PakCount} PAKs ({Scanned} scanned, {Skipped} cached)", num3, files.Length, num, num2);
		await Task.CompletedTask;
	}

	private void ExportMasterIndex(int totalEntries)
	{
		try
		{
			string text = Path.Combine(_pakIndexDir, "master.txt");
			using StreamWriter streamWriter = new StreamWriter(text, append: false, Encoding.UTF8);
			streamWriter.WriteLine("# MASTER VANILLA GAME FILE INDEX");
			streamWriter.WriteLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			streamWriter.WriteLine($"# Total files: {totalEntries}");
			streamWriter.WriteLine();
			streamWriter.WriteLine("## Format: [game file path] | [modified date] | [source PAK file]");
			streamWriter.WriteLine();
			foreach (KeyValuePair<string, string> item in _index.OrderBy<KeyValuePair<string, string>, string>((KeyValuePair<string, string> x) => x.Key))
			{
				DateTime value;
				string value2 = (_fileTimestamps.TryGetValue(item.Key, out value) ? value.ToString("yyyy-MM-dd HH:mm:ss") : "unknown");
				streamWriter.WriteLine($"{item.Key} | {value2} | {item.Value}");
			}
			Log.Information("Wrote master vanilla index to {MasterFile}", text);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Failed to write master vanilla index");
		}
	}

	public string? FindVanillaPak(string internalPath)
	{
		if (!_index.TryGetValue(internalPath, out string value))
		{
			return null;
		}
		return value;
	}

	public bool IsIndexValid(string gameDataPath)
	{
		if (!File.Exists(_timestampCacheFile))
		{
			return false;
		}
		try
		{
			_timestampCache = LoadTimestampCache();
			string[] files = Directory.GetFiles(gameDataPath, "*.pak", SearchOption.AllDirectories);
			if (files.Length != _timestampCache.Paks.Count)
			{
				return false;
			}
			string[] array = files;
			foreach (string path in array)
			{
				string key = Path.GetRelativePath(gameDataPath, path).Replace('\\', '/');
				if (!_timestampCache.Paks.TryGetValue(key, out var value))
				{
					return false;
				}
				if (File.GetLastWriteTimeUtc(path) > value)
				{
					return false;
				}
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	public List<string> FindByFilename(string filename)
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, string> item2 in _index)
		{
			string key = item2.Key;
			string value = item2.Value;
			if (string.Equals(Path.GetFileName(key), filename, StringComparison.OrdinalIgnoreCase))
			{
				string text = Path.GetDirectoryName(value)?.Replace('\\', '/');
				string item = (string.IsNullOrEmpty(text) ? key : (text + "/" + key));
				list.Add(item);
			}
		}
		return list;
	}

	public DateTime GetVanillaEntryTimestamp(string internalPath)
	{
		string text = internalPath.Replace('\\', '/');
		if (_fileTimestamps.TryGetValue(text, out var value))
		{
			return value;
		}
		if (!_index.TryGetValue(text, out string value2))
		{
			return DateTime.MinValue;
		}
		if (string.IsNullOrEmpty(_gameDataPath))
		{
			return DateTime.MinValue;
		}
		string path = Path.Combine(_gameDataPath, value2);
		if (!File.Exists(path))
		{
			return DateTime.MinValue;
		}
		try
		{
			using FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			using ZipFile zipFile = new ZipFile(file);
			foreach (ZipEntry item in zipFile)
			{
				if (!item.IsDirectory && string.Equals(item.Name.Replace('\\', '/'), text, StringComparison.OrdinalIgnoreCase))
				{
					_fileTimestamps[text] = item.DateTime;
					return item.DateTime;
				}
			}
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "[VANILLA-TS] Failed to read entry timestamp for {Path} from {Pak}", text, value2);
		}
		return DateTime.MinValue;
	}
}
