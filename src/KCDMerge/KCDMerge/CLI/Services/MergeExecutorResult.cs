using System.Collections.Generic;

namespace KCDMerge.CLI.Services;

public class MergeExecutorResult
{
	public int MergedCount { get; init; }

	public int SkippedCount { get; init; }

	public IReadOnlySet<string> PtfOutputFiles { get; init; } = new HashSet<string>();

	public IReadOnlySet<string> NoVanillaFiles { get; init; } = new HashSet<string>();
}
