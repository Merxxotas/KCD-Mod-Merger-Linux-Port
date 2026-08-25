using System.Collections.Generic;

namespace KCDMerge.Core.Models;

public class DeltaCacheRoot
{
	public Dictionary<string, DeltaCacheEntry> Entries { get; set; } = new Dictionary<string, DeltaCacheEntry>();
}
