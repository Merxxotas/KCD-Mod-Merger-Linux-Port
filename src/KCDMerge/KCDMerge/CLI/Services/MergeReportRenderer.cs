using System;
using System.Collections.Generic;
using System.Linq;
using KCDMerge.Core.Data;
using KCDMerge.Core.Models;
using Serilog;
using Spectre.Console;

namespace KCDMerge.CLI.Services;

public class MergeReportRenderer
{
	public void Render(MergeReport mergeReport, IReadOnlyList<ModInfo> mods)
	{
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[bold cyan]Merge Summary:[/]");
		HashSet<string> processedMods = new HashSet<string>(mergeReport.FileStatistics.Values.Select((FileStats s) => s.ModName), StringComparer.OrdinalIgnoreCase);
		foreach (ModInfo item in mods.Where((ModInfo m) => !processedMods.Contains(m.DisplayName)))
		{
			mergeReport.RecordFileStats("(Conflict victim)", item.DisplayName, item.LoadPriority, isPatchFile: false, isIdOnlyTable: false, isStandardTable: false, 0, 0, 0, 0, 0);
		}
		if (mergeReport.FileStatistics.Count > 0)
		{
			Table table = new Table();
			table.Border = TableBorder.MinimalHeavyHead;
			table.AddColumn("[bold]Mod[/]");
			table.AddColumn("[bold]File[/]");
			table.AddColumn("[bold]PTF[/]");
			table.AddColumn("[bold]ID[/]");
			table.AddColumn("[bold]Add[/]");
			table.AddColumn("[bold]Del[/]");
			table.AddColumn("[bold]Cha[/]");
			table.AddColumn("[bold]Warn[/]");
			table.AddColumn("[bold]Err[/]");
			List<FileStats> source = mergeReport.FileStatistics.Values.ToList();
			Log.Debug("Merge Summary Stats (first 5 entries, before sorting):");
			foreach (FileStats item2 in source.Take(5))
			{
				Log.Debug("  {ModName}: Priority={Priority}, File={FileName}", item2.ModName, item2.Priority, item2.FileName);
			}
			List<FileStats> list = (from s in source
				orderby s.Priority, (s.FileName == "(Assets)") ? 1 : 0, s.FileName
				select s).ToList();
			Log.Debug("After sorting (first 5 entries):");
			foreach (FileStats item3 in list.Take(5))
			{
				Log.Debug("  {ModName}: Priority={Priority}, File={FileName}", item3.ModName, item3.Priority, item3.FileName);
			}
			int num = 0;
			string text = null;
			foreach (FileStats item4 in list)
			{
				num++;
				if (num <= 5)
				{
					Log.Information("Adding row {RowNum}: {ModName} (Priority={Priority})", num, item4.ModName, item4.Priority);
				}
				if (text != null && text != item4.ModName)
				{
					table.AddEmptyRow();
				}
				text = item4.ModName;
				string modName = item4.ModName;
				bool flag = item4.FileName == "(Assets)";
				bool flag2 = item4.FileName == "(Conflict victim)";
				bool flag3 = item4.FileName == "(CFG)";
				string text2 = ((!flag) ? ((!flag2) ? ((!flag3) ? ((item4.FileName.Length > 40) ? ("..." + item4.FileName.Substring(item4.FileName.Length - 37)) : item4.FileName) : "[green](CFG)[/]") : "[red](Conflict victim)[/]") : "[blue](Assets)[/]");
				string text3 = ((flag || flag2 || flag3) ? "" : (item4.IsPatchFile ? "[green]Yes[/]" : "[red]No[/]"));
				string text4 = ((!item4.IsStandardTable) ? ((!item4.IsIdOnlyTable) ? "" : "[red]ID[/]") : "[green]ID+VAL[/]");
				string value = ((flag || flag3) ? ((item4.Additions > 0) ? "green" : "dim") : ((item4.Additions > 0) ? "green" : "dim"));
				table.AddRow(modName, text2, text3, text4, (flag || item4.Additions > 0) ? $"[{value}]{item4.Additions}[/]" : "", (item4.Deletions > 0) ? $"[red]{item4.Deletions}[/]" : "", (item4.Modifications > 0) ? $"[yellow]{item4.Modifications}[/]" : "", (item4.Warnings > 0) ? $"[yellow]{item4.Warnings}[/]" : "", (item4.Errors > 0) ? $"[red]{item4.Errors}[/]" : "");
			}
			AnsiConsole.Write(table);
		}
		if (mergeReport.Errors.Count > 0)
		{
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine($"[red bold]⚠ {mergeReport.Errors.Count} error(s) occurred during merge[/]");
		}
		if (mergeReport.Warnings.Count > 0 && mergeReport.Warnings.Count <= 10)
		{
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine($"[yellow]Warnings ({mergeReport.Warnings.Count}):[/]");
			foreach (string warning in mergeReport.Warnings)
			{
				string text5 = warning.Replace("[", "[[").Replace("]", "]]");
				AnsiConsole.MarkupLine("[yellow]  • " + text5 + "[/]");
			}
		}
		else if (mergeReport.Warnings.Count > 10)
		{
			AnsiConsole.WriteLine();
			AnsiConsole.MarkupLine($"[yellow]⚠ {mergeReport.Warnings.Count} warning(s) - check logs for details[/]");
		}
		else
		{
			AnsiConsole.MarkupLine("[green]No merge issues detected.[/]");
		}
		if (mergeReport.StalenessWarnings.Count <= 0)
		{
			return;
		}
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine($"[yellow bold]⚠ Stale Mod Detections ({mergeReport.StalenessWarnings.Count}):[/]");
		foreach (string stalenessWarning in mergeReport.StalenessWarnings)
		{
			string text6 = stalenessWarning.Replace("[", "[[").Replace("]", "]]");
			AnsiConsole.MarkupLine("[yellow]  • " + text6 + "[/]");
		}
	}
}
