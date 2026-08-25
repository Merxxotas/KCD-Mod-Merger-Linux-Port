using System.Collections.Generic;
using System.Xml.Linq;

namespace KCDMerge.Core.Data;

public interface IPtfGenerator
{
	PtfResult GeneratePtf(XDocument mergedDoc, XDocument vanillaDoc, List<string> pkColumns, string fileName, bool supportDeleteMarking = false);
}
