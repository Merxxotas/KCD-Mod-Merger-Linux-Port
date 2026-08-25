using System.Threading.Tasks;

namespace KCDMerge.Core.Data;

public interface IPakWriter
{
	Task CreatePakAsync(string outputPath);
}
