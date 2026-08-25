using System.Threading.Tasks;

namespace KCDMerge.Core.Data;

public interface IStagingManager
{
	Task CleanStagingAsync();

	string GetStagingPath();
}
