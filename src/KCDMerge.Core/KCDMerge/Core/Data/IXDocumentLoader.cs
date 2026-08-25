using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace KCDMerge.Core.Data;

public interface IXDocumentLoader
{
	Task<XDocument?> LoadFromStreamAsync(Stream stream, string fileName);

	Task<XDocument?> LoadVanillaAsync(string fileName);

	DateTime GetVanillaTimestamp(string fileName);

	DateTime GetVanillaEntryTimestamp(string fileName);
}
