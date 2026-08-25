using System.Collections.Generic;
using System.Xml.Linq;

namespace KCDMerge.Core.Services;

public class ResolvedMergeOps
{
	public List<XElement> RowsToAdd { get; set; } = new List<XElement>();

	public List<XElement> RowsToDelete { get; set; } = new List<XElement>();

	public List<RowModification> RowsToModify { get; set; } = new List<RowModification>();

	public List<ConflictRecord> ResolvedConflicts { get; set; } = new List<ConflictRecord>();
}
