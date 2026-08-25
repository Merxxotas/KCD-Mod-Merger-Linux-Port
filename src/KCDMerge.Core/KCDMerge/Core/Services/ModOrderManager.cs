using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;

namespace KCDMerge.Core.Services;

public class ModOrderManager
{
	private readonly IBackupService? _backupService;

	public ModOrderManager(IBackupService? backupService = null)
	{
		_backupService = backupService;
	}

	public void UpdateModOrder(string modsPath, string mergedModName)
	{
		UpdateModOrder(modsPath, new string[1] { mergedModName });
	}

	public void UpdateModOrder(string modsPath, IEnumerable<string> mergedModNames)
	{
		List<string> modNames = mergedModNames.ToList();
		string text = Path.Combine(modsPath, "mod_order.txt");
		try
		{
			if (File.Exists(text))
			{
				List<string> list = (from l in File.ReadAllLines(text)
					where !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#")
					select l.Trim()).ToList();
				if (list.Where((string l) => !modNames.Any((string m) => m.Equals(l, StringComparison.OrdinalIgnoreCase))).ToList().Count > 0)
				{
					if (_backupService != null)
					{
						_backupService.BackupFile(text);
					}
					else
					{
						File.WriteAllLines(Path.Combine(modsPath, "mod_order.txt.backup"), list);
						Log.Information("Backed up mod_order.txt to mod_order.txt.backup ({Count} entries)", list.Count);
					}
				}
			}
			File.WriteAllLines(text, modNames);
			Log.Information("Updated mod_order.txt - {Count} mod(s): {Names}", modNames.Count, string.Join(", ", modNames));
		}
		catch (Exception exception)
		{
			Log.Warning(exception, "Failed to update mod_order.txt");
		}
	}
}
