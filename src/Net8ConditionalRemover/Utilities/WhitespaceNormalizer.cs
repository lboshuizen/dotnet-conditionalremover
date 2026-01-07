namespace Net8ConditionalRemover.Utilities;

using System.Text.RegularExpressions;

/// <summary>
/// Normalizes whitespace after directive removal to prevent excess blank lines.
/// </summary>
public static partial class WhitespaceNormalizer
{
    [GeneratedRegex(@"(\r?\n){3,}", RegexOptions.Compiled)]
    private static partial Regex ExcessNewlinesRegex();

    /// <summary>
    /// Reduces sequences of 3+ blank lines to maximum of 1 blank line (2 newlines).
    /// Preserves intentional single blank lines for readability.
    /// </summary>
    public static string Normalize(string content, LineEnding lineEnding)
    {
        var newline = lineEnding == LineEnding.CRLF ? "\r\n" : "\n";
        var doubleNewline = newline + newline;

        var normalized = ExcessNewlinesRegex().Replace(content, doubleNewline);

        var lines = normalized.Split(["\r\n", "\n"], StringSplitOptions.None);
        var trimmedLines = lines.Select(line => line.TrimEnd());

        return string.Join(newline, trimmedLines);
    }
}
