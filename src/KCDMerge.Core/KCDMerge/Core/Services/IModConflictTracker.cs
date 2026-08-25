using System.Threading.Tasks;
using KCDMerge.Core.Data;

namespace KCDMerge.Core.Services;

public interface IModConflictTracker
{
	void RegisterDelta(string fileName, string modName, DeltaResult delta);

	Task<ResolvedMergeOps> ResolveConflictsAsync(string fileName);
}
