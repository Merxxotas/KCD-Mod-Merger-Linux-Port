using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KCDMerge.Core.Models;
using KCDMerge.Core.Services;
using Serilog;
using Spectre.Console;

namespace KCDMerge.CLI.Services;

public class ModScanPresenter
{
	public List<ModInfo>? ScanAndDisplayMods(ModScanner modScanner, IPakManager pakManager)
	{
		Log.Information("\u001b[33mScanning mods...\u001b[0m");
		List<ModInfo> list = modScanner.ScanMods().ToList();
		Log.Information("Found {ModCount} mods", list.Count);
		if (list.Count == 0)
		{
			AnsiConsole.MarkupLine("[yellow]No mods found to merge. Output PAK will be empty.[/]");
			AnsiConsole.MarkupLine("[yellow]Press any key to exit...[/]");
			Console.ReadKey();
			return null;
		}
		List<(string, string, string, int, int)> list2 = new List<(string, string, string, int, int)>();
		foreach (ModInfo item2 in list.OrderBy((ModInfo m) => m.LoadPriority))
		{
			int num = 0;
			int num2 = 0;
			foreach (string pakFile in item2.PakFiles)
			{
				try
				{
					foreach (string pakEntry in pakManager.GetPakEntries(pakFile))
					{
						if (!pakEntry.EndsWith("/") && !pakEntry.EndsWith("\\"))
						{
							if (pakEntry.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
							{
								num++;
							}
							else
							{
								num2++;
							}
						}
					}
				}
				catch (Exception ex)
				{
					Log.Warning(ex, "Failed to scan PAK {PakFile} for mod {ModName}", Path.GetFileName(pakFile), item2.DisplayName);
					AnsiConsole.MarkupLine($"[yellow]Warning: Could not scan PAK {Path.GetFileName(pakFile)} - {ex.Message}[/]");
				}
			}
			string item = ((item2.LoadPriority == int.MaxValue) ? "default" : item2.LoadPriority.ToString());
			list2.Add((item2.DisplayName, Path.GetFileName(item2.PakFiles.FirstOrDefault() ?? "No PAK"), item, num, num2));
		}
		list2 = list2.OrderBy<(string, string, string, int, int), int>(((string Name, string PakFile, string Priority, int XmlCount, int AssetCount) s) => (!(s.Priority == "default")) ? int.Parse(s.Priority) : int.MaxValue).ToList();
		AnsiConsole.MarkupLine($"[green]Found {list.Count} mods:[/]");
		Table table = new Table();
		table.Border(TableBorder.MinimalHeavyHead);
		table.AddColumn("[bold]Mod Name[/]");
		table.AddColumn("[bold]PAK File[/]");
		table.AddColumn("[bold]Priority[/]");
		table.AddColumn("[bold]XML Files[/]");
		table.AddColumn("[bold]Asset Files[/]");
		foreach (var item3 in list2)
		{
			table.AddRow("[cyan]" + item3.Item1 + "[/]", item3.Item2, item3.Item3, $"[green]{item3.Item4}[/]", $"[blue]{item3.Item5}[/]");
		}
		AnsiConsole.Write(table);
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[yellow]Checking PAK file accessibility...[/]");
		foreach (ModInfo item4 in list)
		{
			foreach (string pakFile2 in item4.PakFiles)
			{
				try
				{
					using (new FileStream(pakFile2, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
					}
				}
				catch (IOException ex2)
				{
					AnsiConsole.MarkupLine("[red bold]ERROR:[/] [red]Cannot access PAK file: " + Path.GetFileName(pakFile2) + "[/]");
					AnsiConsole.MarkupLine("[red]From mod: " + item4.DisplayName + "[/]");
					AnsiConsole.MarkupLine("[red]Error: " + ex2.Message + "[/]");
					AnsiConsole.MarkupLine("[yellow]The file may be locked by another process (is the game running?).[/]");
					AnsiConsole.MarkupLine("[yellow]Close the game or any other application using this file and try again.[/]");
					if (ConsoleEnvironment.IsStartedFromExplorer())
					{
						AnsiConsole.MarkupLine("[yellow]Press any key to exit...[/]");
						Console.ReadKey(intercept: true);
					}
					return null;
				}
				catch (UnauthorizedAccessException ex3)
				{
					AnsiConsole.MarkupLine("[red bold]ERROR:[/] [red]Access denied to PAK file: " + Path.GetFileName(pakFile2) + "[/]");
					AnsiConsole.MarkupLine("[red]From mod: " + item4.DisplayName + "[/]");
					AnsiConsole.MarkupLine("[red]Error: " + ex3.Message + "[/]");
					AnsiConsole.MarkupLine("[yellow]Check file permissions or run as administrator.[/]");
					if (ConsoleEnvironment.IsStartedFromExplorer())
					{
						AnsiConsole.MarkupLine("[yellow]Press any key to exit...[/]");
						Console.ReadKey(intercept: true);
					}
					return null;
				}
			}
		}
		AnsiConsole.MarkupLine("[green]All PAK files are accessible.[/]");
		AnsiConsole.WriteLine();
		return list;
	}
}
