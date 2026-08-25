using System.Collections.Generic;

namespace KCDMerge.Core.Data;

public class CfgEntry
{
	public string Variable { get; set; } = string.Empty;

	public string Value { get; set; } = string.Empty;

	public List<string> Comments { get; set; } = new List<string>();

	public string? SourceMod { get; set; }
}
