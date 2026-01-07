namespace Net8ConditionalRemover.Utilities;

public static class FileBackup
{
    public static async Task<string> CreateBackupAsync(string filePath)
    {
        var backupPath = filePath + ".bak";

        if (File.Exists(backupPath))
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            backupPath = $"{filePath}.{timestamp}.bak";
        }

        await using var source = File.OpenRead(filePath);
        await using var dest = File.Create(backupPath);
        await source.CopyToAsync(dest);

        return backupPath;
    }

    public static void RestoreBackup(string backupPath, string originalPath)
    {
        if (File.Exists(backupPath))
        {
            File.Copy(backupPath, originalPath, overwrite: true);
        }
    }

    public static void CleanupBackup(string backupPath)
    {
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }
    }
}
