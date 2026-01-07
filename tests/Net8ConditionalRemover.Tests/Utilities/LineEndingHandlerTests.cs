namespace Net8ConditionalRemover.Tests.Utilities;

using Net8ConditionalRemover.Utilities;
using Xunit;

public class LineEndingHandlerTests
{
    [Theory]
    [InlineData("line1\nline2\nline3", LineEnding.LF, "Only LF returns LF")]
    [InlineData("line1\r\nline2\r\nline3", LineEnding.CRLF, "Only CRLF returns CRLF")]
    [InlineData("line1\r\nline2\nline3", LineEnding.LF, "Mixed equal defaults to LF")]
    [InlineData("line1\r\nline2\r\nline3\nline4", LineEnding.CRLF, "Mostly CRLF returns CRLF")]
    [InlineData("single line", LineEnding.LF, "No line endings defaults to LF")]
    public void Detect_ReturnsExpectedLineEnding(string content, LineEnding expected, string _)
    {
        var result = LineEndingHandler.Detect(content);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("line1\r\nline2\r\n", LineEnding.LF, "line1\nline2\n")]
    [InlineData("line1\nline2\n", LineEnding.CRLF, "line1\r\nline2\r\n")]
    public void Normalize_ConvertsLineEndings(string input, LineEnding target, string expected)
    {
        var result = LineEndingHandler.Normalize(input, target);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_PreservesContent()
    {
        const string content = "namespace Test;\r\nclass Foo { }\r\n";

        var result = LineEndingHandler.Normalize(content, LineEnding.LF);

        Assert.Contains("namespace Test;", result);
        Assert.Contains("class Foo { }", result);
    }
}
