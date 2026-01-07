namespace Net8ConditionalRemover.Tests.Utilities;

using Net8ConditionalRemover.Utilities;
using Xunit;

public class FileBackupTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public async Task CreateBackupAsync_CreatesBackupFile()
    {
        var originalPath = CreateTempFile("original content");

        var backupPath = await FileBackup.CreateBackupAsync(originalPath);
        _tempFiles.Add(backupPath);

        Assert.True(File.Exists(backupPath));
        Assert.Equal("original content", await File.ReadAllTextAsync(backupPath));
    }

    [Fact]
    public async Task CreateBackupAsync_ReturnsPathWithBakExtension()
    {
        var originalPath = CreateTempFile("content");

        var backupPath = await FileBackup.CreateBackupAsync(originalPath);
        _tempFiles.Add(backupPath);

        Assert.EndsWith(".bak", backupPath);
    }

    [Fact]
    public async Task CreateBackupAsync_AddsTimestamp_WhenBackupExists()
    {
        var originalPath = CreateTempFile("content");
        var firstBackup = await FileBackup.CreateBackupAsync(originalPath);
        _tempFiles.Add(firstBackup);

        var secondBackup = await FileBackup.CreateBackupAsync(originalPath);
        _tempFiles.Add(secondBackup);

        Assert.NotEqual(firstBackup, secondBackup);
        Assert.True(File.Exists(firstBackup));
        Assert.True(File.Exists(secondBackup));
    }

    [Fact]
    public void RestoreBackup_RestoresOriginalContent()
    {
        var originalPath = CreateTempFile("original");
        var backupPath = CreateTempFile("backup content");
        File.WriteAllText(originalPath, "modified");

        FileBackup.RestoreBackup(backupPath, originalPath);

        Assert.Equal("backup content", File.ReadAllText(originalPath));
    }

    [Fact]
    public void RestoreBackup_DoesNothing_WhenBackupDoesNotExist()
    {
        var originalPath = CreateTempFile("original");
        var backupPath = Path.GetTempFileName() + ".nonexistent";

        FileBackup.RestoreBackup(backupPath, originalPath);

        Assert.Equal("original", File.ReadAllText(originalPath));
    }

    [Fact]
    public void CleanupBackup_DeletesBackupFile()
    {
        var backupPath = CreateTempFile("backup");

        FileBackup.CleanupBackup(backupPath);

        Assert.False(File.Exists(backupPath));
    }

    private string CreateTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles.Where(File.Exists))
        {
            File.Delete(file);
        }
    }
}
