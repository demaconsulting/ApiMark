using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ApiMark.Tool.Cli;
using DemaConsulting.TestResults.IO;

namespace ApiMark.Tool.SelfTest;

/// <summary>
///     Provides self-validation functionality for ApiMark Tool.
/// </summary>
/// <remarks>
///     Besides the version and help tests, every self-test runs the tool end to end in-process
///     (through <see cref="Program.Run"/> with a child <see cref="Context"/>) against sample sources
///     it writes into its own temporary directory, and passes only when the exit code, the
///     reported findings, and the generated Markdown are exactly as expected. A passing run
///     therefore shows that the installed tool generates and checks API documentation correctly,
///     not only that it starts.
/// </remarks>
internal static partial class Validation
{
    /// <summary>Regular expression that matches a Markdown link to a relative <c>.md</c> page.</summary>
    [GeneratedRegex(@"\]\((?<target>[^)#:]+\.md)\)")]
    private static partial Regex MarkdownLinkPattern();

    /// <summary>
    ///     Runs self-validation tests and optionally writes results to a file.
    /// </summary>
    /// <param name="context">The context containing command-line arguments and program state.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <remarks>
    ///     If any self-test fails, <c>context.WriteError</c> is called for each failure, which sets
    ///     <c>context.ExitCode</c> to 1 as a side-effect. If a results file is requested and its
    ///     extension is unsupported, <c>context.WriteError</c> is also called, resulting in a
    ///     non-zero exit code.
    /// </remarks>
    public static void Run(Context context)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(context);

        // Print validation header with system information
        PrintValidationHeader(context);

        // Create test results collection
        var testResults = new DemaConsulting.TestResults.TestResults
        {
            Name = "ApiMark Tool Self-Validation"
        };

        // Run core functionality tests
        RunTest(context, testResults, "ApiMark_VersionDisplay", VersionDisplayTest);
        RunTest(context, testResults, "ApiMark_HelpDisplay", HelpDisplayTest);

        // Run functional generator tests
        RunTest(context, testResults, "ApiMark_DotNetGeneration", DotNetGenerationTest);
        RunTest(context, testResults, "ApiMark_GradualDisclosureOutput", GradualDisclosureOutputTest);
        RunTest(context, testResults, "ApiMark_CppGeneration", CppGenerationTest, CppSkipReason);
        RunTest(context, testResults, "ApiMark_VhdlGeneration", VhdlGenerationTest);

        // Run documentation-coverage enforcement tests
        RunTest(context, testResults, "ApiMark_DotNetEnforceDocs", DotNetEnforceDocsTest);
        RunTest(context, testResults, "ApiMark_CppEnforceDocs", CppEnforceDocsTest, CppSkipReason);
        RunTest(context, testResults, "ApiMark_VhdlEnforceDocs", VhdlEnforceDocsTest);

        // Calculate totals
        var totalTests = testResults.Results.Count;
        var passedTests = testResults.Results.Count(t => t.Outcome == DemaConsulting.TestResults.TestOutcome.Passed);
        var failedTests = testResults.Results.Count(t => t.Outcome == DemaConsulting.TestResults.TestOutcome.Failed);
        // DemaConsulting.TestResults.TestOutcome has no dedicated "Skipped" value; NotExecuted is
        // used to represent tests that were intentionally skipped (e.g. clang unavailable) rather
        // than failed. NotExecuted is never counted toward failedTests or the exit code.
        var skippedTests = testResults.Results.Count(t => t.Outcome == DemaConsulting.TestResults.TestOutcome.NotExecuted);

        // Print summary
        context.WriteLine("");
        context.WriteLine($"Total Tests: {totalTests}");
        context.WriteLine($"Passed: {passedTests}");
        context.WriteLine($"Skipped: {skippedTests}");
        if (failedTests > 0)
        {
            context.WriteError($"Failed: {failedTests}");
        }
        else
        {
            context.WriteLine($"Failed: {failedTests}");
        }

        // Write results file if requested
        if (context.ResultsFile != null)
        {
            WriteResultsFile(context, testResults);
        }
    }

    /// <summary>
    ///     Prints the validation header with system information.
    /// </summary>
    /// <param name="context">The context for output.</param>
    private static void PrintValidationHeader(Context context)
    {
        var heading = new string('#', context.HeadingDepth);
        context.WriteLine($"{heading} ApiMark Tool");
        context.WriteLine("");
        context.WriteLine("| Information         | Value                                              |");
        context.WriteLine("| :------------------ | :------------------------------------------------- |");
        context.WriteLine($"| Tool Version        | {Program.Version,-50} |");
        context.WriteLine($"| Machine Name        | {Environment.MachineName,-50} |");
        context.WriteLine($"| OS Version          | {RuntimeInformation.OSDescription,-50} |");
        context.WriteLine($"| DotNet Runtime      | {RuntimeInformation.FrameworkDescription,-50} |");
        context.WriteLine($"| Time Stamp          | {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC{"",-29} |");
        context.WriteLine("");
    }

    /// <summary>
    ///     Runs one self-test, prints its outcome, and records it in the test results.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection to append the result to.</param>
    /// <param name="testName">The name of the test.</param>
    /// <param name="test">The test body; returns <see langword="null"/> on success or a failure message.</param>
    /// <param name="skipReason">
    ///     Optional pre-check; returns <see langword="null"/> when the test can run or the reason it is
    ///     skipped. A skipped test is recorded as <c>NotExecuted</c> and reported with
    ///     <c>context.WriteLine</c>, so it never affects the exit code.
    /// </param>
    private static void RunTest(
        Context context,
        DemaConsulting.TestResults.TestResults testResults,
        string testName,
        Func<string?> test,
        Func<string?>? skipReason = null)
    {
        var startTime = DateTime.UtcNow;
        var result = CreateTestResult(testName);

        try
        {
            var skip = skipReason?.Invoke();
            var failure = skip == null ? test() : null;
            if (skip != null)
            {
                result.Outcome = DemaConsulting.TestResults.TestOutcome.NotExecuted;
                context.WriteLine($"⊘ {testName} - Skipped: {skip}");
            }
            else if (failure == null)
            {
                result.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                context.WriteLine($"✓ {testName} - Passed");
            }
            else
            {
                result.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                result.ErrorMessage = failure;
                context.WriteError($"✗ {testName} - Failed: {failure}");
            }
        }
        // Generic catch is justified here as this is a test framework — any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            result.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
            result.ErrorMessage = $"Exception: {ex.Message}";
            context.WriteError($"✗ {testName} - Failed: {ex.Message}");
        }

        result.Duration = DateTime.UtcNow - startTime;
        testResults.Results.Add(result);
    }

    /// <summary>
    ///     Checks that <c>--version</c> prints exactly the tool version and exits with code 0.
    /// </summary>
    /// <returns><see langword="null"/> on success; otherwise the failure message.</returns>
    private static string? VersionDisplayTest()
    {
        using var workspace = new TemporaryDirectory();
        var run = RunApiMark(workspace, "--version");
        return CheckExitCode(run, 0)
               ?? (run.Log.Trim() == Program.Version ? null : $"Expected version '{Program.Version}' but got '{run.Log.Trim()}'");
    }

    /// <summary>
    ///     Checks that <c>--help</c> prints the usage, the common options, and the options of every
    ///     language subcommand, and exits with code 0.
    /// </summary>
    /// <returns><see langword="null"/> on success; otherwise the failure message.</returns>
    private static string? HelpDisplayTest()
    {
        using var workspace = new TemporaryDirectory();
        var run = RunApiMark(workspace, "--help");
        return CheckExitCode(run, 0)
               ?? CheckContains(
                   "help output",
                   run.Log,
                   "Usage: apimark [options] [language [language-options]]",
                   "Options:",
                   "--validate",
                   "dotnet options:",
                   "cpp options:",
                   "vhdl options:");
    }

    /// <summary>
    ///     Runs the tool in-process with the given arguments, capturing its output in a log file in
    ///     <paramref name="workspace"/>.
    /// </summary>
    /// <param name="workspace">The temporary directory of the calling test.</param>
    /// <param name="args">The command-line arguments; never includes <c>--validate</c>, to avoid recursion.</param>
    /// <returns>The exit code and the captured output of the run.</returns>
    private static ApiMarkRun RunApiMark(TemporaryDirectory workspace, params string[] args)
    {
        var logFile = Path.Join(workspace.DirectoryPath, $"run-{Guid.NewGuid():N}.log");

        // Dispose the run context before reading the log so the log file is flushed and closed
        // (an open log file cannot be read or deleted on Windows)
        int exitCode;
        using (var runContext = Context.Create(["--silent", "--log", logFile, .. args]))
        {
            Program.Run(runContext);
            exitCode = runContext.ExitCode;
        }

        var log = File.ReadAllText(logFile);
        File.Delete(logFile);
        return new ApiMarkRun(exitCode, log);
    }

    /// <summary>
    ///     Compares the exit code of a run with the expected one.
    /// </summary>
    /// <param name="run">The run to check.</param>
    /// <param name="expected">The expected exit code.</param>
    /// <returns><see langword="null"/> when they match; otherwise the failure message.</returns>
    private static string? CheckExitCode(ApiMarkRun run, int expected) =>
        run.ExitCode == expected
            ? null
            : $"Expected exit code {expected} but got {run.ExitCode}: {run.Log.Trim()}";

    /// <summary>
    ///     Checks that a text contains every expected fragment.
    /// </summary>
    /// <param name="what">A description of the text, used in the failure message.</param>
    /// <param name="text">The text to check.</param>
    /// <param name="expected">The fragments the text must contain.</param>
    /// <returns><see langword="null"/> when all are present; otherwise a message naming the first missing one.</returns>
    private static string? CheckContains(string what, string text, params string[] expected)
    {
        var normalized = text.ReplaceLineEndings("\n");
        var missing = expected.FirstOrDefault(e => !normalized.Contains(e, StringComparison.Ordinal));
        return missing == null ? null : $"Expected {what} to contain '{missing}'";
    }

    /// <summary>
    ///     Checks that a text contains none of the given fragments.
    /// </summary>
    /// <param name="what">A description of the text, used in the failure message.</param>
    /// <param name="text">The text to check.</param>
    /// <param name="unexpected">The fragments the text must not contain.</param>
    /// <returns><see langword="null"/> when none is present; otherwise a message naming the first one found.</returns>
    private static string? CheckDoesNotContain(string what, string text, params string[] unexpected)
    {
        var normalized = text.ReplaceLineEndings("\n");
        var found = unexpected.FirstOrDefault(u => normalized.Contains(u, StringComparison.Ordinal));
        return found == null ? null : $"Expected {what} not to contain '{found}'";
    }

    /// <summary>
    ///     Checks that a directory contains exactly the expected files.
    /// </summary>
    /// <param name="directory">The directory to check.</param>
    /// <param name="expected">The expected file paths, relative to <paramref name="directory"/>, using <c>/</c>.</param>
    /// <returns><see langword="null"/> when the file set matches; otherwise the failure message.</returns>
    private static string? CheckFiles(string directory, params string[] expected)
    {
        var actual = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(directory, f).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];
        var sorted = expected.Order(StringComparer.Ordinal).ToList();
        return actual.SequenceEqual(sorted, StringComparer.Ordinal)
            ? null
            : $"Expected files [{string.Join(", ", sorted)}] but got [{string.Join(", ", actual)}]";
    }

    /// <summary>
    ///     Checks that every relative Markdown page link in every page under a directory resolves to an
    ///     existing page.
    /// </summary>
    /// <param name="directory">The output directory to check.</param>
    /// <returns><see langword="null"/> when every link resolves; otherwise a message naming the first broken link.</returns>
    private static string? CheckLinksResolve(string directory)
    {
        foreach (var page in Directory.GetFiles(directory, "*.md", SearchOption.AllDirectories))
        {
            var broken = MarkdownLinkPattern().Matches(File.ReadAllText(page))
                .Select(match => match.Groups["target"].Value)
                .FirstOrDefault(target => !File.Exists(Path.GetFullPath(Path.Join(Path.GetDirectoryName(page), target))));
            if (broken != null)
            {
                return $"Broken link '{broken}' in {Path.GetRelativePath(directory, page)}";
            }
        }

        return null;
    }

    /// <summary>
    ///     Writes test results to a file in TRX or JUnit format.
    /// </summary>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results to write.</param>
    private static void WriteResultsFile(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        if (context.ResultsFile == null)
        {
            return;
        }

        try
        {
            var extension = Path.GetExtension(context.ResultsFile).ToLowerInvariant();
            var content = extension switch
            {
                ".trx" => TrxSerializer.Serialize(testResults),
                ".xml" => JUnitSerializer.Serialize(testResults),
                _ => null,
            };

            if (content is null)
            {
                context.WriteError($"Error: Unsupported results file format '{extension}'. Use .trx or .xml extension.");
                return;
            }

            File.WriteAllText(context.ResultsFile, content);
            context.WriteLine($"Results written to {context.ResultsFile}");
        }
        // Generic catch is justified here as a top-level handler to log file write errors
        catch (Exception ex)
        {
            context.WriteError($"Error: Failed to write results file: {ex.Message}");
        }
    }

    /// <summary>
    ///     Creates a new test result object with common properties.
    /// </summary>
    /// <param name="testName">The name of the test.</param>
    /// <returns>A new test result object.</returns>
    private static DemaConsulting.TestResults.TestResult CreateTestResult(string testName)
    {
        return new DemaConsulting.TestResults.TestResult
        {
            Name = testName,
            ClassName = "Validation",
            CodeBase = "ApiMark.Tool"
        };
    }

    /// <summary>
    ///     The outcome of one in-process run of the tool.
    /// </summary>
    /// <param name="ExitCode">The exit code of the run.</param>
    /// <param name="Log">Everything the run wrote to its output.</param>
    private sealed record ApiMarkRun(int ExitCode, string Log);

    /// <summary>
    ///     Represents a temporary directory that is automatically deleted when disposed.
    /// </summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        /// <summary>
        ///     Gets the path to the temporary directory.
        /// </summary>
        public string DirectoryPath { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TemporaryDirectory"/> class.
        /// </summary>
        public TemporaryDirectory()
        {
            DirectoryPath = Path.Join(
                Path.GetTempPath(),
                $"apimark_validation_{Guid.NewGuid()}");

            try
            {
                Directory.CreateDirectory(DirectoryPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new InvalidOperationException($"Failed to create temporary directory: {ex.Message}", ex);
            }
        }

        /// <summary>
        ///     Gets the full path of a file or directory inside the temporary directory.
        /// </summary>
        /// <param name="relativePath">The path relative to the temporary directory.</param>
        /// <returns>The full path.</returns>
        public string PathOf(string relativePath) => Path.Join(DirectoryPath, relativePath);

        /// <summary>
        ///     Creates a subdirectory inside the temporary directory.
        /// </summary>
        /// <param name="relativePath">The path of the subdirectory relative to the temporary directory.</param>
        /// <returns>The full path of the created subdirectory.</returns>
        public string CreateSubdirectory(string relativePath) =>
            Directory.CreateDirectory(PathOf(relativePath)).FullName;

        /// <summary>
        ///     Deletes the temporary directory and all its contents.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ignore cleanup errors during disposal — best-effort only
                _ = ex;
            }
        }
    }
}
