using System.Collections.Generic;
using KCDMerge.Core.Models;

namespace KCDMerge.Core.Services;

public interface IModScanner
{
	IEnumerable<ModInfo> ScanMods();
}
