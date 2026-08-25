using System.Collections.Generic;
using System.Xml.Linq;

namespace KCDMerge.Core.Data;

public class RowSetDiff
{
	public List<XElement> AddedRows { get; set; } = new List<XElement>();

	public List<XElement> RemovedRows { get; set; } = new List<XElement>();

	public List<XElement> CommonRows { get; set; } = new List<XElement>();
}
