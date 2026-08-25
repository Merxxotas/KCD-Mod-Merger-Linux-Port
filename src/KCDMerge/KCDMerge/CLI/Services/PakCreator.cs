using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Spectre.Console;

namespace KCDMerge.CLI.Services;

public class PakCreator
{
	private const long MaxPakSize = 2097152000L;

	public async Task CreateDataPaksAsync(string stagingPath, string dataFolderPath, string modFolderName)
	{
		string[] files = Directory.GetFiles(dataFolderPath, modFolderName + "*.pak");
		foreach (string path in files)
		{
			try
			{
				File.Delete(path);
			}
			catch
			{
			}
		}
		List<List<(string FullPath, string RelativePath, long Size)>> buckets = BuildFileBuckets(stagingPath);
		Log.Information("PAK creation: {FileCount} files -> {BucketCount} PAK(s)", buckets.SelectMany((List<(string FullPath, string RelativePath, long Size)> b) => b).Count(), buckets.Count);
		for (int j = 0; j < buckets.Count; j++)
		{
			string pakName = ((buckets.Count == 1) ? (modFolderName + ".pak") : $"{modFolderName}-part{j}.pak");
			string text = Path.Combine(dataFolderPath, pakName);
			while (File.Exists(text))
			{
				try
				{
					using (new FileStream(text, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
					{
					}
				}
				catch (IOException)
				{
					AnsiConsole.MarkupLine("[red bold]ERROR:[/] [red]" + pakName + " is locked by another process.[/]");
					AnsiConsole.MarkupLine("[yellow]Close Kingdom Come: Deliverance or any program using the file.[/]");
					AnsiConsole.MarkupLine("[yellow]Press any key to retry, or Ctrl+C to abort...[/]");
					Console.ReadKey(intercept: true);
					continue;
				}
				break;
			}
			string text2 = Path.Combine(stagingPath, $"_bucket{j}");
			if (Directory.Exists(text2))
			{
				Directory.Delete(text2, recursive: true);
			}
			Directory.CreateDirectory(text2);
			foreach (var item in buckets[j])
			{
				string text3 = Path.Combine(text2, item.RelativePath.Replace('/', Path.DirectorySeparatorChar));
				string directoryName = Path.GetDirectoryName(text3);
				if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
				File.Copy(item.FullPath, text3, overwrite: true);
			}
			await CreatePakFromFolderAsync(text2, text);
			Log.Information("Created PAK: {PakName} ({FileCount} files)", pakName, buckets[j].Count);
		}
	}

	public async Task CreatePakFromFolderAsync(string sourceFolder, string outputPakPath)
	{
		string path = Path.GetDirectoryName(outputPakPath) ?? ".";
		if (!Directory.Exists(path))
		{
			Directory.CreateDirectory(path);
		}
		string fileName = Path.GetFileName(outputPakPath);
		while (File.Exists(outputPakPath))
		{
			try
			{
				File.Delete(outputPakPath);
			}
			catch (IOException)
			{
				AnsiConsole.MarkupLine("[red bold]ERROR:[/] [red]" + fileName + " is locked by another process.[/]");
				AnsiConsole.MarkupLine("[yellow]Close Kingdom Come: Deliverance or any program using the file.[/]");
				AnsiConsole.MarkupLine("[yellow]Press any key to retry, or Ctrl+C to abort...[/]");
				Console.ReadKey(intercept: true);
				continue;
			}
			break;
		}
		await Task.Run(delegate
		{
			ZipFile.CreateFromDirectory(sourceFolder, outputPakPath, CompressionLevel.NoCompression, includeBaseDirectory: false);
		});
	}

	public List<List<(string FullPath, string RelativePath, long Size)>> BuildFileBuckets(string sourceFolder)
	{
		if (!Directory.Exists(sourceFolder))
		{
			return new List<List<(string FullPath, string RelativePath, long Size)>>();
		}
		var list = (from f in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories)
			where !Path.GetRelativePath(sourceFolder, f).StartsWith("_bucket", StringComparison.OrdinalIgnoreCase)
			select (FullPath: f, RelativePath: Path.GetRelativePath(sourceFolder, f).Replace('\\', '/'), Size: new FileInfo(f).Length)).ToList();
		HashSet<string> hashSet = new HashSet<string>(from f in list
			where f.RelativePath.EndsWith(".tbl", StringComparison.OrdinalIgnoreCase)
			select f.RelativePath, StringComparer.OrdinalIgnoreCase);
		List<List<(string FullPath, string RelativePath, long Size)>> list2 = new List<List<(string FullPath, string RelativePath, long Size)>>();
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var item2 in list)
		{
			if (hashSet2.Contains(item2.RelativePath))
			{
				continue;
			}
			List<(string FullPath, string RelativePath, long Size)> list3 = new List<(string FullPath, string RelativePath, long Size)> { item2 };
			hashSet2.Add(item2.RelativePath);
			if (item2.RelativePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
			{
				string matchingTbl = Path.ChangeExtension(item2.RelativePath, ".tbl");
				if (hashSet.Contains(matchingTbl) && !hashSet2.Contains(matchingTbl))
				{
					var item = list.First(f => f.RelativePath.Equals(matchingTbl, StringComparison.OrdinalIgnoreCase));
					list3.Add(item);
					hashSet2.Add(matchingTbl);
				}
			}
			list2.Add(list3);
		}
		List<List<(string FullPath, string RelativePath, long Size)>> list4 = new List<List<(string FullPath, string RelativePath, long Size)>>();
		List<(string FullPath, string RelativePath, long Size)> list5 = new List<(string FullPath, string RelativePath, long Size)>();
		long num = 0L;
		foreach (var item3 in list2)
		{
			long num2 = item3.Sum(f => f.Size);
			if (list5.Count > 0 && num + num2 > 2097152000)
			{
				list4.Add(list5);
				list5 = new List<(string FullPath, string RelativePath, long Size)>();
				num = 0L;
			}
			list5.AddRange(item3);
			num += num2;
		}
		if (list5.Count > 0)
		{
			list4.Add(list5);
		}
		if (list4.Count == 0)
		{
			list4.Add(new List<(string FullPath, string RelativePath, long Size)>());
		}
		return list4;
	}
}
