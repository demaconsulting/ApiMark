using ApiMark.Core;
using ApiMark.Tool.Cli;
using ApiMark.Vhdl;

namespace ApiMark.Tool.SelfTest;

/// <summary>
///     Provides the VHDL functional self-validation test for ApiMark Tool.
/// </summary>
internal static partial class Validation
{
    /// <summary>
    ///     Runs a functional test that exercises the real <see cref="VhdlGenerator"/> pipeline
    ///     (<c>Parse</c> then <c>Emit</c>) against a tiny embedded sample VHDL entity, and
    ///     verifies the generated Markdown contains expected content.
    /// </summary>
    /// <remarks>
    ///     VHDL parsing is performed entirely in-process via an embedded ANTLR4 grammar, so
    ///     unlike the C++ functional test, no external tool availability check is required.
    /// </remarks>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection to append results to.</param>
    private static void RunVhdlGenerationTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("ApiMark_VhdlGeneration");

        try
        {
            using var tempDir = new TemporaryDirectory();

            const string SampleVhdl =
                "--! @brief Self-test counter entity.\n" +
                "ENTITY apimark_sample_counter IS\n" +
                "    GENERIC (\n" +
                "        WIDTH : INTEGER := 8 --! Width of the counter data bus in bits\n" +
                "    );\n" +
                "    PORT (\n" +
                "        clk : IN STD_LOGIC --! Rising-edge clock input\n" +
                "    );\n" +
                "END ENTITY apimark_sample_counter;\n";

            var sourcePath = Path.Join(tempDir.DirectoryPath, "counter.vhd");
            File.WriteAllText(sourcePath, SampleVhdl);

            var options = new VhdlGeneratorOptions
            {
                LibraryName = "ApiMarkSelfTest",
                Sources = [sourcePath],
            };

            var generator = new VhdlGenerator(options);
            var emitter = generator.Parse(context);

            var outputDir = Path.Join(tempDir.DirectoryPath, "out");
            var factory = new FileMarkdownWriterFactory(outputDir);
            emitter.Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, context);

            var markdown = File.ReadAllText(Path.Join(outputDir, "api.md"));
            if (markdown.Contains("apimark_sample_counter") && markdown.Contains("Width of the counter data bus in bits"))
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                context.WriteLine("✓ ApiMark_VhdlGeneration - Passed");
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = "Expected content not found in generated Markdown";
                context.WriteError("✗ ApiMark_VhdlGeneration - Failed: Expected content not found in generated Markdown");
            }
        }
        // Generic catch is justified here as this is a test framework — any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "ApiMark_VhdlGeneration", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }
}
