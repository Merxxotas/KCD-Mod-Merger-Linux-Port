using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KCDMerge.Core.Services;

public interface IVanillaPakIndexer
{
	Task BuildIndexAsync(string gameDataPath);

	string? FindVanillaPak(string internalPath);

	bool IsIndexValid(string gameDataPath);

	List<string> FindByFilename(string filename);

	DateTime GetVanillaEntryTimestamp(string internalPath);
}
