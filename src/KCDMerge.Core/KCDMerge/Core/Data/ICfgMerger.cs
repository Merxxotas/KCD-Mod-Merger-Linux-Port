using System.Collections.Generic;
using System.Threading.Tasks;
using KCDMerge.Core.Models;

namespace KCDMerge.Core.Data;

public interface ICfgMerger
{
	Task<int> MergeCfgFilesAsync(string gamePath, IReadOnlyList<ModInfo> mods, MergeReport report);
}
