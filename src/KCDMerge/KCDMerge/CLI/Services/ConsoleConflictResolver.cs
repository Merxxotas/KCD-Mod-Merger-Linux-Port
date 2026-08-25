using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using KCDMerge.Core.Configuration;
using KCDMerge.Core.Services;
using Spectre.Console;

namespace KCDMerge.CLI.Services;

public class ConsoleConflictResolver : IConflictResolver
{
	private readonly IModConflictRulesService _rulesService;

	public ConsoleConflictResolver(IModConflictRulesService rulesService)
	{
		_rulesService = rulesService;
	}

	public Task<RowMergeDecision> ResolveIdOnlyTableDifference(string fileName, string modName, int addedCount, int removedCount, List<XElement> sampleAdditions, List<XElement> sampleRemovals)
	{
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
		RowMergeDecision? rowMergeDecision;
		RowMergeDecision? rowMergeDecision2;
		switch (_rulesService.GetRowAdditionStrategy(fileNameWithoutExtension, modName))
		{
		case "IncludeBoth":
			rowMergeDecision = RowMergeDecision.IncludeBoth;
			goto IL_0085;
		case "OnlyAdditions":
			rowMergeDecision = RowMergeDecision.OnlyAdditions;
			goto IL_0085;
		case "OnlyRemovals":
			rowMergeDecision = RowMergeDecision.OnlyRemovals;
			goto IL_0085;
		case "SkipMod":
			rowMergeDecision = RowMergeDecision.SkipMod;
			goto IL_0085;
		default:
			rowMergeDecision = null;
			goto IL_0085;
		case null:
			break;
			IL_0085:
			rowMergeDecision2 = rowMergeDecision;
			if (rowMergeDecision2.HasValue)
			{
				AnsiConsole.MarkupLine($"[dim]Using saved decision for {fileNameWithoutExtension} / {modName}: {rowMergeDecision2.Value}[/]");
				return Task.FromResult(rowMergeDecision2.Value);
			}
			break;
		}
		AnsiConsole.MarkupLine("\n[yellow]ID-Only Table Change Detected:[/] [cyan]" + fileName + "[/]");
		AnsiConsole.MarkupLine("[yellow]Mod:[/] [cyan]" + modName + "[/]\n");
		if (addedCount > 0)
		{
			AnsiConsole.MarkupLine($"[green]+ Adds {addedCount} new row(s)[/]");
			if (sampleAdditions.Count > 0)
			{
				AnsiConsole.MarkupLine("[dim]  Sample additions:[/]");
				foreach (XElement item in sampleAdditions.Take(3))
				{
					string text = string.Join(", ", from a in item.Attributes()
						select $"{a.Name}=\"{a.Value}\"");
					AnsiConsole.MarkupLine("[dim]    <row " + text + "/>[/]");
				}
			}
		}
		if (removedCount > 0)
		{
			AnsiConsole.MarkupLine($"[red]- Removes {removedCount} row(s)[/]");
			if (sampleRemovals.Count > 0)
			{
				AnsiConsole.MarkupLine("[dim]  Sample removals:[/]");
				foreach (XElement item2 in sampleRemovals.Take(3))
				{
					string text2 = string.Join(", ", from a in item2.Attributes()
						select $"{a.Name}=\"{a.Value}\"");
					AnsiConsole.MarkupLine("[dim]    <row " + text2 + "/>[/]");
				}
			}
		}
		RowMergeDecision rowMergeDecision3 = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("\n[yellow]How should these changes be merged?[/]").AddChoices("Include additions AND removals (full mod changes)", "Include additions only (additive merge)", "Include removals only (subtractive merge)", "Skip this mod's changes (keep current state)")) switch
		{
			"Include additions AND removals (full mod changes)" => RowMergeDecision.IncludeBoth, 
			"Include additions only (additive merge)" => RowMergeDecision.OnlyAdditions, 
			"Include removals only (subtractive merge)" => RowMergeDecision.OnlyRemovals, 
			_ => RowMergeDecision.SkipMod, 
		};
		AnsiConsole.MarkupLine($"[green]Decision: {rowMergeDecision3}[/]\n");
		_rulesService.SetRowAdditionStrategy(fileNameWithoutExtension, modName, rowMergeDecision3.ToString());
		return Task.FromResult(rowMergeDecision3);
	}

	public Task<string> ResolveModConflictAsync(string modA, string modB, string conflictSummary)
	{
		AnsiConsole.MarkupLine("\n[yellow]Mod Conflict Detected:[/]");
		AnsiConsole.MarkupLine($"[cyan]{modA}[/] and [cyan]{modB}[/] modify the same data.");
		AnsiConsole.MarkupLine("[dim]" + conflictSummary + "[/]\n");
		string text = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("[yellow]Which mod should take priority?[/]").AddChoices(modA, modB));
		AnsiConsole.MarkupLine("[green]Decision: " + text + " takes priority[/]\n");
		return Task.FromResult(text);
	}

	public Task<StalenessDecision> ResolveStaleModAsync(string fileName, string modName, DateTime modEntryDate, DateTime vanillaPakDate)
	{
		string stalenessOverride = _rulesService.GetStalenessOverride(fileName, modName);
		if (stalenessOverride != null)
		{
			StalenessDecision stalenessDecision = ((!(stalenessOverride == "UseWholeXml")) ? StalenessDecision.MergeAnyway : StalenessDecision.UseWholeXml);
			AnsiConsole.MarkupLine($"[dim]Using saved staleness decision for {Path.GetFileName(fileName)} / {modName}: {stalenessDecision}[/]");
			return Task.FromResult(stalenessDecision);
		}
		int value = (int)(vanillaPakDate - modEntryDate).TotalDays;
		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[yellow]⚠ STALE MOD DETECTED:[/] [cyan]" + fileName + "[/]");
		AnsiConsole.MarkupLine($"  [yellow]Mod:[/]     [cyan]{modName}[/] (file dated {modEntryDate:yyyy-MM-dd})");
		AnsiConsole.MarkupLine($"  [yellow]Vanilla:[/] vanilla file dated {vanillaPakDate:yyyy-MM-dd}");
		AnsiConsole.MarkupLine($"  This mod's XML predates the game patch by [yellow]{value}[/] day(s).");
		AnsiConsole.WriteLine();
		StalenessDecision stalenessDecision2 = ((!(AnsiConsole.Prompt(new SelectionPrompt<string>().Title("[yellow]How should this file be handled?[/]").AddChoices("Use whole XML (mod's file as-is, no merge)", "Merge anyway (delta merge, may produce incorrect results)")) == "Use whole XML (mod's file as-is, no merge)")) ? StalenessDecision.MergeAnyway : StalenessDecision.UseWholeXml);
		AnsiConsole.MarkupLine($"[green]Decision: {stalenessDecision2}[/]\n");
		_rulesService.SetStalenessOverride(fileName, modName, stalenessDecision2.ToString());
		return Task.FromResult(stalenessDecision2);
	}
}
