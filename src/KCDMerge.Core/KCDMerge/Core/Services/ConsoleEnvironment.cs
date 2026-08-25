using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KCDMerge.Core.Services;

public static class ConsoleEnvironment
{
	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

	public static bool IsStartedFromExplorer()
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return false;
		}
		try
		{
			uint[] array = new uint[10];
			if (GetConsoleProcessList(array, (uint)array.Length) == 1)
			{
				return true;
			}
			using Process process = Process.GetCurrentProcess();
			Process parentProcessLegacy = GetParentProcessLegacy(process.Id);
			if (parentProcessLegacy != null && parentProcessLegacy.ProcessName.ToLowerInvariant().Contains("explorer"))
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	private static Process? GetParentProcessLegacy(int processId)
	{
		return null;
	}
}
