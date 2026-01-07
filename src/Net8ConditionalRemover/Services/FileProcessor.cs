namespace Net8ConditionalRemover.Services;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Net8ConditionalRemover.Models;
using Net8ConditionalRemover.Rewriters;
using Net8ConditionalRemover.Utilities;

public class FileProcessor
{
    public async Task<ProcessingResult> ProcessFileAsync(
        string filePath,
        ProcessingOptions options)
    {
        string? backupPath = null;

        try
        {
            if (options is { CreateBackup: true, DryRun: false })
            {
                backupPath = await FileBackup.CreateBackupAsync(filePath);
            }

            var (content, hasBom) = await BomHandler.ReadWithBomDetectionAsync(filePath);

            // Check if file is generated (content-based detection)
            if (!options.IncludeGenerated && GeneratedFileDetector.IsGeneratedFile(filePath, content))
            {
                return ProcessingResult.Skipped(filePath, "Generated file");
            }

            var lineEnding = LineEndingHandler.Detect(content);

            var parseOptions = CSharpParseOptions.Default
                .WithPreprocessorSymbols(options.PreprocessorSymbols);

            var tree = CSharpSyntaxTree.ParseText(content, parseOptions, path: filePath);
            var root = await tree.GetRootAsync();

            var analyzer = new ConditionalAnalyzer(options.TargetSymbolsWithAliases);
            var analysis = analyzer.Analyze(root);

            var simpleBlocks = analysis.Blocks
                .Where(b => b.Complexity != BlockComplexity.Complex)
                .ToList();
            var complexBlocks = analysis.Blocks
                .Where(b => b.Complexity == BlockComplexity.Complex)
                .ToList();

            // Apply transformations with compilation verification gate
            var (newRoot, blocksRemoved, blocksFlagged) = ApplyTransformationsWithVerification(
                root, simpleBlocks, complexBlocks, options, parseOptions);

            var orphans = OrphanDetector.Detect(newRoot);
            if (orphans.Count > 0)
            {
                var validationErrors = orphans
                    .Select(o => $"Orphaned directive at line {o.Line}: {o.Text}")
                    .ToList();
                if (backupPath is not null)
                    FileBackup.RestoreBackup(backupPath, filePath);
                return ProcessingResult.Failed(filePath, validationErrors);
            }

            var output = newRoot.ToFullString();
            output = WhitespaceNormalizer.Normalize(output, lineEnding);

            if (!options.DryRun)
            {
                await BomHandler.WriteWithBomAsync(filePath, output, hasBom);

                // Post-write verification: re-read and verify content matches
                var (verifyContent, _) = await BomHandler.ReadWithBomDetectionAsync(filePath);
                if (verifyContent != output)
                {
                    if (backupPath is not null)
                        FileBackup.RestoreBackup(backupPath, filePath);
                    throw new IOException($"Write verification failed for {filePath}: content mismatch after write");
                }

                // Write succeeded - clean up backup
                if (backupPath is not null)
                {
                    FileBackup.CleanupBackup(backupPath);
                }
            }

            return ProcessingResult.Success(
                filePath,
                blocksRemoved,
                blocksFlagged,
                analysis.Issues,
                options.DryRun ? (hasBom ? "\uFEFF" + output : output) : null);
        }
        catch (Exception ex)
        {
            if (backupPath is not null)
                FileBackup.RestoreBackup(backupPath, filePath);
            return ProcessingResult.Failed(filePath, [$"Unexpected error: {ex.Message}"]);
        }
    }

    /// <summary>
    /// Applies transformations with a compilation verification gate.
    /// If a transformation causes syntax errors, that block is reverted and
    /// an #error directive is injected instead.
    /// </summary>
    private (SyntaxNode root, int blocksRemoved, int blocksFlagged) ApplyTransformationsWithVerification(
        SyntaxNode originalRoot,
        List<DirectiveBlock> simpleBlocks,
        List<DirectiveBlock> complexBlocks,
        ProcessingOptions options,
        CSharpParseOptions parseOptions)
    {
        // First, try to transform all simple blocks at once
        var rewriter = new TargetDirectiveRewriter(simpleBlocks);
        var transformedRoot = rewriter.Visit(originalRoot);

        // Verify syntax after transformation
        var syntaxErrors = GetNonReviewSyntaxErrors(transformedRoot);

        List<DirectiveBlock> successfulBlocks;
        List<DirectiveBlock> failedBlocks;

        if (syntaxErrors.Count == 0)
        {
            // All transformations succeeded
            successfulBlocks = simpleBlocks;
            failedBlocks = [];
        }
        else
        {
            // Some transformations caused syntax errors - fall back to per-block processing
            (transformedRoot, successfulBlocks, failedBlocks) = ApplyBlocksIndividually(
                originalRoot, simpleBlocks, options, parseOptions);
        }

        // Combine failed blocks with complex blocks for #error injection
        var blocksNeedingReview = complexBlocks.Concat(failedBlocks).ToList();

        if (blocksNeedingReview.Count > 0)
        {
            var errorInjector = new ErrorDirectiveInjector(
                blocksNeedingReview,
                failedBlocks.Select(b => b.IfDirective?.SpanStart ?? -1).ToHashSet());
            transformedRoot = errorInjector.Visit(transformedRoot);
        }

        return (transformedRoot, successfulBlocks.Count, blocksNeedingReview.Count);
    }

    /// <summary>
    /// Applies blocks one at a time, verifying syntax after each.
    /// Blocks that cause syntax errors are reverted.
    /// </summary>
    private (SyntaxNode root, List<DirectiveBlock> successful, List<DirectiveBlock> failed) ApplyBlocksIndividually(
        SyntaxNode originalRoot,
        List<DirectiveBlock> blocks,
        ProcessingOptions options,
        CSharpParseOptions parseOptions)
    {
        var currentRoot = originalRoot;
        var successfulBlocks = new List<DirectiveBlock>();
        var failedBlocks = new List<DirectiveBlock>();

        foreach (var block in blocks)
        {
            // Try to apply this single block
            var rewriter = new TargetDirectiveRewriter([block]);
            var testRoot = rewriter.Visit(currentRoot);

            var syntaxErrors = GetNonReviewSyntaxErrors(testRoot);

            if (syntaxErrors.Count == 0)
            {
                // This block transformed cleanly
                currentRoot = testRoot;
                successfulBlocks.Add(block);
            }
            else
            {
                // This block caused syntax errors - mark it for #error injection
                failedBlocks.Add(block);
            }
        }

        return (currentRoot, successfulBlocks, failedBlocks);
    }

    /// <summary>
    /// Gets syntax errors from the transformed tree, excluding #error NET8_REVIEW_REQUIRED directives.
    /// </summary>
    private static List<Diagnostic> GetNonReviewSyntaxErrors(SyntaxNode root)
    {
        return root.SyntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Where(d => !d.GetMessage().Contains("NET8_REVIEW_REQUIRED"))
            .ToList();
    }
}
