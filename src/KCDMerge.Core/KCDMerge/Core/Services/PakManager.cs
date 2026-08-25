using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Serilog;

namespace KCDMerge.Core.Services;

public class PakManager : IPakManager
{
	public IEnumerable<string> GetPakEntries(string pakPath)
	{
		if (!File.Exists(pakPath))
		{
			throw new FileNotFoundException("PAK file not found: " + pakPath);
		}
		FileInfo fileInfo = new FileInfo(pakPath);
		if (fileInfo.Length < 22)
		{
			Log.Error("[PAK] File too small to be a valid PAK: {PakPath} ({Size} bytes)", pakPath, fileInfo.Length);
			throw new InvalidDataException($"PAK file is too small to be valid: {pakPath} ({fileInfo.Length} bytes). The file may be corrupted or incomplete.");
		}
		List<string> list = new List<string>();
		try
		{
			using FileStream file = new FileStream(pakPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using ZipFile zipFile = new ZipFile(file);
			foreach (ZipEntry item in zipFile)
			{
				if (!item.IsDirectory)
				{
					list.Add(item.Name);
				}
			}
			return list;
		}
		catch (ZipException ex)
		{
			Log.Error(ex, "[PAK] Failed to read PAK file: {PakPath}", pakPath);
			if (ex.Message.Contains("central directory"))
			{
				throw new InvalidDataException($"PAK file is corrupted or invalid: {Path.GetFileName(pakPath)}\nThis usually means:\n  • The file was not downloaded completely\n  • The file is damaged or corrupted\n  • The file is not a valid Kingdom Come PAK file\n\nTry re-downloading the mod or excluding this PAK from the merge.", ex);
			}
			if (ex.Message.Contains("Wrong Local Header"))
			{
				throw new InvalidDataException("PAK file structure is invalid: " + Path.GetFileName(pakPath) + "\nThe file appears to be corrupted. Try re-downloading the mod.", ex);
			}
			throw new InvalidDataException($"Failed to read PAK file: {Path.GetFileName(pakPath)}\nError: {ex.Message}\n\nThe file may be corrupted or not a valid PAK format.", ex);
		}
		catch (UnauthorizedAccessException ex2)
		{
			Log.Error(ex2, "[PAK] Access denied to PAK file: {PakPath}", pakPath);
			throw new UnauthorizedAccessException("Cannot access PAK file: " + Path.GetFileName(pakPath) + "\nThe file may be in use by another application or you don't have permission to read it.", ex2);
		}
		catch (IOException ex3)
		{
			Log.Error(ex3, "[PAK] IO error reading PAK file: {PakPath}", pakPath);
			throw new IOException("Failed to read PAK file: " + Path.GetFileName(pakPath) + "\nThe file may be locked by another process or the disk could be full.", ex3);
		}
	}

	public IEnumerable<(string EntryName, DateTime ModifiedDate)> GetPakEntriesWithTimestamps(string pakPath)
	{
		if (!File.Exists(pakPath))
		{
			throw new FileNotFoundException("PAK file not found: " + pakPath);
		}
		FileInfo fileInfo = new FileInfo(pakPath);
		if (fileInfo.Length < 22)
		{
			Log.Error("[PAK] File too small to be a valid PAK: {PakPath} ({Size} bytes)", pakPath, fileInfo.Length);
			throw new InvalidDataException($"PAK file is too small to be valid: {pakPath} ({fileInfo.Length} bytes). The file may be corrupted or incomplete.");
		}
		List<(string, DateTime)> list = new List<(string, DateTime)>();
		try
		{
			using FileStream file = new FileStream(pakPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using ZipFile zipFile = new ZipFile(file);
			foreach (ZipEntry item in zipFile)
			{
				if (!item.IsDirectory)
				{
					list.Add((item.Name, item.DateTime));
				}
			}
			return list;
		}
		catch (ZipException ex)
		{
			Log.Error(ex, "[PAK] Failed to read PAK file: {PakPath}", pakPath);
			if (ex.Message.Contains("central directory"))
			{
				throw new InvalidDataException($"PAK file is corrupted or invalid: {Path.GetFileName(pakPath)}\nThis usually means:\n  • The file was not downloaded completely\n  • The file is damaged or corrupted\n  • The file is not a valid Kingdom Come PAK file\n\nTry re-downloading the mod or excluding this PAK from the merge.", ex);
			}
			if (ex.Message.Contains("Wrong Local Header"))
			{
				throw new InvalidDataException("PAK file structure is invalid: " + Path.GetFileName(pakPath) + "\nThe file appears to be corrupted. Try re-downloading the mod.", ex);
			}
			throw new InvalidDataException($"Failed to read PAK file: {Path.GetFileName(pakPath)}\nError: {ex.Message}\n\nThe file may be corrupted or not a valid PAK format.", ex);
		}
		catch (UnauthorizedAccessException ex2)
		{
			Log.Error(ex2, "[PAK] Access denied to PAK file: {PakPath}", pakPath);
			throw new UnauthorizedAccessException("Cannot access PAK file: " + Path.GetFileName(pakPath) + "\nThe file may be in use by another application or you don't have permission to read it.", ex2);
		}
		catch (IOException ex3)
		{
			Log.Error(ex3, "[PAK] IO error reading PAK file: {PakPath}", pakPath);
			throw new IOException("Failed to read PAK file: " + Path.GetFileName(pakPath) + "\nThe file may be locked by another process or the disk could be full.", ex3);
		}
	}

	public async Task<bool> ExtractFileAsync(string pakPath, string entryName, string destinationPath)
	{
		if (!File.Exists(pakPath))
		{
			return false;
		}
		try
		{
			using FileStream fs = new FileStream(pakPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using ZipFile zipFile = new ZipFile(fs);
			ZipEntry zipEntry = zipFile.GetEntry(entryName);
			if (zipEntry == null)
			{
				zipEntry = zipFile.Cast<ZipEntry>().FirstOrDefault((ZipEntry e) => e.Name.Equals(entryName, StringComparison.OrdinalIgnoreCase));
			}
			if (zipEntry == null)
			{
				return false;
			}
			string directoryName = Path.GetDirectoryName(destinationPath);
			if (!string.IsNullOrEmpty(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			using Stream entryStream = zipFile.GetInputStream(zipEntry);
			using FileStream destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
			await entryStream.CopyToAsync(destStream);
			return true;
		}
		catch (ZipException ex)
		{
			Log.Error(ex, "[PAK] Failed to extract {Entry} from {PakPath}", entryName, Path.GetFileName(pakPath));
			throw new InvalidDataException("Failed to extract file from PAK: " + Path.GetFileName(pakPath) + "\nThe PAK file appears to be corrupted.", ex);
		}
		catch (Exception ex2) when (!(ex2 is InvalidDataException))
		{
			Log.Error(ex2, "[PAK] Unexpected error extracting {Entry} from {PakPath}", entryName, Path.GetFileName(pakPath));
			throw;
		}
	}

	public async Task<MemoryStream?> ReadFileToMemoryAsync(string pakPath, string entryName)
	{
		if (!File.Exists(pakPath))
		{
			return null;
		}
		if (!pakPath.EndsWith(".pak", StringComparison.OrdinalIgnoreCase))
		{
			MemoryStream ms = new MemoryStream();
			using (FileStream fs = new FileStream(pakPath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				await fs.CopyToAsync(ms);
			}
			ms.Position = 0L;
			return ms;
		}
		try
		{
			using FileStream fs = new FileStream(pakPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using ZipFile zipFile = new ZipFile(fs);
			ZipEntry zipEntry = zipFile.GetEntry(entryName);
			if (zipEntry == null)
			{
				zipEntry = zipFile.Cast<ZipEntry>().FirstOrDefault((ZipEntry e) => e.Name.Equals(entryName, StringComparison.OrdinalIgnoreCase));
			}
			if (zipEntry == null)
			{
				return null;
			}
			if (zipEntry.Size > 1500000000)
			{
				Log.Error("[PAK] File {Entry} is too large ({Size:N0} bytes) to load into memory from {PakPath}", entryName, zipEntry.Size, Path.GetFileName(pakPath));
				throw new InvalidOperationException($"File '{entryName}' is too large ({zipEntry.Size:N0} bytes) to load into memory. " + "MemoryStream is limited to ~2GB. Use ExtractFileAsync() to stream to disk instead.");
			}
			MemoryStream ms = new MemoryStream();
			using (Stream entryStream = zipFile.GetInputStream(zipEntry))
			{
				await entryStream.CopyToAsync(ms);
			}
			ms.Position = 0L;
			return ms;
		}
		catch (ZipException ex)
		{
			Log.Error(ex, "[PAK] Failed to read {Entry} from {PakPath}", entryName, Path.GetFileName(pakPath));
			throw new InvalidDataException("Failed to read file from PAK: " + Path.GetFileName(pakPath) + "\nThe PAK file appears to be corrupted.", ex);
		}
		catch (Exception ex2) when (!(ex2 is InvalidDataException))
		{
			Log.Error(ex2, "[PAK] Unexpected error reading {Entry} from {PakPath}", entryName, Path.GetFileName(pakPath));
			throw;
		}
	}

	public async Task<bool> IsXmlContentAsync(string pakPath, string entryName)
	{
		if (!File.Exists(pakPath))
		{
			return false;
		}
		try
		{
			using FileStream fs = new FileStream(pakPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using ZipFile zipFile = new ZipFile(fs);
			ZipEntry zipEntry = zipFile.GetEntry(entryName);
			if (zipEntry == null)
			{
				zipEntry = zipFile.Cast<ZipEntry>().FirstOrDefault((ZipEntry e) => e.Name.Equals(entryName, StringComparison.OrdinalIgnoreCase));
			}
			if (zipEntry == null)
			{
				return false;
			}
			using Stream entryStream = zipFile.GetInputStream(zipEntry);
			byte[] buffer = new byte[64];
			int num = await entryStream.ReadAsync(buffer, 0, buffer.Length);
			if (num == 0)
			{
				return false;
			}
			int num2 = 0;
			if (num >= 3 && buffer[0] == 239 && buffer[1] == 187 && buffer[2] == 191)
			{
				num2 = 3;
			}
			else if (num >= 2 && ((buffer[0] == byte.MaxValue && buffer[1] == 254) || (buffer[0] == 254 && buffer[1] == byte.MaxValue)))
			{
				num2 = 2;
			}
			string @string = Encoding.UTF8.GetString(buffer, num2, num - num2);
			@string = @string.TrimStart();
			return @string.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) || @string.StartsWith("<", StringComparison.Ordinal);
		}
		catch (Exception exception)
		{
			Log.Debug(exception, "[PAK] Failed to check XML content for {Entry} in {PakPath}", entryName, Path.GetFileName(pakPath));
			return false;
		}
	}
}
