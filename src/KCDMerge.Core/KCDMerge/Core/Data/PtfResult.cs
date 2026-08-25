using System.Xml.Linq;

namespace KCDMerge.Core.Data;

public class PtfResult
{
	public XDocument? PtfDoc { get; set; }

	public bool RequiresFullXmlFallback { get; set; }

	public int AddedRows { get; set; }

	public int ModifiedRows { get; set; }

	public int DeletedRows { get; set; }

	public int UnchangedRows { get; set; }
}
