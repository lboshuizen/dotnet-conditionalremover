namespace Net8ConditionalRemover.Models;

using Microsoft.CodeAnalysis;

public record AnalysisIssue(Location Location, string Message);
