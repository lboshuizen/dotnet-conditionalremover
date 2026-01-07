using System.CommandLine;
using System.Text.Json;
using Net8ConditionalRemover.Models;
using Net8ConditionalRemover.Services;
using Net8ConditionalRemover.Utilities;

var rootCommand = new RootCommand("Remove NET8_0_OR_GREATER conditionals from C# files");

var pathArg = new Argument<string>("path", "File or directory to process");
var dryRunOption = new Option<bool>("--dry-run", "Preview changes without modifying files");
var verboseOption = new Option<bool>("--verbose", "Show detailed output");
var includeGeneratedOption = new Option<bool>(
    "--include-generated",
    "Process generated files (*.g.cs, *.Designer.cs, etc.)");
var backupOption = new Option<bool>(
    "--backup",
    "Create .bak files before modifying (recommended)");
var parallelOption = new Option<bool>(
    "--parallel",
    "Process files in parallel (faster for large codebases)");
var reportOption = new Option<string?>(
    "--report",
    "Write JSON report to specified path");
var targetOption = new Option<string>(
    "--target",
    () => "NET8_0_OR_GREATER",
    "Symbol to target for removal (default: NET8_0_OR_GREATER)");
var defineOption = new Option<string[]>(
    "--define",
    () => [],
    "Additional preprocessor symbols to define during parsing (repeatable)");
defineOption.AllowMultipleArgumentsPerToken = true;
var failOnReviewOption = new Option<bool>(
    "--fail-on-review",
    "Exit with code 2 if any blocks need manual review (CI gate)");

rootCommand.AddArgument(pathArg);
rootCommand.AddOption(dryRunOption);
rootCommand.AddOption(verboseOption);
rootCommand.AddOption(includeGeneratedOption);
rootCommand.AddOption(backupOption);
rootCommand.AddOption(parallelOption);
rootCommand.AddOption(reportOption);
rootCommand.AddOption(targetOption);
rootCommand.AddOption(defineOption);
rootCommand.AddOption(failOnReviewOption);

rootCommand.SetHandler(async (context) =>
{
    var path = context.ParseResult.GetValueForArgument(pathArg);
    var options = new ProcessingOptions
    {
        DryRun = context.ParseResult.GetValueForOption(dryRunOption),
        Verbose = context.ParseResult.GetValueForOption(verboseOption),
        IncludeGenerated = context.ParseResult.GetValueForOption(includeGeneratedOption),
        CreateBackup = context.ParseResult.GetValueForOption(backupOption),
        Parallel = context.ParseResult.GetValueForOption(parallelOption),
        ReportPath = context.ParseResult.GetValueForOption(reportOption),
        TargetSymbol = context.ParseResult.GetValueForOption(targetOption) ?? "NET8_0_OR_GREATER",
        AdditionalDefines = context.ParseResult.GetValueForOption(defineOption) ?? [],
        FailOnReview = context.ParseResult.GetValueForOption(failOnReviewOption)
    };

    var processor = new FileProcessor();
    var files = GetCSharpFiles(path, options.IncludeGenerated).ToList();

    Console.WriteLine($"Found {files.Count} files to process");
    if (options.DryRun) Console.WriteLine("(dry-run mode - no files will be modified)");
    if (options.CreateBackup) Console.WriteLine("(backup mode - .bak files will be created)");
    Console.WriteLine();

    var results = new List<ProcessingResult>();
    var totalRemoved = 0;
    var totalFlagged = 0;
    var lockObj = new object();

    if (options.Parallel)
    {
        await Parallel.ForEachAsync(files, async (file, ct) =>
        {
            if (options.Verbose) Console.WriteLine($"Processing: {file}");

            var result = await processor.ProcessFileAsync(file, options);

            lock (lockObj)
            {
                results.Add(result);
                totalRemoved += result.BlocksRemoved;
                totalFlagged += result.BlocksFlaggedForReview;
                PrintResult(result, options.Verbose);
            }
        });
    }
    else
    {
        foreach (var file in files)
        {
            if (options.Verbose) Console.WriteLine($"Processing: {file}");

            var result = await processor.ProcessFileAsync(file, options);
            results.Add(result);
            totalRemoved += result.BlocksRemoved;
            totalFlagged += result.BlocksFlaggedForReview;
            PrintResult(result, options.Verbose);
        }
    }

    Console.WriteLine();
    Console.WriteLine(new string('=', 55));
    Console.WriteLine($"  Files processed:     {results.Count}");
    Console.WriteLine($"  Blocks cleaned:      {totalRemoved}");
    Console.WriteLine($"  Blocks need review:  {totalFlagged} (#error injected)");
    Console.WriteLine($"  Files failed:        {results.Count(r => r.Status == ResultStatus.Failed)}");
    Console.WriteLine(new string('=', 55));

    if (totalFlagged > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"Warning: {totalFlagged} complex blocks have #error directives injected.");
        Console.WriteLine("  Build will fail until these are manually reviewed and resolved.");
        Console.WriteLine();
        Console.WriteLine("  To find them: grep -rn 'NET8_REVIEW_REQUIRED' --include='*.cs'");
    }

    if (options.ReportPath is not null)
    {
        var report = new
        {
            Timestamp = DateTime.UtcNow,
            TotalFiles = results.Count,
            BlocksCleaned = totalRemoved,
            BlocksFlagged = totalFlagged,
            FilesFailed = results.Count(r => r.Status == ResultStatus.Failed),
            Files = results.Select(r => new
            {
                r.FilePath,
                Status = r.Status.ToString(),
                r.BlocksRemoved,
                r.BlocksFlaggedForReview,
                Issues = r.Issues?.Select(i => new
                {
                    Line = i.Location.GetLineSpan().StartLinePosition.Line + 1,
                    i.Message
                })
            })
        };

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(options.ReportPath, json);
        Console.WriteLine($"\nReport written to: {options.ReportPath}");
    }

    context.ExitCode = results.Any(r => r.Status == ResultStatus.Failed) ? 1
        : (options.FailOnReview && totalFlagged > 0) ? 2
        : 0;

    if (options.FailOnReview && totalFlagged > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Error: --fail-on-review: Exiting with code 2 (manual review required)");
    }
});

return await rootCommand.InvokeAsync(args);

static void PrintResult(ProcessingResult result, bool verbose)
{
    switch (result.Status)
    {
        case ResultStatus.Success:
            Console.WriteLine($"  OK {result.FilePath} ({result.BlocksRemoved} cleaned)");
            break;

        case ResultStatus.SuccessWithReview:
            Console.WriteLine($"  REVIEW {result.FilePath} ({result.BlocksRemoved} cleaned, {result.BlocksFlaggedForReview} need review)");
            if (verbose && result.Issues is not null)
            {
                foreach (var issue in result.Issues)
                    Console.WriteLine($"    -> {issue.Message} at line {issue.Location.GetLineSpan().StartLinePosition.Line + 1}");
            }
            break;

        case ResultStatus.Failed:
            Console.WriteLine($"  FAIL {result.FilePath}");
            if (result.Errors is not null)
            {
                foreach (var error in result.Errors)
                    Console.WriteLine($"    {error}");
            }
            break;

        case ResultStatus.Skipped:
            if (verbose)
                Console.WriteLine($"  SKIP {result.FilePath}");
            break;
    }
}

static IEnumerable<string> GetCSharpFiles(string path, bool includeGenerated)
{
    if (File.Exists(path))
        return [path];

    var sep = Path.DirectorySeparatorChar;
    var files = Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{sep}obj{sep}"))
        .Where(f => !f.Contains($"{sep}bin{sep}"))
        .Where(f => !f.Contains($"{sep}.git{sep}"));

    // Only filter by extension pattern here (path-based detection).
    // Content-based generated file detection is handled in FileProcessor
    // after reading the file content.
    if (!includeGenerated)
    {
        files = files.Where(f => !GeneratedFileDetector.IsGeneratedFile(f, content: null));
    }

    return files;
}
