using ApiMark.Core;
using ApiMark.Cpp;
using ApiMark.Cpp.CppAst;
using ApiMark.Tool.Cli;

namespace ApiMark.Tool.SelfTest;

/// <summary>
///     Provides the C++ functional self-validation test for ApiMark Tool.
/// </summary>
internal static partial class Validation
{
    /// <summary>
    ///     Runs a functional test that exercises the real <see cref="CppGenerator"/> pipeline
    ///     (<c>Parse</c> then <c>Emit</c>) against a tiny embedded sample header, and verifies
    ///     the generated Markdown contains expected content.
    /// </summary>
    /// <remarks>
    ///     Requires a clang executable to be discoverable using the same resolution logic as
    ///     <see cref="ClangAstParser"/> (checked via <see cref="ClangDiscovery.IsAvailable"/>).
    ///     When clang is unavailable, the test is recorded as <see cref="DemaConsulting.TestResults.TestOutcome.NotExecuted"/>
    ///     (skipped) rather than failed, and a message is written via <c>context.WriteLine</c>
    ///     (never <c>WriteError</c>) so the skip never causes a non-zero exit code. The
    ///     generator's own informational <c>WriteLine</c> output is routed to a silent,
    ///     log-capturing child <see cref="Context"/> rather than the outer validation
    ///     <paramref name="context"/>, so it never interleaves with the pass/fail transcript.
    /// </remarks>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection to append results to.</param>
    private static void RunCppGenerationTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("ApiMark_CppGeneration");

        try
        {
            if (!ClangDiscovery.IsAvailable())
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.NotExecuted;
                context.WriteLine(
                    "⊘ ApiMark_CppGeneration - Skipped: clang not found; install LLVM clang or " +
                    "set APIMARK_CLANG_PATH to validate C++ support");
                FinalizeTestResult(test, startTime, testResults);
                return;
            }

            using var tempDir = new TemporaryDirectory();
            var logFile = Path.Join(tempDir.DirectoryPath, "cpp-generation.log");

            const string SampleHeader =
                "/// Represents a single point in 2D space.\n" +
                "struct ApiMarkSamplePoint\n" +
                "{\n" +
                "    /// The horizontal coordinate.\n" +
                "    int x;\n" +
                "};\n";

            var headerPath = Path.Join(tempDir.DirectoryPath, "point.h");
            File.WriteAllText(headerPath, SampleHeader);

            var options = new CppGeneratorOptions
            {
                LibraryName = "ApiMarkSelfTest",
                PublicIncludeRoots = [tempDir.DirectoryPath],
            };

            // Route the generator's own informational output to a silent, log-capturing child
            // context so it doesn't interleave with the validation transcript.
            using var genContext = Context.Create(["--silent", "--log", logFile]);

            var generator = new CppGenerator(options);
            var emitter = generator.Parse(genContext);

            var outputDir = Path.Join(tempDir.DirectoryPath, "out");
            var factory = new FileMarkdownWriterFactory(outputDir);
            emitter.Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, genContext);

            var markdown = File.ReadAllText(Path.Join(outputDir, "api.md"));
            if (markdown.Contains("ApiMarkSamplePoint") && markdown.Contains("Represents a single point in 2D space"))
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                context.WriteLine("✓ ApiMark_CppGeneration - Passed");
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = "Expected content not found in generated Markdown";
                context.WriteError("✗ ApiMark_CppGeneration - Failed: Expected content not found in generated Markdown");
            }
        }
        // Generic catch is justified here as this is a test framework — any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "ApiMark_CppGeneration", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }
}
