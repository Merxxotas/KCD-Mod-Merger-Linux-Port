using System.Collections.Generic;

namespace KCDMerge.Core.Models;

public class ModInfo
{
	public string Name { get; set; } = string.Empty;

	public string DisplayName { get; set; } = string.Empty;

	public string RootPath { get; set; } = string.Empty;

	public List<string> PakFiles { get; set; } = new List<string>();

	public Dictionary<string, List<string>> LooseFileFolders { get; set; } = new Dictionary<string, List<string>>();

	public bool IsNonStandardStructure { get; set; }

	public int LoadPriority { get; set; } = int.MaxValue;

	public List<string> CfgFiles { get; set; } = new List<string>();

	public bool IsValid { get; set; } = true;

	public override string ToString()
	{
		return $"{Name} (Priority: {LoadPriority})";
	}
}
