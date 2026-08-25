namespace KCDMerge.Core.Services;

public interface IBackupService
{
	string? BackupFile(string filePath, int maxBackups = 5);
}
