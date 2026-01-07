namespace Net8ConditionalRemover.Rewriters;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Net8ConditionalRemover.Models;

/// <summary>
/// Injects #error directives before complex conditional blocks.
/// This ensures builds fail until a human reviews and resolves them.
/// </summary>
public class ErrorDirectiveInjector : CSharpSyntaxRewriter
{
    private readonly Dictionary<int, DirectiveBlock> _blocksBySpan;
    private readonly HashSet<int> _compilationFailedSpans;

    public ErrorDirectiveInjector(IEnumerable<DirectiveBlock> complexBlocks)
        : this(complexBlocks, [])
    {
    }

    /// <summary>
    /// Creates an error injector for blocks that need review.
    /// </summary>
    /// <param name="blocksNeedingReview">Blocks that need #error injection</param>
    /// <param name="compilationFailedSpans">SpanStart values of blocks that failed compilation verification</param>
    public ErrorDirectiveInjector(
        IEnumerable<DirectiveBlock> blocksNeedingReview,
        HashSet<int> compilationFailedSpans)
        : base(visitIntoStructuredTrivia: true)
    {
        _blocksBySpan = blocksNeedingReview
            .Where(b => b.IfDirective is not null)
            .ToDictionary(b => b.IfDirective!.SpanStart);
        _compilationFailedSpans = compilationFailedSpans;
    }

    public override SyntaxToken VisitToken(SyntaxToken token)
    {
        var newLeading = ProcessLeadingTrivia(token.LeadingTrivia);

        return newLeading != token.LeadingTrivia ? token.WithLeadingTrivia(newLeading) : token;
    }

    private SyntaxTriviaList ProcessLeadingTrivia(SyntaxTriviaList triviaList)
    {
        var newTrivia = new List<SyntaxTrivia>();

        foreach (var trivia in triviaList)
        {
            if (trivia.HasStructure &&
                trivia.GetStructure() is IfDirectiveTriviaSyntax ifDirective &&
                _blocksBySpan.TryGetValue(ifDirective.SpanStart, out var block))
            {
                var isCompilationFailure = _compilationFailedSpans.Contains(ifDirective.SpanStart);
                var errorTrivia = CreateErrorDirective(block, isCompilationFailure);
                newTrivia.Add(errorTrivia);
            }

            newTrivia.Add(trivia);
        }

        return new SyntaxTriviaList(newTrivia);
    }

    private static SyntaxTrivia CreateErrorDirective(DirectiveBlock block, bool isCompilationFailure)
    {
        string reason;

        if (isCompilationFailure)
        {
            reason = "Transformation caused compilation error";
        }
        else if (block.HasElif)
        {
            reason = "Complex conditional with #elif branches";
        }
        else if (block.HasBooleanExpression)
        {
            reason = "Boolean expression (&&/||) requires manual simplification";
        }
        else
        {
            reason = "Complex conditional pattern";
        }

        var errorDirective = SyntaxFactory.ErrorDirectiveTrivia(
            SyntaxFactory.Token(SyntaxKind.HashToken),
            SyntaxFactory.Token(SyntaxKind.ErrorKeyword),
            SyntaxFactory.Token(
                SyntaxFactory.TriviaList(),
                SyntaxKind.EndOfDirectiveToken,
                $" NET8_REVIEW_REQUIRED: {reason}",
                $" NET8_REVIEW_REQUIRED: {reason}",
                SyntaxFactory.TriviaList(SyntaxFactory.LineFeed)),
            true);

        return SyntaxFactory.Trivia(errorDirective);
    }
}
