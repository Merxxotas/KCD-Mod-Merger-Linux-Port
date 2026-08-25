using System.Collections.Generic;
using System.Xml.Linq;

namespace KCDMerge.Core.Data;

public interface IXPathIndexer
{
	Dictionary<string, XElement> BuildIndex(XDocument doc, string fileName);

	bool IsIdOnlyTable(string fileName);

	List<string>? DeriveTablePkSchema(XDocument doc, string fileName);

	bool ValidatePkUniqueness(XDocument doc, List<string> pkColumns);

	bool HasTableStructure(XDocument doc);
}
