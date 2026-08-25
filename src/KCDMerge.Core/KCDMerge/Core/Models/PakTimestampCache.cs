using System;
using System.Collections.Generic;

namespace KCDMerge.Core.Models;

public class PakTimestampCache
{
	public DateTime LastScanTime { get; set; }

	public Dictionary<string, DateTime> Paks { get; set; } = new Dictionary<string, DateTime>();
}
