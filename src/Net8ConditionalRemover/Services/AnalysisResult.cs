namespace Net8ConditionalRemover.Services;

using Net8ConditionalRemover.Models;

public record AnalysisResult(
    List<DirectiveBlock> Blocks,
    List<AnalysisIssue> Issues);
