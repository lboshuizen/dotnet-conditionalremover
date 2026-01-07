namespace Net8ConditionalRemover.Tests.Rewriters;

using Microsoft.CodeAnalysis.CSharp;
using Net8ConditionalRemover.Models;
using Net8ConditionalRemover.Rewriters;
using Net8ConditionalRemover.Services;
using Xunit;

public class TargetDirectiveRewriterTests
{
    private readonly ConditionalAnalyzer _analyzer = new(["NET8_0_OR_GREATER", "NET_8_0_OR_GREATER"]);

    [Fact]
    public void Visit_RemovesSimpleIfEndif()
    {
        const string code = """
                            #if NET8_0_OR_GREATER
                            var x = 1;
                            #endif
                            """;

        var result = ProcessCode(code);

        Assert.DoesNotContain("#if", result);
        Assert.DoesNotContain("#endif", result);
        Assert.Contains("var x = 1;", result);
    }

    [Fact]
    public void Visit_KeepsNet8Code_RemovesElse()
    {
        const string code = """
                            #if NET8_0_OR_GREATER
                            var x = 1;
                            #else
                            var x = 2;
                            #endif
                            """;

        var result = ProcessCode(code);

        Assert.Contains("var x = 1;", result);
        Assert.DoesNotContain("var x = 2;", result);
        Assert.DoesNotContain("#if", result);
        Assert.DoesNotContain("#else", result);
        Assert.DoesNotContain("#endif", result);
    }

    [Fact]
    public void Visit_RemovesNegatedBlock()
    {
        const string code = """
                            #if !NET8_0_OR_GREATER
                            var x = 1;
                            #endif
                            var y = 2;
                            """;

        var result = ProcessCode(code);

        Assert.DoesNotContain("var x = 1;", result);
        Assert.Contains("var y = 2;", result);
    }

    [Fact]
    public void Visit_PreservesDebugConditional()
    {
        const string code = """
                            #if DEBUG
                            var x = 1;
                            #else
                            var x = 2;
                            #endif
                            """;

        var result = ProcessCode(code);

        Assert.Contains("#if DEBUG", result);
        Assert.Contains("#else", result);
        Assert.Contains("#endif", result);
        Assert.Contains("var x = 1;", result);
        Assert.Contains("var x = 2;", result);
    }

    [Fact]
    public void Visit_PreservesNestedDebugInsideNet8()
    {
        const string code = """
                            #if NET8_0_OR_GREATER
                            #if DEBUG
                            var debug = true;
                            #else
                            var debug = false;
                            #endif
                            var x = 1;
                            #endif
                            """;

        var result = ProcessCode(code);

        Assert.DoesNotContain("NET8_0_OR_GREATER", result);
        Assert.Contains("#if DEBUG", result);
        Assert.Contains("#else", result);
        Assert.Contains("#endif", result);
        Assert.Contains("var debug = true;", result);
        Assert.Contains("var debug = false;", result);
        Assert.Contains("var x = 1;", result);
    }

    [Fact]
    public void Visit_PreservesPragmaDirectives()
    {
        const string code = """
                            #if NET8_0_OR_GREATER
                            #pragma warning disable CS0618
                            var x = 1;
                            #pragma warning restore CS0618
                            #endif
                            """;

        var result = ProcessCode(code);

        Assert.Contains("#pragma warning disable CS0618", result);
        Assert.Contains("#pragma warning restore CS0618", result);
    }

    [Fact]
    public void Visit_PreservesRegionDirectives()
    {
        const string code = """
                            #region Test
                            #if NET8_0_OR_GREATER
                            var x = 1;
                            #endif
                            #endregion
                            """;

        var result = ProcessCode(code);

        Assert.Contains("#region Test", result);
        Assert.Contains("#endregion", result);
    }

    private string ProcessCode(string code)
    {
        var options = CSharpParseOptions.Default
            .WithPreprocessorSymbols("NET8_0_OR_GREATER");
        var tree = CSharpSyntaxTree.ParseText(code, options);
        var root = tree.GetRoot();

        var analysis = _analyzer.Analyze(root);
        var simpleBlocks = analysis.Blocks
            .Where(b => b.Complexity != BlockComplexity.Complex)
            .ToList();

        var rewriter = new TargetDirectiveRewriter(simpleBlocks);

        var newRoot = rewriter.Visit(root);
        return newRoot.ToFullString();
    }
}
