namespace KCDMerge.Core.Configuration;

public interface IConfigurationService
{
	AppConfig LoadConfiguration();

	void SaveConfiguration(AppConfig config);

	bool ValidateConfiguration(out string error);
}
