namespace Net8ConditionalRemover.Tests.Rewriters;

using Microsoft.CodeAnalysis.CSharp;
using Net8ConditionalRemover.Models;
using Net8ConditionalRemover.Rewriters;
using Net8ConditionalRemover.Services;
using Xunit;

public class ErrorDirectiveInjectorTests
{
    private readonly ConditionalAnalyzer _analyzer = new(["NET8_0_OR_GREATER", "NET_8_0_OR_GREATER"]);

    [Theory]
    [InlineData("#if NET8_0_OR_GREATER\nvar x = 1;\n#elif DEBUG\nvar x = 2;\n#endif", true, "#elif")]
    [InlineData("#if NET8_0_OR_GREATER && DEBUG\nvar x = 1;\n#endif", true, "Boolean expression")]
    [InlineData("#if NET8_0_OR_GREATER\nvar x = 1;\n#endif", false, "")]
    [InlineData("#if NET8_0_OR_GREATER\nvar x = 1;\n#else\nvar x = 2;\n#endif", false, "")]
    public void Visit_InjectsErrorForComplexBlocksOnly(string code, bool expectsError, string expectedContent)
    {
        var result = ProcessCode(code);

        if (expectsError)
        {
            Assert.Contains("#error", result);
            Assert.Contains("NET8_REVIEW_REQUIRED", result);
            Assert.Contains(expectedContent, result);
        }
        else
        {
            Assert.DoesNotContain("#error", result);
        }
    }

    [Fact]
    public void Visit_PreservesOriginalCode()
    {
        const string code = """
                            #if NET8_0_OR_GREATER && DEBUG
                            var x = 1;
                            #endif
                            """;

        var result = ProcessCode(code);

        Assert.Contains("#if NET8_0_OR_GREATER && DEBUG", result);
        Assert.Contains("var x = 1;", result);
        Assert.Contains("#endif", result);
    }

    private string ProcessCode(string code)
    {
        var options = CSharpParseOptions.Default
            .WithPreprocessorSymbols("NET8_0_OR_GREATER");
        var tree = CSharpSyntaxTree.ParseText(code, options);
        var root = tree.GetRoot();

        var analysis = _analyzer.Analyze(root);
        var complexBlocks = analysis.Blocks
            .Where(b => b.Complexity == BlockComplexity.Complex)
            .ToList();

        if (complexBlocks.Count == 0)
            return root.ToFullString();

        var injector = new ErrorDirectiveInjector(complexBlocks);
        var newRoot = injector.Visit(root);
        return newRoot.ToFullString();
    }
}
