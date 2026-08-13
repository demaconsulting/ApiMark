using ApiMark.Core;
using ApiMark.Tool.Cli;
using ApiMark.Vhdl;

namespace ApiMark.Tool.SelfTest;

/// <summary>
///     Provides the documentation-coverage enforcement (<c>--enforce-docs</c>) functional
///     self-validation test for ApiMark Tool.
/// </summary>
internal static partial class Validation
{
    /// <summary>
    ///     Runs a functional test that exercises the real <see cref="VhdlGenerator.CheckDocumentationCoverage"/>
    ///     pipeline (<c>Parse</c> then <c>CheckDocumentationCoverage</c>) against a tiny embedded
    ///     sample VHDL entity containing one documented and one deliberately undocumented port,
    ///     and verifies that enforcement correctly detects the undocumented port and leaves the
    ///     documented port unreported.
    /// </summary>
    /// <remarks>
    ///     VHDL is used for this test (rather than .NET or C++) because its sample source is
    ///     entirely self-contained and requires no external tool (unlike C++, which needs clang),
    ///     letting this test run unconditionally alongside <see cref="RunVhdlGenerationTest"/>.
    ///     This test exercises <see cref="IDocumentationCoverageCapable.CheckDocumentationCoverage"/>
    ///     directly (the same API <c>Program.RunToolLogic</c> calls when <c>--enforce-docs</c> is
    ///     supplied on the command line) rather than re-invoking the full CLI argument-parsing
    ///     path, since that path is already covered by <c>ArgumentParserTests</c> and
    ///     <c>ProgramTests</c>. The generator's own informational <c>WriteLine</c> output is
    ///     routed to a silent, log-capturing child <see cref="Context"/> rather than the outer
    ///     validation <paramref name="context"/>, so it never interleaves with the pass/fail
    ///     transcript.
    /// </remarks>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection to append results to.</param>
    private static void RunEnforceDocsTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("ApiMark_EnforceDocs");

        try
        {
            using var tempDir = new TemporaryDirectory();
            var logFile = Path.Join(tempDir.DirectoryPath, "enforce-docs.log");

            const string SampleVhdl =
                "--! @brief Self-test enforcement entity.\n" +
                "ENTITY apimark_enforce_sample IS\n" +
                "    PORT (\n" +
                "        clk : IN STD_LOGIC; --! Rising-edge clock input\n" +
                "        rst : IN STD_LOGIC\n" +
                "    );\n" +
                "END ENTITY apimark_enforce_sample;\n";

            var sourcePath = Path.Join(tempDir.DirectoryPath, "enforce.vhd");
            File.WriteAllText(sourcePath, SampleVhdl);

            var options = new VhdlGeneratorOptions
            {
                LibraryName = "ApiMarkSelfTest",
                Sources = [sourcePath],
            };

            // Route the generator's own informational output to a silent, log-capturing child
            // context so it doesn't interleave with the validation transcript.
            using var genContext = Context.Create(["--silent", "--log", logFile]);

            var generator = new VhdlGenerator(options);
            generator.Parse(genContext);

            var result = generator.CheckDocumentationCoverage("Public");

            var foundUndocumentedRst = result.UndocumentedItems.Any(i => i.DisplayName.Contains("rst"));
            var foundDocumentedClk = result.UndocumentedItems.Any(i => i.DisplayName.Contains("clk"));

            if (result.HasViolations && foundUndocumentedRst && !foundDocumentedClk)
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                context.WriteLine("✓ ApiMark_EnforceDocs - Passed");
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = "Documentation-coverage enforcement did not report expected results";
                context.WriteError(
                    "✗ ApiMark_EnforceDocs - Failed: Documentation-coverage enforcement did not report expected results");
            }
        }
        // Generic catch is justified here as this is a test framework — any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "ApiMark_EnforceDocs", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }
}
