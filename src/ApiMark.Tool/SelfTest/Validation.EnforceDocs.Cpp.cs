using ApiMark.Core;
using ApiMark.Cpp;
using ApiMark.Cpp.CppAst;
using ApiMark.Tool.Cli;

namespace ApiMark.Tool.SelfTest;

/// <summary>
///     Provides the C++ documentation-coverage enforcement (<c>--enforce-docs</c>) functional
///     self-validation test for ApiMark Tool.
/// </summary>
internal static partial class Validation
{
    /// <summary>
    ///     Runs a functional test that exercises the real <see cref="CppGenerator.CheckDocumentationCoverage"/>
    ///     pipeline (<c>Parse</c> then <c>CheckDocumentationCoverage</c>) against a tiny embedded
    ///     sample header containing one documented and one deliberately undocumented struct member,
    ///     and verifies that enforcement correctly detects the undocumented member and leaves the
    ///     documented member unreported.
    /// </summary>
    /// <remarks>
    ///     Requires a clang executable to be discoverable using the same resolution logic as
    ///     <see cref="ClangAstParser"/> (checked via <see cref="ClangDiscovery.IsAvailable"/>).
    ///     When clang is unavailable, the test is recorded as <see cref="DemaConsulting.TestResults.TestOutcome.NotExecuted"/>
    ///     (skipped) rather than failed, and a message is written via <c>context.WriteLine</c>
    ///     (never <c>WriteError</c>) so the skip never causes a non-zero exit code. This test
    ///     exercises <see cref="IDocumentationCoverageCapable.CheckDocumentationCoverage"/> directly
    ///     (the same API <c>Program.RunToolLogic</c> calls when <c>--enforce-docs</c> is supplied
    ///     on the command line). The generator's own informational <c>WriteLine</c> output is
    ///     routed to a silent, log-capturing child <see cref="Context"/> rather than the outer
    ///     validation <paramref name="context"/>, so it never interleaves with the pass/fail
    ///     transcript.
    /// </remarks>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection to append results to.</param>
    private static void RunCppEnforceDocsTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("ApiMark_CppEnforceDocs");

        try
        {
            if (!ClangDiscovery.IsAvailable())
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.NotExecuted;
                context.WriteLine(
                    "⊘ ApiMark_CppEnforceDocs - Skipped: clang not found; install LLVM clang or " +
                    "set APIMARK_CLANG_PATH to validate C++ enforcement support");
                FinalizeTestResult(test, startTime, testResults);
                return;
            }

            using var tempDir = new TemporaryDirectory();
            var logFile = Path.Join(tempDir.DirectoryPath, "cpp-enforce-docs.log");

            const string SampleHeader =
                "/// Represents a single point in 2D space.\n" +
                "struct ApiMarkEnforceSamplePoint\n" +
                "{\n" +
                "    /// The horizontal coordinate.\n" +
                "    int x;\n" +
                "    int y;\n" +
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
            generator.Parse(genContext);

            var result = generator.CheckDocumentationCoverage("Public");

            var foundUndocumentedY = result.UndocumentedItems.Any(i => i.DisplayName.Contains("::y"));
            var foundDocumentedX = result.UndocumentedItems.Any(i => i.DisplayName.Contains("::x"));

            if (result.HasViolations && foundUndocumentedY && !foundDocumentedX)
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                context.WriteLine("✓ ApiMark_CppEnforceDocs - Passed");
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = "Documentation-coverage enforcement did not report expected results";
                context.WriteError(
                    "✗ ApiMark_CppEnforceDocs - Failed: Documentation-coverage enforcement did not report expected results");
            }
        }
        // Generic catch is justified here as this is a test framework — any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "ApiMark_CppEnforceDocs", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }
}
