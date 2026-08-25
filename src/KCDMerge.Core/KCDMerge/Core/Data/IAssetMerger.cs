using System.Threading.Tasks;

namespace KCDMerge.Core.Data;

public interface IAssetMerger
{
	Task MergeAssetAsync(string pakPath, string entryName, string sourceMod, int priority, MergeReport report);
}
