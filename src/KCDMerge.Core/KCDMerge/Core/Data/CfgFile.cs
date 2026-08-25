using System;
using System.Collections.Generic;

namespace KCDMerge.Core.Data;

public class CfgFile
{
	public List<CfgEntry> Entries { get; set; } = new List<CfgEntry>();

	public List<string> TrailingLines { get; set; } = new List<string>();

	public CfgEntry? FindEntry(string variableName)
	{
		foreach (CfgEntry entry in Entries)
		{
			if (string.Equals(entry.Variable, variableName, StringComparison.OrdinalIgnoreCase))
			{
				return entry;
			}
		}
		return null;
	}
}
