using System.Xml.Linq;

namespace KCDMerge.Core.Data;

public interface IDeltaNormalizer
{
	DeltaResult NormalizeToDelta(XDocument vanillaDoc, XDocument modDoc, string fileName, string modName, bool isPatchFile);
}
