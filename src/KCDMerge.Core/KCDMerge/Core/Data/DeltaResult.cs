using System.Collections.Generic;
using System.Xml.Linq;

namespace KCDMerge.Core.Data;

public class DeltaResult
{
	public XDocument DeltaDoc { get; set; } = new XDocument();

	public DeltaType Type { get; set; }

	public List<string>? PkColumns { get; set; }

	public int AddedRows { get; set; }

	public int DeletedRows { get; set; }

	public int ModifiedRows { get; set; }

	public int UnchangedRows { get; set; }
}
