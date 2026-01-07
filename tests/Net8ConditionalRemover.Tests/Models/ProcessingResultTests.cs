namespace Net8ConditionalRemover.Tests.Models;

using Net8ConditionalRemover.Models;
using Xunit;

public class ProcessingResultTests
{
    [Fact]
    public void Success_ReturnsSuccessStatus_WhenNoBlocksFlagged()
    {
        var result = ProcessingResult.Success(
            path: "/test/file.cs",
            blocksRemoved: 5,
            blocksFlagged: 0,
            issues: [],
            preview: null);

        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.Equal("/test/file.cs", result.FilePath);
        Assert.Equal(5, result.BlocksRemoved);
        Assert.Equal(0, result.BlocksFlaggedForReview);
    }

    [Fact]
    public void Success_ReturnsSuccessWithReview_WhenBlocksFlagged()
    {
        var result = ProcessingResult.Success(
            path: "/test/file.cs",
            blocksRemoved: 3,
            blocksFlagged: 2,
            issues: [],
            preview: "content");

        Assert.Equal(ResultStatus.SuccessWithReview, result.Status);
        Assert.Equal(2, result.BlocksFlaggedForReview);
        Assert.Equal("content", result.PreviewContent);
    }

    [Fact]
    public void Failed_ReturnsFailedStatus_WithErrors()
    {
        var result = ProcessingResult.Failed(
            path: "/test/file.cs",
            errors: ["Error 1", "Error 2"]);

        Assert.Equal(ResultStatus.Failed, result.Status);
        Assert.Equal(2, result.Errors!.Count);
    }

    [Fact]
    public void Skipped_ReturnsSkippedStatus()
    {
        var result = ProcessingResult.Skipped(
            path: "/test/file.cs",
            reason: "Generated file");

        Assert.Equal(ResultStatus.Skipped, result.Status);
        Assert.Contains("Generated file", result.Errors!);
    }
}
