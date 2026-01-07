namespace Net8ConditionalRemover.Tests.Models;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Net8ConditionalRemover.Models;
using Xunit;

public class DirectiveBlockTests
{
    [Fact]
    public void Complexity_Simple_WhenNoElifNoBooleanNoNegation()
    {
        var block = new DirectiveBlock
        {
            ElifDirectives = [],
            HasBooleanExpression = false,
            IsNegatedBoolean = false,
            IsNegated = false
        };

        Assert.Equal(BlockComplexity.Simple, block.Complexity);
    }

    [Fact]
    public void Complexity_Negated_WhenIsNegatedOnly()
    {
        var block = new DirectiveBlock
        {
            ElifDirectives = [],
            HasBooleanExpression = false,
            IsNegatedBoolean = false,
            IsNegated = true
        };

        Assert.Equal(BlockComplexity.Negated, block.Complexity);
    }

    [Fact]
    public void Complexity_Complex_WhenHasElif()
    {
        var elifDirective = CreateElifDirective();
        var block = new DirectiveBlock
        {
            ElifDirectives = [elifDirective],
            HasBooleanExpression = false,
            IsNegatedBoolean = false,
            IsNegated = false
        };

        Assert.Equal(BlockComplexity.Complex, block.Complexity);
    }

    [Fact]
    public void Complexity_Complex_WhenHasBooleanExpression()
    {
        var block = new DirectiveBlock
        {
            ElifDirectives = [],
            HasBooleanExpression = true,
            IsNegatedBoolean = false,
            IsNegated = false
        };

        Assert.Equal(BlockComplexity.Complex, block.Complexity);
    }

    [Fact]
    public void Complexity_Complex_WhenIsNegatedBoolean()
    {
        var block = new DirectiveBlock
        {
            ElifDirectives = [],
            HasBooleanExpression = false,
            IsNegatedBoolean = true,
            IsNegated = false
        };

        Assert.Equal(BlockComplexity.Complex, block.Complexity);
    }

    private static ElifDirectiveTriviaSyntax CreateElifDirective()
    {
        var code = @"
#if DEBUG
#elif RELEASE
#endif
";
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();
        return root.DescendantTrivia()
            .Select(t => t.GetStructure())
            .OfType<ElifDirectiveTriviaSyntax>()
            .First();
    }
}
