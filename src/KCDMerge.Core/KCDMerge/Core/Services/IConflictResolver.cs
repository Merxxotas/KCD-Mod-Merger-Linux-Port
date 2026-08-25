using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace KCDMerge.Core.Services;

public interface IConflictResolver
{
	Task<string> ResolveModConflictAsync(string modA, string modB, string conflictSummary);

	Task<RowMergeDecision> ResolveIdOnlyTableDifference(string fileName, string modName, int addedCount, int removedCount, List<XElement> sampleAdditions, List<XElement> sampleRemovals);

	Task<StalenessDecision> ResolveStaleModAsync(string fileName, string modName, DateTime modEntryDate, DateTime vanillaPakDate);
}
