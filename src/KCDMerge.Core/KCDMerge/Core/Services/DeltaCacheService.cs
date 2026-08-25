using System;
using System.IO;
using System.Xml.Linq;
using KCDMerge.Core.Configuration;
using KCDMerge.Core.Data;
using KCDMerge.Core.Models;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KCDMerge.Core.Services;

public class DeltaCacheService : IDeltaCacheService
{
	private readonly string _cacheFilePath;

	private DeltaCacheRoot _cache = new DeltaCacheRoot();

	public DeltaCacheService(IConfigurationService configService)
	{
		string path = configService.LoadConfiguration().GetResolvedTempPath();
		_cacheFilePath = Path.Combine(path, "delta_cache.yaml");
	}

	public void LoadCache()
	{
		if (!File.Exists(_cacheFilePath))
		{
			Log.Debug("[DELTA-CACHE] No cache file found at {Path}", _cacheFilePath);
			_cache = new DeltaCacheRoot();
			return;
		}
		try
		{
			string input = File.ReadAllText(_cacheFilePath);
			IDeserializer deserializer = new DeserializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance).Build();
			_cache = deserializer.Deserialize<DeltaCacheRoot>(input) ?? new DeltaCacheRoot();
			Log.Information("[DELTA-CACHE] Loaded {Count} cached delta(s)", _cache.Entries.Count);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "[DELTA-CACHE] Failed to load cache, starting fresh");
			_cache = new DeltaCacheRoot();
		}
	}

	public void SaveCache()
	{
		try
		{
			string directoryName = Path.GetDirectoryName(_cacheFilePath);
			if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string contents = new SerializerBuilder().WithNamingConvention(PascalCaseNamingConvention.Instance).Build().Serialize(_cache);
			File.WriteAllText(_cacheFilePath, contents);
			Log.Debug("[DELTA-CACHE] Saved {Count} delta(s) to cache", _cache.Entries.Count);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "[DELTA-CACHE] Failed to save cache");
		}
	}

	public DeltaResult? TryGetCachedDelta(string fileName, string modName, DateTime sourceTimestamp, DateTime vanillaTimestamp, string? pakSource = null)
	{
		string text = (string.IsNullOrEmpty(pakSource) ? (fileName + "|" + modName) : $"{fileName}|{modName}|{pakSource}");
		if (!_cache.Entries.TryGetValue(text, out DeltaCacheEntry value))
		{
			return null;
		}
		if (value.SourceTimestamp != sourceTimestamp || value.VanillaTimestamp != vanillaTimestamp)
		{
			Log.Debug("[DELTA-CACHE] {Key}: Timestamp mismatch, cache invalid", text);
			return null;
		}
		try
		{
			XDocument deltaDoc = XDocument.Parse(value.DeltaXml);
			DeltaType type = Enum.Parse<DeltaType>(value.DeltaType);
			DeltaResult result = new DeltaResult
			{
				DeltaDoc = deltaDoc,
				Type = type,
				PkColumns = value.PkColumns,
				AddedRows = value.AddedRows,
				DeletedRows = value.DeletedRows,
				ModifiedRows = value.ModifiedRows,
				UnchangedRows = value.UnchangedRows
			};
			Log.Debug("[DELTA-CACHE] {Key}: Cache hit", text);
			return result;
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "[DELTA-CACHE] {Key}: Failed to deserialize cached delta", text);
			return null;
		}
	}

	public void CacheDelta(string fileName, string modName, DateTime sourceTimestamp, DateTime vanillaTimestamp, DeltaResult delta, string? pakSource = null)
	{
		string text = (string.IsNullOrEmpty(pakSource) ? (fileName + "|" + modName) : $"{fileName}|{modName}|{pakSource}");
		DeltaCacheEntry value = new DeltaCacheEntry
		{
			FileName = fileName,
			ModName = modName,
			SourceTimestamp = sourceTimestamp,
			VanillaTimestamp = vanillaTimestamp,
			DeltaXml = delta.DeltaDoc.ToString(),
			DeltaType = delta.Type.ToString(),
			PkColumns = delta.PkColumns,
			AddedRows = delta.AddedRows,
			DeletedRows = delta.DeletedRows,
			ModifiedRows = delta.ModifiedRows,
			UnchangedRows = delta.UnchangedRows
		};
		_cache.Entries[text] = value;
		Log.Debug("[DELTA-CACHE] {Key}: Cached delta", text);
	}
}
