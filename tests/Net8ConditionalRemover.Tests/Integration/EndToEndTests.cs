namespace Net8ConditionalRemover.Tests.Integration;

using Net8ConditionalRemover.Models;
using Net8ConditionalRemover.Services;
using Xunit;

public class EndToEndTests : IDisposable
{
    private readonly FileProcessor _processor = new();
    private readonly List<string> _tempFiles = [];

    [Fact]
    public async Task CompleteWorkflow_PatternA_KeepsNet8RemovesElse()
    {
        const string code = """
                            #if NET8_0_OR_GREATER
                            namespace Modern;
                            #else
                            namespace Legacy;
                            #endif
                            """;

        var result = await ProcessAndVerify(code);

        Assert.Equal("namespace Modern;", result.Trim());
    }

    [Fact]
    public async Task CompleteWorkflow_PatternB_RemovesNegatedBlock()
    {
        const string code = """
                            public class Foo
                            {
                            #if !NET8_0_OR_GREATER
                                [Obsolete]
                            #endif
                                public void Bar() { }
                            }
                            """;

        var result = await ProcessAndVerify(code);

        Assert.DoesNotContain("Obsolete", result);
        Assert.Contains("public void Bar()", result);
    }

    [Fact]
    public async Task CompleteWorkflow_PatternC_RemovesWrapper()
    {
        const string code = """
                            #if NET8_0_OR_GREATER
                            using System;
                            namespace Wrapped;
                            #endif
                            """;

        var result = await ProcessAndVerify(code);

        Assert.DoesNotContain("#if", result);
        Assert.Contains("using System;", result);
    }

    [Fact]
    public async Task CompleteWorkflow_MixedConditionals_OnlyRemovesNet8()
    {
        const string code = """
                            #if NET8_0_OR_GREATER
                            using Modern;
                            #else
                            using Legacy;
                            #endif

                            #if DEBUG
                            var debug = true;
                            #else
                            var debug = false;
                            #endif
                            """;

        var result = await ProcessAndVerify(code);

        Assert.Contains("using Modern;", result);
        Assert.DoesNotContain("using Legacy;", result);
        Assert.Contains("#if DEBUG", result);
        Assert.Contains("var debug = true;", result);
        Assert.Contains("var debug = false;", result);
    }

    [Fact]
    public async Task CompleteWorkflow_NestedDebug_PreservesBothBranches()
    {
        const string code = """
                            #if NET8_0_OR_GREATER
                            #if DEBUG
                            var modernDebug = true;
                            #else
                            var modernRelease = true;
                            #endif
                            #else
                            var legacy = true;
                            #endif
                            """;

        var result = await ProcessAndVerify(code);

        Assert.DoesNotContain("var legacy", result);
        Assert.Contains("#if DEBUG", result);
        Assert.Contains("var modernDebug = true;", result);
        Assert.Contains("var modernRelease = true;", result);
    }

    [Fact]
    public async Task CompleteWorkflow_ComplexPattern_InjectsError()
    {
        const string code = """
                            #if NET8_0_OR_GREATER && DEBUG
                            var x = 1;
                            #endif
                            """;
        var path = CreateTempFile(code);

        var processResult = await _processor.ProcessFileAsync(path, new ProcessingOptions { DryRun = true });

        Assert.Equal(ResultStatus.SuccessWithReview, processResult.Status);
        Assert.Contains("#error", processResult.PreviewContent);
        Assert.Contains("NET8_REVIEW_REQUIRED", processResult.PreviewContent);
    }

    /// <summary>
    /// Pattern D: Mid-expression conditional inside method parameters.
    /// Simple if/else patterns are auto-processed - NET8 branch kept, else removed.
    /// Note: Semantic context (parameter list) is not detected; only syntax matters.
    /// </summary>
    [Fact]
    public async Task CompleteWorkflow_PatternD_MidExpression_AutoProcessed()
    {
        const string code = """
                            public class Foo
                            {
                                public void Method(
                            #if NET8_0_OR_GREATER
                                    int param1,
                            #else
                                    string param1,
                            #endif
                                    bool param2)
                                {
                                }
                            }
                            """;
        var path = CreateTempFile(code);

        var processResult = await _processor.ProcessFileAsync(path, new ProcessingOptions { DryRun = true });

        // Simple if/else is auto-processed successfully
        Assert.Equal(ResultStatus.Success, processResult.Status);
        // NET8 branch kept
        Assert.Contains("int param1", processResult.PreviewContent);
        // Else branch removed
        Assert.DoesNotContain("string param1", processResult.PreviewContent);
        // Directives removed
        Assert.DoesNotContain("#if NET8_0_OR_GREATER", processResult.PreviewContent);
    }

    /// <summary>
    /// Pattern E: Constructor parameter conditional with different signatures.
    /// Simple if/else patterns are auto-processed - NET8 branch kept, else removed.
    /// Note: Different parameter counts are NOT detected; callers may need manual updates.
    /// </summary>
    [Fact]
    public async Task CompleteWorkflow_PatternE_ConstructorParam_AutoProcessed()
    {
        const string code = """
                            public class Service
                            {
                                public Service(
                            #if NET8_0_OR_GREATER
                                    ILogger<Service> logger,
                                    IOptions<ServiceOptions> options
                            #else
                                    ILogger logger
                            #endif
                                )
                                {
                                }
                            }
                            """;
        var path = CreateTempFile(code);

        var processResult = await _processor.ProcessFileAsync(path, new ProcessingOptions { DryRun = true });

        // Simple if/else is auto-processed successfully
        Assert.Equal(ResultStatus.Success, processResult.Status);
        // NET8 branch kept
        Assert.Contains("ILogger<Service>", processResult.PreviewContent);
        Assert.Contains("IOptions<ServiceOptions>", processResult.PreviewContent);
        // Else branch removed
        Assert.DoesNotContain("ILogger logger", processResult.PreviewContent);
        // Directives removed
        Assert.DoesNotContain("#if NET8_0_OR_GREATER", processResult.PreviewContent);
    }

    /// <summary>
    /// Edge case: Indented directives.
    /// Roslyn handles indentation correctly, but we verify the behavior.
    /// </summary>
    [Fact]
    public async Task CompleteWorkflow_IndentedDirective_ProcessesCorrectly()
    {
        const string code = """
                            public class Foo
                            {
                                public void Bar()
                                {
                                    #if NET8_0_OR_GREATER
                                    var modern = true;
                                    #else
                                    var legacy = true;
                                    #endif
                                }
                            }
                            """;

        var result = await ProcessAndVerify(code);

        Assert.Contains("var modern = true;", result);
        Assert.DoesNotContain("var legacy = true;", result);
        Assert.DoesNotContain("#if NET8_0_OR_GREATER", result);
    }

    /// <summary>
    /// Edge case: Commented directive should NOT be processed.
    /// Lines like "// #if NET8_0_OR_GREATER" are comments, not directives.
    /// </summary>
    [Fact]
    public async Task CompleteWorkflow_CommentedDirective_Ignored()
    {
        const string code = """
                            // This is a comment explaining the pattern:
                            // #if NET8_0_OR_GREATER
                            // We use this for modern code paths
                            // #endif
                            public class Foo
                            {
                                public void Bar() { }
                            }
                            """;
        var path = CreateTempFile(code);

        var result = await _processor.ProcessFileAsync(path, new ProcessingOptions { DryRun = true });

        // Commented directives should remain unchanged
        Assert.Contains("// #if NET8_0_OR_GREATER", result.PreviewContent);
        Assert.Contains("// #endif", result.PreviewContent);
        // No actual directives were found/removed
        Assert.Equal(0, result.BlocksRemoved);
    }

    private async Task<string> ProcessAndVerify(string code)
    {
        var path = CreateTempFile(code);
        var result = await _processor.ProcessFileAsync(path, new ProcessingOptions { DryRun = true });

        Assert.True(
            result.Status == ResultStatus.Success || result.Status == ResultStatus.SuccessWithReview,
            $"Processing failed: {string.Join(", ", result.Errors ?? [])}");

        return result.PreviewContent ?? "";
    }

    private string CreateTempFile(string content)
    {
        var path = Path.GetTempFileName() + ".cs";
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles.Where(File.Exists))
        {
            File.Delete(file);
        }
    }
}
