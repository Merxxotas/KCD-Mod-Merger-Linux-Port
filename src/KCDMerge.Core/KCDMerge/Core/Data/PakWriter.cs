using System.IO;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using KCDMerge.Core.Configuration;
using Serilog;

namespace KCDMerge.Core.Data;

public class PakWriter : IPakWriter
{
	private readonly IConfigurationService _configService;

	private readonly IStagingManager _stagingManager;

	public PakWriter(IConfigurationService configService, IStagingManager stagingManager)
	{
		_configService = configService;
		_stagingManager = stagingManager;
	}

	public async Task CreatePakAsync(string outputPath)
	{
		string stagingPath = _stagingManager.GetStagingPath();
		if (!Directory.Exists(stagingPath))
		{
			throw new DirectoryNotFoundException("Staging directory not found at " + stagingPath + ". Cannot create PAK.");
		}
		string directoryName = Path.GetDirectoryName(outputPath);
		if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		if (File.Exists(outputPath))
		{
			File.Delete(outputPath);
		}
		await Task.Run(delegate
		{
			using FileStream baseOutputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
			using ZipOutputStream zipOutputStream = new ZipOutputStream(baseOutputStream);
			zipOutputStream.UseZip64 = UseZip64.On;
			zipOutputStream.SetLevel(0);
			AddDirectoryToZip(zipOutputStream, stagingPath, stagingPath);
			zipOutputStream.Finish();
			Log.Debug("[PAK] Created PAK with Zip64 support: {OutputPath}", outputPath);
		});
	}

	private void AddDirectoryToZip(ZipOutputStream zipStream, string sourceDir, string rootDir)
	{
		string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
		foreach (string text in files)
		{
			ZipEntry entry = new ZipEntry(Path.GetRelativePath(rootDir, text).Replace('\\', '/'))
			{
				DateTime = File.GetLastWriteTime(text),
				Size = new FileInfo(text).Length
			};
			zipStream.PutNextEntry(entry);
			using FileStream fileStream = new FileStream(text, FileMode.Open, FileAccess.Read, FileShare.Read);
			fileStream.CopyTo(zipStream);
			zipStream.CloseEntry();
		}
	}
}
