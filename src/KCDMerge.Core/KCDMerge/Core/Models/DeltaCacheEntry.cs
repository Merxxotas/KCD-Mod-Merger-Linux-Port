using System;
using System.Collections.Generic;

namespace KCDMerge.Core.Models;

public class DeltaCacheEntry
{
	public string FileName { get; set; } = string.Empty;

	public string ModName { get; set; } = string.Empty;

	public DateTime SourceTimestamp { get; set; }

	public DateTime VanillaTimestamp { get; set; }

	public string DeltaXml { get; set; } = string.Empty;

	public string DeltaType { get; set; } = string.Empty;

	public List<string>? PkColumns { get; set; }

	public int AddedRows { get; set; }

	public int DeletedRows { get; set; }

	public int ModifiedRows { get; set; }

	public int UnchangedRows { get; set; }
}
