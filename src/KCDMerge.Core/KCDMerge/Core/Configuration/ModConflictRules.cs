using System.Collections.Generic;

namespace KCDMerge.Core.Configuration;

public class ModConflictRules
{
	public Dictionary<string, string> XmlConflicts { get; set; } = new Dictionary<string, string>();

	public Dictionary<string, string> AssetConflicts { get; set; } = new Dictionary<string, string>();

	public Dictionary<string, string> RowAdditionRules { get; set; } = new Dictionary<string, string>();

	public Dictionary<string, string> CfgConflicts { get; set; } = new Dictionary<string, string>();

	public Dictionary<string, string> StalenessOverrides { get; set; } = new Dictionary<string, string>();
}
