namespace KCDMerge.Core.Configuration;

public class PreferenceService : IPreferenceService
{
	private readonly IModConflictRulesService _rulesService;

	public PreferenceService(IModConflictRulesService rulesService)
	{
		_rulesService = rulesService;
	}

	public string? GetPreferredMod(string modA, string modB)
	{
		return _rulesService.GetAssetConflictWinner(modA, modB);
	}

	public void SetPreference(string winner, string loser)
	{
		_rulesService.SetAssetConflictWinner(winner, loser);
	}
}
