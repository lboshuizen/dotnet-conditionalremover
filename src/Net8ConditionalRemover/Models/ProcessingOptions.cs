namespace Net8ConditionalRemover.Models;

/// <summary>
/// Processing options from CLI
/// </summary>
public record ProcessingOptions
{
    public bool DryRun { get; init; }
    public bool Verbose { get; init; }
    public bool IncludeGenerated { get; init; }
    public bool CreateBackup { get; init; }
    public bool Parallel { get; init; }
    public string? ReportPath { get; init; }
    public bool FailOnReview { get; init; }

    /// <summary>
    /// The symbol to target for removal (e.g., NET8_0_OR_GREATER, NET9_0_OR_GREATER).
    /// The underscore variant is automatically included as an alias.
    /// </summary>
    public string TargetSymbol { get; init; } = "NET8_0_OR_GREATER";

    /// <summary>
    /// Additional preprocessor symbols to define when parsing.
    /// TargetSymbol is always included automatically.
    /// </summary>
    public string[] AdditionalDefines { get; init; } = [];

    /// <summary>
    /// All symbols to define when parsing (TargetSymbol + AdditionalDefines).
    /// </summary>
    public string[] PreprocessorSymbols => [TargetSymbol, .. AdditionalDefines];

    /// <summary>
    /// Target symbols including underscore variant for analyzer matching.
    /// </summary>
    public string[] TargetSymbolsWithAliases => GetSymbolsWithAliases(TargetSymbol);

    private static string[] GetSymbolsWithAliases(string symbol)
    {
        var withUnderscore = symbol.Replace("NET8", "NET_8")
                                   .Replace("NET9", "NET_9")
                                   .Replace("NET10", "NET_10");
        var withoutUnderscore = symbol.Replace("NET_8", "NET8")
                                      .Replace("NET_9", "NET9")
                                      .Replace("NET_10", "NET10");

        return new[] { symbol, withUnderscore, withoutUnderscore }
            .Distinct()
            .ToArray();
    }
}
