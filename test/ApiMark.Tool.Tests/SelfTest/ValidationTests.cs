using ApiMark.Tool.Cli;
using ApiMark.Tool.SelfTest;
using DemaConsulting.TestResults;
using DemaConsulting.TestResults.IO;
using Xunit;

namespace ApiMark.Tool.Tests.SelfTest;

/// <summary>Unit tests for <see cref="Validation"/>.</summary>
public sealed class ValidationTests
{
    /// <summary>
    ///     Validates that running self-validation with a valid context exits with code 0.
    /// </summary>
    [Fact]
    public void Validation_Run_WithValidContext_ExitsZero()
    {
        // Arrange: create a context with --validate and --silent to suppress console output
        using var context = Context.Create(["--validate", "--silent"]);

        // Act
        Validation.Run(context);

        // Assert: all self-tests must pass and ExitCode must be 0
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Validates that Validation.Run writes a .trx results file when --results specifies a .trx path.
    /// </summary>
    [Fact]
    public void Validation_Run_WithResultsTrxFile_CreatesTrxFile()
    {
        // Arrange: create a temporary .trx path and a context requesting results output
        var trxPath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName() + ".trx");

        try
        {
            using var context = Context.Create(["--validate", "--silent", "--results", trxPath]);

            // Act
            Validation.Run(context);

            // Assert: the .trx file must exist and round-trip both test names and outcomes
            Assert.True(File.Exists(trxPath), "TRX results file must be created");
            var trxContent = File.ReadAllText(trxPath);
            var trxResults = TrxSerializer.Deserialize(trxContent);
            Assert.Equal(5, trxResults.Results.Count);
            Assert.Contains(trxResults.Results, r => r.Name == "ApiMark_VersionDisplay" && r.Outcome == TestOutcome.Passed);
            Assert.Contains(trxResults.Results, r => r.Name == "ApiMark_HelpDisplay" && r.Outcome == TestOutcome.Passed);
            Assert.Contains(trxResults.Results, r => r.Name == "ApiMark_DotNetGeneration" && r.Outcome == TestOutcome.Passed);
            Assert.Contains(trxResults.Results, r => r.Name == "ApiMark_VhdlGeneration" && r.Outcome == TestOutcome.Passed);
            Assert.Contains(
                trxResults.Results,
                r => r.Name == "ApiMark_CppGeneration" &&
                     (r.Outcome == TestOutcome.Passed || r.Outcome == TestOutcome.NotExecuted));
        }
        finally
        {
            // Clean up the temporary results file regardless of test outcome
            if (File.Exists(trxPath))
            {
                File.Delete(trxPath);
            }
        }
    }

    /// <summary>
    ///     Validates that Validation.Run writes an .xml results file when --results specifies a .xml path.
    /// </summary>
    [Fact]
    public void Validation_Run_WithResultsXmlFile_CreatesXmlFile()
    {
        // Arrange: create a temporary .xml path and a context requesting results output
        var xmlPath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName() + ".xml");

        try
        {
            using var context = Context.Create(["--validate", "--silent", "--results", xmlPath]);

            // Act
            Validation.Run(context);

            // Assert: the .xml file must exist and round-trip both test names and outcomes
            Assert.True(File.Exists(xmlPath), "XML results file must be created");
            var xmlContent = File.ReadAllText(xmlPath);
            var xmlResults = JUnitSerializer.Deserialize(xmlContent);
            Assert.Equal(5, xmlResults.Results.Count);
            Assert.Contains(xmlResults.Results, r => r.Name == "ApiMark_VersionDisplay" && r.Outcome == TestOutcome.Passed);
            Assert.Contains(xmlResults.Results, r => r.Name == "ApiMark_HelpDisplay" && r.Outcome == TestOutcome.Passed);
            Assert.Contains(xmlResults.Results, r => r.Name == "ApiMark_DotNetGeneration" && r.Outcome == TestOutcome.Passed);
            Assert.Contains(xmlResults.Results, r => r.Name == "ApiMark_VhdlGeneration" && r.Outcome == TestOutcome.Passed);
            Assert.Contains(
                xmlResults.Results,
                r => r.Name == "ApiMark_CppGeneration" &&
                     (r.Outcome == TestOutcome.Passed || r.Outcome == TestOutcome.NotExecuted));
        }
        finally
        {
            // Clean up the temporary results file regardless of test outcome
            if (File.Exists(xmlPath))
            {
                File.Delete(xmlPath);
            }
        }
    }

    /// <summary>
    ///     Validates that an unsupported results file extension causes ExitCode to be set to 1.
    /// </summary>
    [Fact]
    public void Validation_Run_WithUnsupportedResultsExtension_SetsExitCodeToOne()
    {
        // Arrange: create a context with an unsupported .json extension for results
        var jsonPath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        using var context = Context.Create(["--validate", "--silent", "--results", jsonPath]);

        // Act
        Validation.Run(context);

        // Assert: unsupported extension must cause WriteError and set ExitCode to 1
        Assert.Equal(1, context.ExitCode);
    }

    /// <summary>
    ///     Validates that Validation.Run produces output mentioning both self-test names.
    /// </summary>
    [Fact]
    public void Validation_Run_WritesVersionAndHelpTestResults()
    {
        // Arrange: create a temporary log file to capture output
        var logPath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName() + ".log");

        try
        {
            // Act: run validation inside a block so the context is disposed (flushing the log)
            // before the file is read
            using (var context = Context.Create(["--validate", "--silent", "--log", logPath]))
            {
                Validation.Run(context);
            }

            // Assert: the log must contain both self-test names
            var output = File.ReadAllText(logPath);
            Assert.Multiple(
                () => Assert.Contains("ApiMark_VersionDisplay", output),
                () => Assert.Contains("ApiMark_HelpDisplay", output));
        }
        finally
        {
            // Clean up the temporary log file regardless of test outcome
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }
    }

    /// <summary>
    ///     Validates that passing a null context to Validation.Run throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void Validation_Run_NullContext_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: null context must throw ArgumentNullException
        Assert.Throws<ArgumentNullException>(() => Validation.Run(null!));
    }

    /// <summary>
    ///     Validates that Validation.Run produces output mentioning all three functional
    ///     generation test names.
    /// </summary>
    [Fact]
    public void Validation_Run_WritesFunctionalGenerationTestResults()
    {
        // Arrange: create a temporary log file to capture output
        var logPath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName() + ".log");

        try
        {
            // Act
            using (var context = Context.Create(["--validate", "--silent", "--log", logPath]))
            {
                Validation.Run(context);
            }

            // Assert: the log must mention all three functional generation tests
            var output = File.ReadAllText(logPath);
            Assert.Multiple(
                () => Assert.Contains("ApiMark_DotNetGeneration", output),
                () => Assert.Contains("ApiMark_CppGeneration", output),
                () => Assert.Contains("ApiMark_VhdlGeneration", output));
        }
        finally
        {
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }
    }

    /// <summary>
    ///     Validates that Validation.Run's summary output includes a "Skipped: N" line.
    /// </summary>
    [Fact]
    public void Validation_Run_WritesSkippedSummaryLine()
    {
        // Arrange: create a temporary log file to capture output
        var logPath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName() + ".log");

        try
        {
            // Act
            using (var context = Context.Create(["--validate", "--silent", "--log", logPath]))
            {
                Validation.Run(context);
            }

            // Assert: the summary must include a "Skipped: N" line regardless of clang availability
            var output = File.ReadAllText(logPath);
            Assert.Matches(@"Skipped: \d+", output);
        }
        finally
        {
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }
    }

    /// <summary>
    ///     Validates that the C++ functional test is Skipped (not Failed) and does not affect
    ///     the exit code when clang cannot be located, by forcing <c>APIMARK_CLANG_PATH</c> to
    ///     point at a nonexistent path for the duration of the test.
    /// </summary>
    [Fact]
    public void Validation_Run_CppGenerationSkippedWhenClangUnavailable()
    {
        // Arrange: force clang discovery to fail by overriding the env var with a bogus path;
        // restore the original value in `finally` to avoid poisoning other tests in the run
        const string ClangPathEnvVar = "APIMARK_CLANG_PATH";
        var originalValue = Environment.GetEnvironmentVariable(ClangPathEnvVar);
        var bogusPath = Path.Join(Path.GetTempPath(), $"apimark_no_such_clang_{Guid.NewGuid():N}.exe");
        Environment.SetEnvironmentVariable(ClangPathEnvVar, bogusPath);

        var trxPath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName() + ".trx");
        try
        {
            using var context = Context.Create(["--validate", "--silent", "--results", trxPath]);

            // Act
            Validation.Run(context);

            // Assert: the C++ test must be recorded as NotExecuted (skipped), and the overall
            // exit code must remain 0 since a skip is not a failure
            Assert.Equal(0, context.ExitCode);
            var trxContent = File.ReadAllText(trxPath);
            var trxResults = TrxSerializer.Deserialize(trxContent);
            Assert.Contains(
                trxResults.Results,
                r => r.Name == "ApiMark_CppGeneration" && r.Outcome == TestOutcome.NotExecuted);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ClangPathEnvVar, originalValue);
            if (File.Exists(trxPath))
            {
                File.Delete(trxPath);
            }
        }
    }
}

