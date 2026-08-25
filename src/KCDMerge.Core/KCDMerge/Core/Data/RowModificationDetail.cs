using System.Collections.Generic;
using System.Xml.Linq;

namespace KCDMerge.Core.Data;

public class RowModificationDetail
{
	public string RowKey { get; set; } = string.Empty;

	public XElement BaseRow { get; set; } = new XElement("row");

	public XElement ModRow { get; set; } = new XElement("row");

	public Dictionary<string, (string OldValue, string NewValue)> ChangedAttributes { get; set; } = new Dictionary<string, (string, string)>();
}
