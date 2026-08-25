namespace KCDMerge.Core.Services;

public enum RowMergeDecision
{
	IncludeBoth,
	OnlyAdditions,
	OnlyRemovals,
	SkipMod
}
