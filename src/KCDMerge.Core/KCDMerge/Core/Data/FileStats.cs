namespace KCDMerge.Core.Data;

public class FileStats
{
	public string FileName { get; set; } = string.Empty;

	public string ModName { get; set; } = string.Empty;

	public int Priority { get; set; }

	public bool IsPatchFile { get; set; }

	public bool IsIdOnlyTable { get; set; }

	public bool IsStandardTable { get; set; }

	public int Additions { get; set; }

	public int Deletions { get; set; }

	public int Modifications { get; set; }

	public int Warnings { get; set; }

	public int Errors { get; set; }
}
