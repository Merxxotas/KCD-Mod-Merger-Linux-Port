using System.Collections.Generic;
using System.Threading.Tasks;
using KCDMerge.Core.Data;

namespace KCDMerge.Core.Services;

public interface IMergePipeline
{
	Task<MergeResult> MergeFileAsync(string fileName, IReadOnlyList<ModFileSource> modSources);

	MergeReport GetReport();

	void ResetReport();
}
