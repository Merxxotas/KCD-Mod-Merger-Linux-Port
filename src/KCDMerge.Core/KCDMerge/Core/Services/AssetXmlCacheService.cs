using System;
using System.IO;
using KCDMerge.Core.Models;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KCDMerge.Core.Services;

public class AssetXmlCacheService
{
	private readonly string _cacheFile;

	private AssetXmlCache _cache = new AssetXmlCache();

	public AssetXmlCacheService()
	{
		_cacheFile = Path.Combine(".temp", "asset_xml_cache.yaml");
		LoadCache();
	}

	public bool? IsExtensionXml(string extension)
	{
		if (string.IsNullOrEmpty(extension))
		{
			return null;
		}
		string key = extension.ToLowerInvariant();
		if (_cache.ExtensionIsXml.TryGetValue(key, out var value))
		{
			return value;
		}
		return null;
	}

	public void SetExtensionXml(string extension, bool isXml)
	{
		if (!string.IsNullOrEmpty(extension))
		{
			string key = extension.ToLowerInvariant();
			_cache.ExtensionIsXml[key] = isXml;
		}
	}

	public void SaveCache()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(_cacheFile) ?? ".temp");
			string contents = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build().Serialize(_cache);
			File.WriteAllText(_cacheFile, contents);
			Log.Debug("Saved asset XML cache with {Count} extension(s)", _cache.ExtensionIsXml.Count);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Failed to save asset XML cache");
		}
	}

	private void LoadCache()
	{
		if (!File.Exists(_cacheFile))
		{
			_cache = new AssetXmlCache();
			return;
		}
		try
		{
			string input = File.ReadAllText(_cacheFile);
			IDeserializer deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
			_cache = deserializer.Deserialize<AssetXmlCache>(input) ?? new AssetXmlCache();
			Log.Debug("Loaded asset XML cache with {Count} extension(s)", _cache.ExtensionIsXml.Count);
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Failed to load asset XML cache, starting fresh");
			_cache = new AssetXmlCache();
		}
	}
}
