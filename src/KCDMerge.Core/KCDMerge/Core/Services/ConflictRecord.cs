namespace KCDMerge.Core.Services;

public class ConflictRecord
{
	public string RowKey { get; set; } = string.Empty;

	public string ModA { get; set; } = string.Empty;

	public string ModB { get; set; } = string.Empty;

	public string ConflictType { get; set; } = string.Empty;

	public string Winner { get; set; } = string.Empty;
}
