using ApiMark.Core;
using ApiMark.DotNet;
using ApiMark.Tool.Cli;

namespace ApiMark.Tool.SelfTest;

/// <summary>
///     Provides the DotNet functional self-validation test for ApiMark Tool.
/// </summary>
internal static partial class Validation
{
    /// <summary>
    ///     Runs a functional test that exercises the real <see cref="DotNetGenerator"/>
    ///     pipeline (<c>Parse</c> then <c>Emit</c>) against ApiMark.Tool's own already-built
    ///     assembly and XML documentation file, and verifies the generated Markdown contains
    ///     expected content.
    /// </summary>
    /// <remarks>
    ///     The currently-executing <see cref="Program"/> assembly is used as the sample source
    ///     rather than compiling a fresh mini-assembly: <c>ApiMark.Tool.csproj</c> already sets
    ///     <c>GenerateDocumentationFile=true</c>, so the sibling <c>.xml</c> doc file is
    ///     guaranteed present at build output alongside the DLL, keeping this test
    ///     dependency-free (no Roslyn compile-at-runtime step required).
    ///     The generator's own informational <c>WriteLine</c> output (e.g. "Parsing assembly:
    ///     ..." / "Found N types...") is routed to a silent, log-capturing child
    ///     <see cref="Context"/> rather than the outer validation <paramref name="context"/>, so
    ///     it never interleaves with the pass/fail transcript — matching the pattern used by
    ///     <see cref="RunVersionTest"/> and <see cref="RunHelpTest"/>.
    /// </remarks>
    /// <param name="context">The context for output.</param>
    /// <param name="testResults">The test results collection to append results to.</param>
    private static void RunDotNetGenerationTest(Context context, DemaConsulting.TestResults.TestResults testResults)
    {
        var startTime = DateTime.UtcNow;
        var test = CreateTestResult("ApiMark_DotNetGeneration");

        try
        {
            using var tempDir = new TemporaryDirectory();
            var logFile = Path.Join(tempDir.DirectoryPath, "dotnet-generation.log");

            var assemblyPath = typeof(Program).Assembly.Location;
            var xmlDocPath = Path.ChangeExtension(assemblyPath, ".xml");

            var options = new DotNetGeneratorOptions
            {
                AssemblyPath = assemblyPath,
                XmlDocPath = xmlDocPath,
                // ApiMark.Tool's own types are internal, so the "All" tier is required here to
                // exercise the generator meaningfully against this self-contained sample source.
                Visibility = ApiVisibility.All,
            };

            // Route the generator's own informational output to a silent, log-capturing child
            // context so it doesn't interleave with the validation transcript.
            using var genContext = Context.Create(["--silent", "--log", logFile]);

            var generator = new DotNetGenerator(options);
            var emitter = generator.Parse(genContext);

            var factory = new FileMarkdownWriterFactory(tempDir.DirectoryPath);
            emitter.Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, genContext);

            var markdown = File.ReadAllText(Path.Join(tempDir.DirectoryPath, "api.md"));
            if (markdown.Contains("Program") && markdown.Contains("ApiMark.Tool"))
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Passed;
                context.WriteLine("✓ ApiMark_DotNetGeneration - Passed");
            }
            else
            {
                test.Outcome = DemaConsulting.TestResults.TestOutcome.Failed;
                test.ErrorMessage = "Expected content not found in generated Markdown";
                context.WriteError("✗ ApiMark_DotNetGeneration - Failed: Expected content not found in generated Markdown");
            }
        }
        // Generic catch is justified here as this is a test framework — any exception should be
        // recorded as a test failure to ensure robust test execution and reporting.
        catch (Exception ex)
        {
            HandleTestException(test, context, "ApiMark_DotNetGeneration", ex);
        }

        FinalizeTestResult(test, startTime, testResults);
    }
}
