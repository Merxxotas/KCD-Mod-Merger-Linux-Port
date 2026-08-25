using System.Collections.Generic;
using System.Xml.Linq;
using KCDMerge.Core.Services;

namespace KCDMerge.Core.Data;

public interface IXDocumentMerger
{
	XDocument Merge(XDocument baseDoc, XDocument modDoc, string fileName, string modName, int priority, MergeReport report, bool isPatchFile = false, XDocument? vanillaDoc = null);

	void ResetAttributeTracking();

	XDocument ApplyResolvedOps(XDocument vanillaDoc, ResolvedMergeOps ops, List<string>? pkColumns, string fileName, MergeReport report);
}
