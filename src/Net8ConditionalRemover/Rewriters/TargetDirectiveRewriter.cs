namespace Net8ConditionalRemover.Rewriters;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Net8ConditionalRemover.Models;

/// <summary>
/// Rewrites syntax tree to remove target conditionals.
/// CRITICAL: Non-target conditional blocks must remain untouched!
/// </summary>
public class TargetDirectiveRewriter : CSharpSyntaxRewriter
{
    private readonly HashSet<int> _directiveSpansToRemove = [];
    private readonly HashSet<TextSpan> _disabledTextSpansToRemove = [];

    public TargetDirectiveRewriter(IEnumerable<DirectiveBlock> blocks)
        : base(visitIntoStructuredTrivia: true)
    {
        foreach (var block in blocks)
        {
            if (block.IfDirective is not null)
                _directiveSpansToRemove.Add(block.IfDirective.SpanStart);
            if (block.ElseDirective is not null)
                _directiveSpansToRemove.Add(block.ElseDirective.SpanStart);
            if (block.EndIfDirective is not null)
                _directiveSpansToRemove.Add(block.EndIfDirective.SpanStart);

            foreach (var span in block.AssociatedDisabledTextSpans)
            {
                _disabledTextSpansToRemove.Add(span);
            }
        }
    }

    public override SyntaxToken VisitToken(SyntaxToken token)
    {
        var newLeading = ProcessTrivia(token.LeadingTrivia);
        var newTrailing = ProcessTrivia(token.TrailingTrivia);

        if (newLeading != token.LeadingTrivia ||
            newTrailing != token.TrailingTrivia)
        {
            return token
                .WithLeadingTrivia(newLeading)
                .WithTrailingTrivia(newTrailing);
        }

        return token;
    }

    private SyntaxTriviaList ProcessTrivia(SyntaxTriviaList triviaList)
    {
        var newTrivia = new List<SyntaxTrivia>();

        foreach (var trivia in triviaList)
        {
            if (trivia.IsKind(SyntaxKind.DisabledTextTrivia))
            {
                if (_disabledTextSpansToRemove.Contains(trivia.Span))
                    continue;
            }

            if (trivia.HasStructure)
            {
                var structure = trivia.GetStructure();
                if (structure is DirectiveTriviaSyntax directive)
                {
                    // Only remove directives that are explicitly in our blocks collection
                    // DO NOT remove any directive just because it mentions the target symbol
                    // Complex blocks need their directives preserved for #error injection
                    if (_directiveSpansToRemove.Contains(directive.SpanStart))
                        continue;
                }
            }

            newTrivia.Add(trivia);
        }

        return new SyntaxTriviaList(newTrivia);
    }
}
