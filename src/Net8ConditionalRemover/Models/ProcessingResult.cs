namespace Net8ConditionalRemover.Models;

using Microsoft.CodeAnalysis;

public record ProcessingResult
{
    public required string FilePath { get; init; }
    public required ResultStatus Status { get; init; }
    public int BlocksRemoved { get; init; }
    public int BlocksFlaggedForReview { get; init; }
    public string? PreviewContent { get; init; }
    public List<string>? Errors { get; init; }
    public List<AnalysisIssue>? Issues { get; init; }

    public static ProcessingResult Success(
        string path,
        int blocksRemoved,
        int blocksFlagged,
        List<AnalysisIssue> issues,
        string? preview)
        => new()
        {
            FilePath = path,
            Status = blocksFlagged > 0 ? ResultStatus.SuccessWithReview : ResultStatus.Success,
            BlocksRemoved = blocksRemoved,
            BlocksFlaggedForReview = blocksFlagged,
            Issues = issues,
            PreviewContent = preview
        };

    public static ProcessingResult Failed(string path, List<Diagnostic> diagnostics)
        => new()
        {
            FilePath = path,
            Status = ResultStatus.Failed,
            Errors = diagnostics.Select(d => d.GetMessage()).ToList()
        };

    public static ProcessingResult Failed(string path, List<string> errors)
        => new() { FilePath = path, Status = ResultStatus.Failed, Errors = errors };

    public static ProcessingResult Skipped(string path, string reason)
        => new()
        {
            FilePath = path,
            Status = ResultStatus.Skipped,
            Errors = [reason]
        };
}
