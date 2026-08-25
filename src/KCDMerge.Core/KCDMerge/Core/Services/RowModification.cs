using System.Collections.Generic;

namespace KCDMerge.Core.Services;

public class RowModification
{
	public string RowKey { get; set; } = string.Empty;

	public Dictionary<string, string> AttributeChanges { get; set; } = new Dictionary<string, string>();

	public string WinningMod { get; set; } = string.Empty;
}
