namespace Net8ConditionalRemover.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Represents a complete #if..#endif block with all branches.
/// Tracks the span of all associated DisabledTextTrivia for targeted removal.
/// </summary>
public class DirectiveBlock
{
    public IfDirectiveTriviaSyntax? IfDirective { get; init; }
    public ElseDirectiveTriviaSyntax? ElseDirective { get; init; }
    public List<ElifDirectiveTriviaSyntax> ElifDirectives { get; init; } = [];
    public EndIfDirectiveTriviaSyntax? EndIfDirective { get; init; }

    /// <summary>
    /// Spans of DisabledTextTrivia that belong to THIS block only.
    /// Used to avoid removing DisabledTextTrivia from non-NET8 conditionals.
    /// </summary>
    public List<TextSpan> AssociatedDisabledTextSpans { get; init; } = [];

    public bool IsNegated { get; init; }
    public bool IsNegatedBoolean { get; init; }
    public bool HasBooleanExpression { get; init; }
    public bool HasElse => ElseDirective is not null;
    public bool HasElif => ElifDirectives.Count > 0;

    public BlockComplexity Complexity => (HasElif, HasBooleanExpression, IsNegatedBoolean, IsNegated) switch
    {
        (true, _, _, _) => BlockComplexity.Complex,
        (_, true, _, _) => BlockComplexity.Complex,
        (_, _, true, _) => BlockComplexity.Complex,
        (false, false, false, true) => BlockComplexity.Negated,
        (false, false, false, false) => BlockComplexity.Simple
    };
}
