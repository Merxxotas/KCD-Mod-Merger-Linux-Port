using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using KCDMerge.Core.Configuration;
using Serilog;

namespace KCDMerge.Core.Data;

public class XmlWriter : IXmlWriter
{
	private readonly IConfigurationService _configService;

	public XmlWriter(IConfigurationService configService)
	{
		_configService = configService;
	}

	public async Task<string> WriteMergedDocumentAsync(string fileName, XDocument document)
	{
		if (document == null)
		{
			throw new ArgumentNullException("document");
		}
		string path = Path.Combine(_configService.LoadConfiguration().GetResolvedTempPath(), "Staging");
		string fullPath = Path.Combine(path, fileName);
		string directoryName = Path.GetDirectoryName(fullPath);
		if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		try
		{
			XmlWriterSettings settings = new XmlWriterSettings
			{
				Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
				Indent = true,
				IndentChars = "  ",
				OmitXmlDeclaration = false,
				Async = true
			};
			foreach (XText item in (from t in document.DescendantNodes().OfType<XText>()
				where string.IsNullOrWhiteSpace(t.Value)
				select t).ToList())
			{
				item.Remove();
			}
			using (FileStream fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
			{
				using System.Xml.XmlWriter xmlWriter = System.Xml.XmlWriter.Create(fileStream, settings);
				await document.SaveAsync(xmlWriter, default(CancellationToken));
			}
			FileInfo fileInfo = new FileInfo(fullPath);
			Log.Information("[WRITE] {FileName}: Written to staging ({Size} bytes)", fileName, fileInfo.Length);
			return fullPath;
		}
		catch (Exception exception)
		{
			Log.Error(exception, "[WRITE ERROR] {FileName}: Failed to write merged document", fileName);
			throw;
		}
	}

	public async Task<string> WriteMergedDocumentAsync(string fileName, XDocument document, bool isLocalization, string? languageCode)
	{
		if (document == null)
		{
			throw new ArgumentNullException("document");
		}
		string path = _configService.LoadConfiguration().GetResolvedTempPath();
		string fullPath;
		if (isLocalization && !string.IsNullOrEmpty(languageCode))
		{
			string path2 = Path.Combine(Path.Combine(path, "Staging_localization"), languageCode);
			fullPath = Path.Combine(path2, Path.GetFileName(fileName));
		}
		else
		{
			string path3 = Path.Combine(path, "Staging");
			fullPath = Path.Combine(path3, fileName);
		}
		string directoryName = Path.GetDirectoryName(fullPath);
		if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		try
		{
			XmlWriterSettings settings = new XmlWriterSettings
			{
				Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
				Indent = true,
				IndentChars = "  ",
				OmitXmlDeclaration = false,
				Async = true
			};
			foreach (XText item in (from t in document.DescendantNodes().OfType<XText>()
				where string.IsNullOrWhiteSpace(t.Value)
				select t).ToList())
			{
				item.Remove();
			}
			using (FileStream fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
			{
				using System.Xml.XmlWriter xmlWriter = System.Xml.XmlWriter.Create(fileStream, settings);
				await document.SaveAsync(xmlWriter, default(CancellationToken));
			}
			FileInfo fileInfo = new FileInfo(fullPath);
			string propertyValue = (isLocalization ? ("Localization/" + languageCode) : "Data");
			Log.Information("[WRITE] {FileName}: Written to staging/{Location} ({Size} bytes)", fileName, propertyValue, fileInfo.Length);
			return fullPath;
		}
		catch (Exception exception)
		{
			Log.Error(exception, "[WRITE ERROR] {FileName}: Failed to write merged document", fileName);
			throw;
		}
	}
}
