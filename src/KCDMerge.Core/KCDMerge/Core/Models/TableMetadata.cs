using System.Collections.Generic;

namespace KCDMerge.Core.Models;

public class TableMetadata
{
	public List<string> PkColumns { get; set; } = new List<string>();

	public bool HasOnlyPkColumns { get; set; }
}
