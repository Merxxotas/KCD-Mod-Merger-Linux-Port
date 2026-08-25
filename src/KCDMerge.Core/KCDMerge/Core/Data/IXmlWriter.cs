using System.Threading.Tasks;
using System.Xml.Linq;

namespace KCDMerge.Core.Data;

public interface IXmlWriter
{
	Task<string> WriteMergedDocumentAsync(string fileName, XDocument document);

	Task<string> WriteMergedDocumentAsync(string fileName, XDocument document, bool isLocalization, string? languageCode);
}
