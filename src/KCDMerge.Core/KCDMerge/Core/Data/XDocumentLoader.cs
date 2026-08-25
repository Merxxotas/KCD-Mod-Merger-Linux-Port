using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using KCDMerge.Core.Services;
using Serilog;

namespace KCDMerge.Core.Data;

public class XDocumentLoader : IXDocumentLoader
{
	private readonly IVanillaPakIndexer _vanillaIndexer;

	private readonly IPakManager _pakManager;

	private readonly string _gameDataPath;

	public XDocumentLoader(IVanillaPakIndexer vanillaIndexer, IPakManager pakManager, string gameDataPath)
	{
		_vanillaIndexer = vanillaIndexer ?? throw new ArgumentNullException("vanillaIndexer");
		_pakManager = pakManager ?? throw new ArgumentNullException("pakManager");
		_gameDataPath = gameDataPath ?? throw new ArgumentNullException("gameDataPath");
	}

	public async Task<XDocument?> LoadFromStreamAsync(Stream stream, string fileName)
	{
		if (stream == null)
		{
			Log.Warning("[LOAD] Cannot load {FileName}: stream is null", fileName);
			return null;
		}
		try
		{
			if (stream.CanSeek)
			{
				stream.Position = 0L;
			}
			XDocument xDocument = await XDocument.LoadAsync(stream, LoadOptions.PreserveWhitespace, default(CancellationToken));
			if (xDocument.Root == null)
			{
				Log.Warning("[LOAD] {FileName}: Document has no root element", fileName);
				return null;
			}
			Log.Debug("[LOAD] {FileName}: Successfully loaded ({Size} bytes)", fileName, stream.Length);
			return xDocument;
		}
		catch (XmlException ex)
		{
			Log.Error("[LOAD ERROR] {FileName}: Invalid XML - {Message} at line {Line}, position {Position}", fileName, ex.Message, ex.LineNumber, ex.LinePosition);
			return null;
		}
		catch (Exception exception)
		{
			Log.Error(exception, "[LOAD ERROR] {FileName}: Unexpected error loading XML", fileName);
			return null;
		}
	}

	public async Task<XDocument?> LoadVanillaAsync(string fileName)
	{
		_ = 1;
		try
		{
			string normalizedPath = fileName.Replace('\\', '/');
			Log.Debug("[LOAD] Searching for vanilla file: {FileName}", normalizedPath);
			string pakFileName = _vanillaIndexer.FindVanillaPak(normalizedPath);
			if (string.IsNullOrEmpty(pakFileName))
			{
				Log.Warning("[LOAD] {FileName}: Not found in vanilla PAK index", normalizedPath);
				return null;
			}
			string text = Path.Combine(_gameDataPath, pakFileName);
			if (!File.Exists(text))
			{
				Log.Error("[LOAD] {FileName}: PAK file not found: {PakPath}", normalizedPath, text);
				return null;
			}
			Log.Debug("[LOAD] {FileName}: Found in {PakFileName}", normalizedPath, pakFileName);
			using MemoryStream memoryStream = await _pakManager.ReadFileToMemoryAsync(text, normalizedPath);
			if (memoryStream == null)
			{
				Log.Warning("[LOAD] {FileName}: Failed to read from PAK {PakFileName}", normalizedPath, pakFileName);
				return null;
			}
			XDocument obj = await LoadFromStreamAsync(memoryStream, normalizedPath);
			if (obj != null)
			{
				Log.Information("[LOAD] {FileName}: Loaded vanilla baseline from {PakFileName}", normalizedPath, pakFileName);
			}
			return obj;
		}
		catch (Exception exception)
		{
			Log.Error(exception, "[LOAD ERROR] {FileName}: Failed to load vanilla file", fileName);
			return null;
		}
	}

	public DateTime GetVanillaTimestamp(string fileName)
	{
		try
		{
			string internalPath = fileName.Replace('\\', '/');
			string text = _vanillaIndexer.FindVanillaPak(internalPath);
			if (string.IsNullOrEmpty(text))
			{
				return DateTime.MinValue;
			}
			string path = Path.Combine(_gameDataPath, text);
			if (!File.Exists(path))
			{
				return DateTime.MinValue;
			}
			return File.GetLastWriteTimeUtc(path);
		}
		catch
		{
			return DateTime.MinValue;
		}
	}

	public DateTime GetVanillaEntryTimestamp(string fileName)
	{
		try
		{
			string internalPath = fileName.Replace('\\', '/');
			return _vanillaIndexer.GetVanillaEntryTimestamp(internalPath);
		}
		catch
		{
			return DateTime.MinValue;
		}
	}
}
