namespace KCDMerge.Core.Configuration;

public interface IModConflictRulesService
{
	ModConflictRules LoadRules();

	void SaveRules(ModConflictRules rules);

	string? GetXmlConflictWinner(string modA, string modB);

	void SetXmlConflictWinner(string winner, string loser);

	string? GetAssetConflictWinner(string modA, string modB);

	void SetAssetConflictWinner(string winner, string loser);

	string? GetRowAdditionStrategy(string tableName, string modName);

	void SetRowAdditionStrategy(string tableName, string modName, string strategy);

	string? GetCfgConflictWinner(string variableName, string modA, string modB);

	void SetCfgConflictWinner(string variableName, string winner, string loser);

	string? GetStalenessOverride(string fileName, string modName);

	void SetStalenessOverride(string fileName, string modName, string decision);
}
