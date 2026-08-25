using System.Collections.Generic;
using System.Xml.Linq;

namespace KCDMerge.Core.Data;

public class MergeResult
{
	public XDocument? MergedDoc { get; set; }

	public XDocument? VanillaDoc { get; set; }

	public DeltaType? TableType { get; set; }

	public List<string>? PkColumns { get; set; }

	public bool HasTableStructure { get; set; }
}
