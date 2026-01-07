namespace Net8ConditionalRemover.Tests.Services;

using Microsoft.CodeAnalysis.CSharp;
using Net8ConditionalRemover.Models;
using Net8ConditionalRemover.Services;
using Xunit;

public class ConditionalAnalyzerTests
{
    private readonly ConditionalAnalyzer _analyzer = new(["NET8_0_OR_GREATER", "NET_8_0_OR_GREATER"]);

    [Theory]
    [InlineData("NET8_0_OR_GREATER")]
    [InlineData("NET_8_0_OR_GREATER")]
    public void Analyze_FindsSymbolVariants(string symbol)
    {
        var code = $"""
            #if {symbol}
            var x = 1;
            #endif
            """;
        var root = ParseWithNet8(code);

        var result = _analyzer.Analyze(root);

        Assert.Single(result.Blocks);
    }

    [Fact]
    public void Analyze_IgnoresNonNet8Blocks()
    {
        const string code = """
                            #if DEBUG
                            var x = 1;
                            #endif
                            """;
        var root = ParseWithNet8(code);

        var result = _analyzer.Analyze(root);

        Assert.Empty(result.Blocks);
    }

    [Theory]
    [InlineData("#if NET8_0_OR_GREATER\nvar x = 1;\n#endif", BlockComplexity.Simple, false, false, false, false)]
    [InlineData("#if !NET8_0_OR_GREATER\nvar x = 1;\n#endif", BlockComplexity.Negated, true, false, false, false)]
    [InlineData("#if NET8_0_OR_GREATER\nvar x = 1;\n#elif DEBUG\nvar x = 2;\n#endif", BlockComplexity.Complex, false, true, false, false)]
    [InlineData("#if NET8_0_OR_GREATER && DEBUG\nvar x = 1;\n#endif", BlockComplexity.Complex, false, false, true, false)]
    [InlineData("#if !(NET8_0_OR_GREATER && DEBUG)\nvar x = 1;\n#endif", BlockComplexity.Complex, false, false, false, true)]
    public void Analyze_ClassifiesBlockCorrectly(
        string code,
        BlockComplexity expectedComplexity,
        bool isNegated,
        bool hasElif,
        bool hasBooleanExpression,
        bool isNegatedBoolean)
    {
        var root = ParseWithNet8(code);

        var result = _analyzer.Analyze(root);

        Assert.Single(result.Blocks);
        var block = result.Blocks[0];
        Assert.Equal(expectedComplexity, block.Complexity);
        Assert.Equal(isNegated, block.IsNegated);
        Assert.Equal(hasElif, block.HasElif);
        Assert.Equal(hasBooleanExpression, block.HasBooleanExpression);
        Assert.Equal(isNegatedBoolean, block.IsNegatedBoolean);
    }

    [Fact]
    public void Analyze_DetectsElseBranch()
    {
        const string code = """
                            #if NET8_0_OR_GREATER
                            var x = 1;
                            #else
                            var x = 2;
                            #endif
                            """;
        var root = ParseWithNet8(code);

        var result = _analyzer.Analyze(root);

        Assert.True(result.Blocks[0].HasElse);
    }

    [Fact]
    public void Analyze_FindsMultipleBlocks()
    {
        const string code = """
                            #if NET8_0_OR_GREATER
                            var x = 1;
                            #endif

                            #if NET8_0_OR_GREATER
                            var y = 2;
                            #endif
                            """;
        var root = ParseWithNet8(code);

        var result = _analyzer.Analyze(root);

        Assert.Equal(2, result.Blocks.Count);
    }

    private static Microsoft.CodeAnalysis.SyntaxNode ParseWithNet8(string code)
    {
        var options = CSharpParseOptions.Default
            .WithPreprocessorSymbols("NET8_0_OR_GREATER");
        return CSharpSyntaxTree.ParseText(code, options).GetRoot();
    }
}
