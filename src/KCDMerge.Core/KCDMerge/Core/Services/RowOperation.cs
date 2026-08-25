using System.Collections.Generic;
using System.Xml.Linq;

namespace KCDMerge.Core.Services;

public class RowOperation
{
	public RowOpType Type { get; set; }

	public string ModName { get; set; } = string.Empty;

	public XElement Row { get; set; } = new XElement("row");

	public Dictionary<string, string>? ChangedAttrs { get; set; }

	public List<string> PkColumns { get; set; } = new List<string>();
}
