namespace Net8ConditionalRemover.Tests.Utilities;

using System.Text;
using Net8ConditionalRemover.Utilities;
using Xunit;

public class BomHandlerTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadWithBomDetectionAsync_DetectsBomPresence(bool withBom)
    {
        var path = CreateTempFile(withBom, "test content");

        var (content, hasBom) = await BomHandler.ReadWithBomDetectionAsync(path);

        Assert.Equal(withBom, hasBom);
        Assert.Equal("test content", content);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WriteWithBomAsync_IncludesBomWhenRequested(bool includeBom)
    {
        var path = GetTempFilePath();
        const string content = "test content";

        await BomHandler.WriteWithBomAsync(path, content, includeBom);

        var bytes = await File.ReadAllBytesAsync(path);
        if (includeBom)
        {
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);
        }
        else
        {
            Assert.NotEqual(0xEF, bytes[0]);
        }
    }

    [Fact]
    public async Task RoundTrip_PreservesBom()
    {
        var originalPath = CreateTempFile(withBom: true, "original content");
        var outputPath = GetTempFilePath();

        var (_, hasBom) = await BomHandler.ReadWithBomDetectionAsync(originalPath);
        await BomHandler.WriteWithBomAsync(outputPath, "modified content", hasBom);

        var (_, outputHasBom) = await BomHandler.ReadWithBomDetectionAsync(outputPath);
        Assert.True(outputHasBom);
    }

    private string CreateTempFile(bool withBom, string content)
    {
        var path = GetTempFilePath();
        using var stream = File.Create(path);

        if (withBom)
        {
            stream.Write([0xEF, 0xBB, 0xBF]);
        }

        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes);

        return path;
    }

    private string GetTempFilePath()
    {
        var path = Path.GetTempFileName();
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
