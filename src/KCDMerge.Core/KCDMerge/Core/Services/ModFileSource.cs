using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

namespace KCDMerge.Core.Services;

public record ModFileSource
{
	public required string ModName { get; init; }

	public required Stream XmlStream { get; init; }

	public required int Priority { get; init; }

	public string? PakPath { get; init; }

	public string? EntryName { get; init; }

	public bool IsLocalization { get; init; }

	public string? LanguageCode { get; init; }

	public bool IsPatchFile { get; init; }

	public string? BaseFileName { get; init; }

	public DateTime Timestamp { get; init; }

	[CompilerGenerated]
	[SetsRequiredMembers]
	protected ModFileSource(ModFileSource original)
	{
		ModName = original.ModName;
		XmlStream = original.XmlStream;
		Priority = original.Priority;
		PakPath = original.PakPath;
		EntryName = original.EntryName;
		IsLocalization = original.IsLocalization;
		LanguageCode = original.LanguageCode;
		IsPatchFile = original.IsPatchFile;
		BaseFileName = original.BaseFileName;
		Timestamp = original.Timestamp;
	}

	public ModFileSource()
	{
	}
}
