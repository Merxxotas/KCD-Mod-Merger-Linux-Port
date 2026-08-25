using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace KCDMerge.Core.Services;

public interface IPakManager
{
	IEnumerable<string> GetPakEntries(string pakPath);

	IEnumerable<(string EntryName, DateTime ModifiedDate)> GetPakEntriesWithTimestamps(string pakPath);

	Task<bool> ExtractFileAsync(string pakPath, string entryName, string destinationPath);

	Task<MemoryStream?> ReadFileToMemoryAsync(string pakPath, string entryName);

	Task<bool> IsXmlContentAsync(string pakPath, string entryName);
}
