using System;
using System.Collections.Generic;

namespace KCDMerge.Core.Data;

public class MergeReport
{
	public List<string> Errors { get; } = new List<string>();

	public List<string> Warnings { get; } = new List<string>();

	public List<MergeDetail> MergeDetails { get; } = new List<MergeDetail>();

	public Dictionary<string, FileStats> FileStatistics { get; } = new Dictionary<string, FileStats>();

	public List<string> StalenessWarnings { get; } = new List<string>();

	public bool HasIssues
	{
		get
		{
			if (Errors.Count <= 0)
			{
				return Warnings.Count > 0;
			}
			return true;
		}
	}

	public void AddError(string error)
	{
		Errors.Add(error);
	}

	public void AddWarning(string warning)
	{
		Warnings.Add(warning);
	}

	public void AddStalenessWarning(string fileName, string modName, DateTime modDate, DateTime vanillaDate, string decision)
	{
		int value = (int)(vanillaDate - modDate).TotalDays;
		StalenessWarnings.Add($"{fileName} | {modName} | mod: {modDate:yyyy-MM-dd} | vanilla: {vanillaDate:yyyy-MM-dd} | {value}d behind | {decision}");
	}

	public void AddMergeDetail(string fileName, string entityKey, string sourceMod, string reason)
	{
		MergeDetails.Add(new MergeDetail
		{
			FileName = fileName,
			EntityKey = entityKey,
			SourceMod = sourceMod,
			Reason = reason
		});
	}

	public void RecordFileStats(string fileName, string modName, int priority, bool isPatchFile, bool isIdOnlyTable, bool isStandardTable, int additions, int deletions, int modifications, int warnings, int errors)
	{
		string key = fileName + "|" + modName;
		if (FileStatistics.TryGetValue(key, out FileStats value))
		{
			value.Additions += additions;
			value.Deletions += deletions;
			value.Modifications += modifications;
			value.Warnings += warnings;
			value.Errors += errors;
			return;
		}
		FileStatistics[key] = new FileStats
		{
			FileName = fileName,
			ModName = modName,
			Priority = priority,
			IsPatchFile = isPatchFile,
			IsIdOnlyTable = isIdOnlyTable,
			IsStandardTable = isStandardTable,
			Additions = additions,
			Deletions = deletions,
			Modifications = modifications,
			Warnings = warnings,
			Errors = errors
		};
	}

	public void Clear()
	{
		Errors.Clear();
		Warnings.Clear();
		MergeDetails.Clear();
		FileStatistics.Clear();
		StalenessWarnings.Clear();
	}
}
