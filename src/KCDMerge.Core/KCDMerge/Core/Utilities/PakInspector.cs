using System;
using System.IO;
using System.IO.Compression;
using Serilog;

namespace KCDMerge.Core.Utilities;

public static class PakInspector
{
	public static void Inspect(string pakPath)
	{
		Log.Information("--- Inspecting: {PakPath} ---", Path.GetFileName(pakPath));
		if (!File.Exists(pakPath))
		{
			Log.Error("File not found: {PakPath}", pakPath);
			return;
		}
		try
		{
			using FileStream stream = new FileStream(pakPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using ZipArchive zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
			Log.Information("Entry Count: {EntryCount}", zipArchive.Entries.Count);
			foreach (ZipArchiveEntry entry in zipArchive.Entries)
			{
				Log.Information(" - {EntryName} ({EntryLength} bytes)", entry.FullName, entry.Length);
			}
		}
		catch (InvalidDataException)
		{
			Log.Error("{PakPath} is not a valid Zip/Pak archive.", pakPath);
		}
		catch (Exception exception)
		{
			Log.Error(exception, "Failed to read {PakPath}", pakPath);
		}
		Log.Information("---------------------------------------------");
	}
}
