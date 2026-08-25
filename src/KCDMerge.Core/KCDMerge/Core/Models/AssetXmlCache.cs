using System.Collections.Generic;

namespace KCDMerge.Core.Models;

public class AssetXmlCache
{
	public Dictionary<string, bool> ExtensionIsXml { get; set; } = new Dictionary<string, bool>();
}
