using ApiMark.Core;
using ApiMark.DotNet;
using ApiMark.Tool.Cli;

namespace ApiMark.Tool.SelfTest;

/// <summary>
///     Provides the DotNet documentation-coverage enforcement (<c>--enforce-docs</c>)
///     functional self-validation test for ApiMark Tool.
/// </summary>
internal static partial class Validation
{
    /// <summary>
    ///     Runs a functional test that exercises the real <see cref="DotNetGenerator.CheckDocumentationCoverage"/>
    ///     pipeline (<c>Parse</c> then <c>CheckDocumentationCoverage</c>) against ApiMark.Tool's own
    ///     already-built assembly and XML documentation file, and verifies enforcement runs
    ///     successfully and reports a well-formed result.
    /// </summary>
    /// <remarks>
    ///     Reuses the same self-referencing assembly/XML doc approach as
    ///     <see cref="RunDotNetGenerationTest"/> (see its remarks for rationale) rather than a
    ///     purpose-built undocumented/documented pair, since the exact undocumented-item count
    ///     of the tool's own already-built assembly varies by build configuration and target
    ///     framework (see <c>RunDotNetGenerationTest</c> remarks). This test therefore verifies
    ///     that the enforcement pipeline runs to completion and reports internally consistent
    ///     counts (<c>UndocumentedCount &lt;= CheckedCount</c>), rather than asserting an exact
    ///     violation. This test exercises
    ///     <see cref="IDocumentationCoverageCapable.CheckDocumentationCoverage"/> directly (the
    ///     same API <c>Program.RunToolLogic</c> calls when <c>--enforce-docs</c> is supplied on
    ///     the command line). The generator's own informational <c>WriteLine</c> output is routed
    ///     to a silent, log-capturing child <see cref="Context"/> rather than the outer validation
    ///     <paramref name="context"/>, so it never interleaves with the pass/fail transcript.
    /// </remarks>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection to append results to.</param>
    private static void RunDotNetEnforceDocsTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("ApiMark_DotNetEnforceDocs");

        try
        {
            using var tempDir = new TemporaryDirectory();
            var logFile = Path.Join(tempDir.DirectoryPath, "dotnet-enforce-docs.log");

            var assemblyPath = typeof(Program).Assembly.Location;
            var xmlDocPath = Path.ChangeExtension(assemblyPath, ".xml");

            var options = new DotNetGeneratorOptions
            {
                AssemblyPath = assemblyPath,
                XmlDocPath = xmlDocPath,
                // ApiMark.Tool's own types are internal, so the "All" tier is required here to
                // exercise the checker meaningfully against this self-contained sample source.
                Visibility = ApiVisibility.All,
            };

            // Route the generator's own informational output to a silent, log-capturing child
            // context so it doesn't interleave with the validation transcript.
            using var genContext = Context.Create(["--silent", "--log", logFile]);

            var generator = new DotNetGenerator(options);
            generator.Parse(genContext);

            var result = generator.CheckDocumentationCoverage("All");

            if (result.CheckedCount > 0 && result.UndocumentedCount <= result.CheckedCount)
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                context.WriteLine("✓ ApiMark_DotNetEnforceDocs - Passed");
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = "Documentation-coverage enforcement did not report expected results";
                context.WriteError(
                    "✗ ApiMark_DotNetEnforceDocs - Failed: Documentation-coverage enforcement did not report expected results");
            }
        }
        // Generic catch is justified here as this is a test framework — any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "ApiMark_DotNetEnforceDocs", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }
}
