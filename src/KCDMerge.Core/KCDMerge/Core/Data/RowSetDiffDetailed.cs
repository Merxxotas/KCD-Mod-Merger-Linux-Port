using System.Collections.Generic;
using System.Xml.Linq;

namespace KCDMerge.Core.Data;

public class RowSetDiffDetailed
{
	public List<XElement> AddedRows { get; set; } = new List<XElement>();

	public List<XElement> RemovedRows { get; set; } = new List<XElement>();

	public List<RowModificationDetail> ModifiedRows { get; set; } = new List<RowModificationDetail>();

	public List<XElement> UnchangedRows { get; set; } = new List<XElement>();
}
