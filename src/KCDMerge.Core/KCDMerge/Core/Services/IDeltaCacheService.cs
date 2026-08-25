using System;
using KCDMerge.Core.Data;

namespace KCDMerge.Core.Services;

public interface IDeltaCacheService
{
	DeltaResult? TryGetCachedDelta(string fileName, string modName, DateTime sourceTimestamp, DateTime vanillaTimestamp, string? pakSource = null);

	void CacheDelta(string fileName, string modName, DateTime sourceTimestamp, DateTime vanillaTimestamp, DeltaResult delta, string? pakSource = null);

	void LoadCache();

	void SaveCache();
}
