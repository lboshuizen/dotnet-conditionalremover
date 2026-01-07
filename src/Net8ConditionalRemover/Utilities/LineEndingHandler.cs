namespace Net8ConditionalRemover.Utilities;

public static class LineEndingHandler
{
    public static LineEnding Detect(string content)
    {
        var crlfCount = 0;
        var lfCount = 0;

        for (var i = 0; i < content.Length; i++)
        {
            switch (content[i])
            {
                case '\r' when i + 1 < content.Length && content[i + 1] == '\n':
                    crlfCount++;
                    i++;
                    break;
                case '\n':
                    lfCount++;
                    break;
            }
        }

        // If mixed, default to LF (Unix standard)
        return crlfCount > lfCount ? LineEnding.CRLF : LineEnding.LF;
    }

    public static string Normalize(string content, LineEnding targetEnding)
    {
        var normalized = content.Replace("\r\n", "\n");

        return targetEnding switch
        {
            LineEnding.CRLF => normalized.Replace("\n", "\r\n"),
            _ => normalized
        };
    }
}
