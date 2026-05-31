using ApiMark.Tool.Cli;
using ApiMark.Tool.SelfTest;
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
        var trxPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".trx");

        try
        {
            using var context = Context.Create(["--validate", "--silent", "--results", trxPath]);

            // Act
            Validation.Run(context);

            // Assert: the .trx file must exist and contain TRX XML content
            Assert.True(File.Exists(trxPath), "TRX results file must be created");
            var content = File.ReadAllText(trxPath);
            Assert.Contains("TestRun", content);
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
        var xmlPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xml");

        try
        {
            using var context = Context.Create(["--validate", "--silent", "--results", xmlPath]);

            // Act
            Validation.Run(context);

            // Assert: the .xml file must exist
            Assert.True(File.Exists(xmlPath), "XML results file must be created");
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
        var jsonPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
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
        var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".log");

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
}
