namespace Net8ConditionalRemover.Tests.Utilities;

using Net8ConditionalRemover.Utilities;
using Xunit;

public class WhitespaceNormalizerTests
{
    [Theory]
    [InlineData("line1\n\n\n\nline2", LineEnding.LF, "line1\n\nline2")]
    [InlineData("line1\r\n\r\n\r\n\r\nline2", LineEnding.CRLF, "line1\r\n\r\nline2")]
    [InlineData("line1\n\nline2", LineEnding.LF, "line1\n\nline2")]
    public void Normalize_ReducesExcessBlankLines(string content, LineEnding lineEnding, string expected)
    {
        var result = WhitespaceNormalizer.Normalize(content, lineEnding);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("line1   \nline2\t\nline3", "line1\nline2\nline3")]
    [InlineData("    indented\n        more indented", "    indented\n        more indented")]
    [InlineData("", "")]
    public void Normalize_HandlesWhitespace(string content, string expected)
    {
        var result = WhitespaceNormalizer.Normalize(content, LineEnding.LF);

        Assert.Equal(expected, result);
    }
}
