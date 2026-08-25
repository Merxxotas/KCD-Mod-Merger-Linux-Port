namespace KCDMerge.Core.Configuration;

public interface IPreferenceService
{
	string? GetPreferredMod(string modA, string modB);

	void SetPreference(string winner, string loser);
}
