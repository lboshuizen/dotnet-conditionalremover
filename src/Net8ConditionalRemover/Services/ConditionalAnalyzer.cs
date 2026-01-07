namespace Net8ConditionalRemover.Services;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Net8ConditionalRemover.Models;

/// <summary>
/// Analyzes a syntax tree to find and classify conditional blocks for the target symbol.
/// </summary>
public class ConditionalAnalyzer(string[] targetSymbols)
{
    public AnalysisResult Analyze(SyntaxNode root)
    {
        var blocks = new List<DirectiveBlock>();
        var issues = new List<AnalysisIssue>();

        // Extract all directives once, outside the loop
        var allDirectives = root.DescendantTrivia()
            .Where(t => t.IsDirective)
            .Select(t => t.GetStructure())
            .OfType<DirectiveTriviaSyntax>()
            .OrderBy(d => d.SpanStart)
            .ToList();

        var ifDirectives = allDirectives
            .OfType<IfDirectiveTriviaSyntax>()
            .Where(IsTargetDirective);

        foreach (var ifDir in ifDirectives)
        {
            var block = BuildBlock(ifDir, root, allDirectives);
            if (block is null)
            {
                issues.Add(new AnalysisIssue(
                    ifDir.GetLocation(),
                    "Unmatched #if directive"));
                continue;
            }

            if (block.HasElif)
            {
                issues.Add(new AnalysisIssue(
                    ifDir.GetLocation(),
                    "Complex conditional with #elif - requires manual review"));
            }

            if (block.HasBooleanExpression)
            {
                issues.Add(new AnalysisIssue(
                    ifDir.GetLocation(),
                    "Boolean expression (&&/||) - requires manual review"));
            }

            blocks.Add(block);
        }

        return new AnalysisResult(blocks, issues);
    }

    private bool IsTargetDirective(IfDirectiveTriviaSyntax directive)
    {
        var condition = directive.Condition.ToString();
        return targetSymbols.Any(s =>
            condition.Contains(s, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsNegatedTarget(IfDirectiveTriviaSyntax directive)
    {
        if (directive.Condition is PrefixUnaryExpressionSyntax prefix
            && prefix.IsKind(SyntaxKind.LogicalNotExpression))
        {
            return prefix.Operand is IdentifierNameSyntax;
        }
        return false;
    }

    private static bool IsNegatedBooleanExpression(IfDirectiveTriviaSyntax directive)
    {
        if (directive.Condition is PrefixUnaryExpressionSyntax prefix
            && prefix.IsKind(SyntaxKind.LogicalNotExpression))
        {
            return prefix.Operand is ParenthesizedExpressionSyntax paren
                && paren.Expression is BinaryExpressionSyntax;
        }
        return false;
    }

    private bool HasBooleanExpression(IfDirectiveTriviaSyntax directive)
    {
        // If it's a negated boolean expression, don't also flag it as HasBooleanExpression
        // since IsNegatedBoolean is a more specific classification
        if (IsNegatedBooleanExpression(directive))
            return false;

        return ContainsBooleanExpression(directive.Condition);
    }

    private static bool ContainsBooleanExpression(ExpressionSyntax expr)
    {
        return expr switch
        {
            BinaryExpressionSyntax binary when
                binary.IsKind(SyntaxKind.LogicalAndExpression) ||
                binary.IsKind(SyntaxKind.LogicalOrExpression) => true,
            PrefixUnaryExpressionSyntax prefix => ContainsBooleanExpression(prefix.Operand),
            ParenthesizedExpressionSyntax paren => ContainsBooleanExpression(paren.Expression),
            _ => false
        };
    }

    private DirectiveBlock? BuildBlock(
        IfDirectiveTriviaSyntax ifDir,
        SyntaxNode root,
        List<DirectiveTriviaSyntax> allDirectives)
    {
        var ifIndex = allDirectives.IndexOf(ifDir);
        if (ifIndex < 0) return null;

        ElseDirectiveTriviaSyntax? elseDir = null;
        var elifDirs = new List<ElifDirectiveTriviaSyntax>();
        EndIfDirectiveTriviaSyntax? endifDir = null;

        int depth = 1;
        for (int i = ifIndex + 1; i < allDirectives.Count && depth > 0; i++)
        {
            switch (allDirectives[i])
            {
                case IfDirectiveTriviaSyntax:
                    depth++;
                    break;
                case ElseDirectiveTriviaSyntax e when depth == 1:
                    elseDir = e;
                    break;
                case ElifDirectiveTriviaSyntax elif when depth == 1:
                    elifDirs.Add(elif);
                    break;
                case EndIfDirectiveTriviaSyntax end:
                    depth--;
                    if (depth == 0) endifDir = end;
                    break;
            }
        }

        if (endifDir is null) return null;

        var isNegated = IsNegatedTarget(ifDir);
        var hasElse = elseDir is not null;

        // Collect disabled spans before creating the block
        var disabledSpans = CollectDisabledTextSpans(
            ifDir, elseDir, endifDir, isNegated, hasElse, root);

        return new DirectiveBlock
        {
            IfDirective = ifDir,
            ElseDirective = elseDir,
            ElifDirectives = elifDirs,
            EndIfDirective = endifDir,
            IsNegated = isNegated,
            IsNegatedBoolean = IsNegatedBooleanExpression(ifDir),
            HasBooleanExpression = HasBooleanExpression(ifDir),
            AssociatedDisabledTextSpans = disabledSpans
        };
    }

    private static List<TextSpan> CollectDisabledTextSpans(
        IfDirectiveTriviaSyntax ifDir,
        ElseDirectiveTriviaSyntax? elseDir,
        EndIfDirectiveTriviaSyntax endifDir,
        bool isNegated,
        bool hasElse,
        SyntaxNode root)
    {
        var result = new List<TextSpan>();

        if (!hasElse && !isNegated)
            return result;

        int disabledStart, disabledEnd;

        if (isNegated)
        {
            disabledStart = ifDir.Span.End;
            disabledEnd = endifDir.SpanStart;
        }
        else if (hasElse)
        {
            disabledStart = elseDir!.Span.End;
            disabledEnd = endifDir.SpanStart;
        }
        else
        {
            return result;
        }

        var disabledTrivia = root.DescendantTrivia()
            .Where(t => t.IsKind(SyntaxKind.DisabledTextTrivia))
            .Where(t => t.SpanStart >= disabledStart && t.Span.End <= disabledEnd);

        result.AddRange(disabledTrivia.Select(t => t.Span));
        return result;
    }
}
