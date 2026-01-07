namespace Net8ConditionalRemover.Utilities;

using System.Text;

public static class BomHandler
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public static async Task<(string Content, bool HasBom)> ReadWithBomDetectionAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);

        bool hasBom = bytes.Length >= 3
            && bytes[0] == Utf8Bom[0]
            && bytes[1] == Utf8Bom[1]
            && bytes[2] == Utf8Bom[2];

        var content = hasBom
            ? Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3)
            : Encoding.UTF8.GetString(bytes);

        return (content, hasBom);
    }

    public static async Task WriteWithBomAsync(string path, string content, bool includeBom)
    {
        await using var stream = File.Create(path);

        if (includeBom)
        {
            await stream.WriteAsync(Utf8Bom);
        }

        var contentBytes = Encoding.UTF8.GetBytes(content);
        await stream.WriteAsync(contentBytes);
    }
}
