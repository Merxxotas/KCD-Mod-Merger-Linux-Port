using System;
using System.Collections.Generic;

namespace KCDMerge.Core.Services;

public interface IModFileTracker
{
	void TrackFile(string fileName, string modName, int priority, string pakPath, string entryName, DateTime entryModifiedDate);

	IReadOnlyList<string> GetAllTrackedFiles();

	IReadOnlyList<ModFileSource> GetModSourcesForFile(string fileName);

	void Clear();

	Dictionary<string, int> GetStatistics();
}
