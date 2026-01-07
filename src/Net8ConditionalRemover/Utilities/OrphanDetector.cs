namespace Net8ConditionalRemover.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Detects orphaned preprocessor directives (#else/#endif without matching #if)
/// after transformation. This is a critical safety check.
/// Uses Roslyn diagnostics (CS1028) for orphaned #else/#elif/#endif (not parsed as directives)
/// and stack-based tracking for orphaned #if (missing #endif).
/// </summary>
public static class OrphanDetector
{
    public record OrphanedDirective(SyntaxKind Kind, int Line, string Text);

    private const string CS1028 = "CS1028"; // Unexpected preprocessor directive

    public static List<OrphanedDirective> Detect(SyntaxNode root)
    {
        var orphans = new List<OrphanedDirective>();

        // Roslyn doesn't parse orphaned #else/#elif/#endif as directive trivia - they generate CS1028
        foreach (var diagnostic in root.SyntaxTree.GetDiagnostics(root))
        {
            if (diagnostic.Id != CS1028) continue;

            var linePosition = diagnostic.Location.GetLineSpan().StartLinePosition;
            var line = linePosition.Line + 1;
            var lineText = root.SyntaxTree.GetText().Lines[linePosition.Line].ToString().Trim();

            var (kind, message) = lineText switch
            {
                _ when lineText.StartsWith("#endif") => (SyntaxKind.EndIfDirectiveTrivia, "#endif without matching #if"),
                _ when lineText.StartsWith("#elif") => (SyntaxKind.ElifDirectiveTrivia, "#elif without matching #if"),
                _ when lineText.StartsWith("#else") => (SyntaxKind.ElseDirectiveTrivia, "#else without matching #if"),
                _ => (SyntaxKind.None, null as string)
            };

            if (message is not null)
                orphans.Add(new OrphanedDirective(kind, line, message));
        }

        // Stack-based detection for orphaned #if (missing #endif)
        var ifStack = new Stack<DirectiveTriviaSyntax>();
        var allDirectives = root.DescendantTrivia()
            .Where(t => t.IsDirective)
            .Select(t => t.GetStructure())
            .OfType<DirectiveTriviaSyntax>()
            .OrderBy(d => d.SpanStart);

        foreach (var directive in allDirectives)
        {
            switch (directive)
            {
                case IfDirectiveTriviaSyntax:
                    ifStack.Push(directive);
                    break;
                case EndIfDirectiveTriviaSyntax when ifStack.Count > 0:
                    ifStack.Pop();
                    break;
            }
        }

        while (ifStack.Count > 0)
        {
            var orphan = ifStack.Pop();
            orphans.Add(new OrphanedDirective(
                SyntaxKind.IfDirectiveTrivia,
                GetLineNumber(orphan),
                "#if without matching #endif"));
        }

        return orphans;
    }

    private static int GetLineNumber(DirectiveTriviaSyntax directive)
    {
        return directive.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    }
}
