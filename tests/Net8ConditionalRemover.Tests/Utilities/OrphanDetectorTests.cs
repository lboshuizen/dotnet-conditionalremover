namespace Net8ConditionalRemover.Tests.Utilities;

using Microsoft.CodeAnalysis.CSharp;
using Net8ConditionalRemover.Utilities;
using Xunit;

public class OrphanDetectorTests
{
    [Theory]
    [InlineData("#if DEBUG\nvar x = 1;\n#else\nvar x = 2;\n#endif", false, "")]
    [InlineData("#if DEBUG\n#if TRACE\nvar x = 1;\n#endif\n#endif", false, "")]
    [InlineData("var x = 1;\n#else\nvar x = 2;\n#endif", true, "#else")]
    [InlineData("var x = 1;\n#endif", true, "#endif")]
    [InlineData("#if DEBUG\nvar x = 1;", true, "#if")]
    public void Detect_IdentifiesOrphanedDirectives(string code, bool hasOrphans, string expectedDirective)
    {
        var root = CSharpSyntaxTree.ParseText(code).GetRoot();

        var orphans = OrphanDetector.Detect(root);

        if (hasOrphans)
        {
            Assert.NotEmpty(orphans);
            Assert.Contains(orphans, o => o.Text.Contains(expectedDirective));
        }
        else
        {
            Assert.Empty(orphans);
        }
    }

    [Fact]
    public void Detect_ReportsLineNumber()
    {
        const string code = """
                            line1
                            line2
                            #endif
                            """;
        var root = CSharpSyntaxTree.ParseText(code).GetRoot();

        var orphans = OrphanDetector.Detect(root);

        Assert.Single(orphans);
        Assert.Equal(3, orphans[0].Line);
    }
}
