using System.Collections.Generic;

namespace KCDMerge.Core.Models;

public class PkSchemaCache
{
	public string CacheVersion { get; set; } = "";

	public Dictionary<string, TableMetadata> Tables { get; set; } = new Dictionary<string, TableMetadata>();
}
