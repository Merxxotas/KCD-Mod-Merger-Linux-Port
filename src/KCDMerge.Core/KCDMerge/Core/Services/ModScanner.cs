using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using KCDMerge.Core.Configuration;
using KCDMerge.Core.Models;
using Serilog;

namespace KCDMerge.Core.Services;

public class ModScanner : IModScanner
{
	private readonly IConfigurationService _configService;

	public ModScanner(IConfigurationService configService)
	{
		_configService = configService;
	}

	public IEnumerable<ModInfo> ScanMods()
	{
		AppConfig appConfig = _configService.LoadConfiguration();
		string modsPath = appConfig.ModsPath;
		if (!Directory.Exists(modsPath))
		{
			return Enumerable.Empty<ModInfo>();
		}
		List<string> orderList = LoadModOrder(modsPath);
		string[] directories = Directory.GetDirectories(modsPath);
		List<ModInfo> list = new List<ModInfo>();
		string[] array = directories;
		foreach (string dir in array)
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(dir);
			string name = directoryInfo.Name;
			if (!string.IsNullOrEmpty(appConfig.OutputPath))
			{
				string b = (Path.IsPathRooted(appConfig.OutputPath) ? appConfig.OutputPath : Path.GetFullPath(Path.Combine(modsPath, appConfig.OutputPath)));
				if (string.Equals(directoryInfo.FullName, b, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
			}
			List<string> list2 = Directory.GetFiles(dir, "*.pak", SearchOption.AllDirectories).ToList();
			Dictionary<string, List<string>> dictionary = new Dictionary<string, List<string>>();
			bool isNonStandardStructure = false;
			List<string> list3 = list2.Where((string p) => !Path.GetDirectoryName(p).Equals(dir, StringComparison.OrdinalIgnoreCase)).ToList();
			if (list3.Any())
			{
				Log.Information("Mod {ModName}: Detected {Count} deep PAK(s) in subdirectories", name, list3.Count);
				isNonStandardStructure = true;
			}
			if (!list2.Any())
			{
				List<string> list4 = Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories).ToList();
				if (list4.Any())
				{
					foreach (string item in list4)
					{
						string directoryName = Path.GetDirectoryName(item);
						string key = Path.GetRelativePath(dir, directoryName).Replace('\\', '/');
						if (!dictionary.ContainsKey(key))
						{
							dictionary[key] = new List<string>();
						}
						dictionary[key].Add(Path.GetFileName(item));
					}
					Log.Warning("Mod {ModName}: No PAKs found, but has {Count} loose XML files in {FolderCount} folders", name, list4.Count, dictionary.Count);
					Log.Warning("This mod may require manual repacking or have incomplete structure");
					isNonStandardStructure = true;
				}
			}
			List<string> list5 = Directory.GetFiles(dir, "*.cfg", SearchOption.AllDirectories).ToList();
			if (list5.Any())
			{
				Log.Information("Mod {ModName}: Found {Count} .cfg file(s)", name, list5.Count);
			}
			string text = ExtractModNameFromFolder(dir) ?? ExtractModNameFromManifest(list2.FirstOrDefault());
			if (text == null)
			{
				text = name;
				Match match = Regex.Match(name, "^(.+?)(\\d{5,})$");
				if (match.Success)
				{
					text = match.Groups[1].Value;
					Log.Debug("Applied folder name fallback: {FolderName} -> {DisplayName}", name, text);
				}
			}
			ModInfo modInfo = new ModInfo
			{
				Name = name,
				DisplayName = text,
				RootPath = dir,
				PakFiles = list2,
				LooseFileFolders = dictionary,
				IsNonStandardStructure = isNonStandardStructure,
				CfgFiles = list5,
				IsValid = (list2.Any() || dictionary.Any() || list5.Any()),
				LoadPriority = GetPriority(name, orderList)
			};
			if (modInfo.IsValid)
			{
				list.Add(modInfo);
			}
		}
		return from m in list
			orderby m.LoadPriority, m.Name
			select m;
	}

	private List<string> LoadModOrder(string modsPath)
	{
		string path = Path.Combine(modsPath, "mod_order.txt");
		string path2 = Path.Combine(modsPath, "mod_order.txt.backup");
		if (!File.Exists(path))
		{
			return new List<string>();
		}
		try
		{
			List<string> list = (from l in File.ReadAllLines(path)
				where !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#")
				select l.Trim()).ToList();
			if (list.Where((string e) => !e.Equals("KCDMerge", StringComparison.OrdinalIgnoreCase)).ToList().Count == 0 && File.Exists(path2))
			{
				Log.Information("mod_order.txt contains only KCDMerge or is empty - loading from backup");
				try
				{
					list = (from l in File.ReadAllLines(path2)
						where !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#")
						select l.Trim()).ToList();
				}
				catch (Exception exception)
				{
					Log.Warning(exception, "Failed to read mod_order.txt.backup");
				}
			}
			return list;
		}
		catch
		{
			return new List<string>();
		}
	}

	private int GetPriority(string modName, List<string> orderList)
	{
		int num = orderList.IndexOf(modName);
		if (num == -1)
		{
			return int.MaxValue;
		}
		return num;
	}

	private string? ExtractModNameFromFolder(string modFolder)
	{
		string path = Path.Combine(modFolder, "mod.manifest");
		if (!File.Exists(path))
		{
			return null;
		}
		try
		{
			byte[] array = File.ReadAllBytes(path);
			Encoding encoding = ((array.Length >= 2 && array[0] == byte.MaxValue && array[1] == 254) ? Encoding.Unicode : ((array.Length < 2 || array[0] != 254 || array[1] != byte.MaxValue) ? Encoding.UTF8 : Encoding.BigEndianUnicode));
			string input;
			using (MemoryStream stream = new MemoryStream(array))
			{
				using StreamReader streamReader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
				input = streamReader.ReadToEnd();
			}
			input = Regex.Replace(input, "<\\?xml\\s+version=\"2\\.0\"", "<?xml version=\"1.0\"", RegexOptions.IgnoreCase);
			XElement xElement = XDocument.Parse(input).Root?.Element("info")?.Element("name");
			if (xElement != null && !string.IsNullOrWhiteSpace(xElement.Value))
			{
				Log.Debug("Extracted mod name '{ModName}' from mod.manifest in {ModFolder}", xElement.Value, Path.GetFileName(modFolder));
				return xElement.Value;
			}
		}
		catch (Exception exception)
		{
			Log.Debug(exception, "Failed to extract mod name from mod.manifest in {ModFolder}", Path.GetFileName(modFolder));
		}
		return null;
	}

	private string? ExtractModNameFromManifest(string? pakPath)
	{
		if (string.IsNullOrEmpty(pakPath) || !File.Exists(pakPath))
		{
			return null;
		}
		try
		{
			using ZipArchive zipArchive = ZipFile.OpenRead(pakPath);
			ZipArchiveEntry zipArchiveEntry = zipArchive.Entries.FirstOrDefault((ZipArchiveEntry e) => e.FullName.Equals("mod.manifest", StringComparison.OrdinalIgnoreCase));
			if (zipArchiveEntry == null)
			{
				return null;
			}
			using Stream stream = zipArchiveEntry.Open();
			XElement xElement = XDocument.Load(stream).Root?.Element("info")?.Element("name");
			if (xElement != null && !string.IsNullOrWhiteSpace(xElement.Value))
			{
				Log.Debug("Extracted mod name '{ModName}' from {PakFile}", xElement.Value, Path.GetFileName(pakPath));
				return xElement.Value;
			}
		}
		catch (Exception exception)
		{
			Log.Debug(exception, "Failed to extract mod name from {PakFile}", Path.GetFileName(pakPath));
		}
		return null;
	}
}
