using ApiMark.DotNet.Fixtures;
using ApiMark.Tool;
using Xunit;

namespace ApiMark.Tool.Tests;

/// <summary>Integration tests for <see cref="Program"/>.</summary>
public class ProgramTests
{
    /// <summary>
    ///     Validates that invoking the <c>dotnet</c> subcommand with a valid fixture
    ///     assembly and XML documentation file exits with code 0 and writes
    ///     <c>api.md</c> to the output directory.
    /// </summary>
    [Fact]
    public void Program_Main_DotNetCommand_GeneratesExpectedOutput()
    {
        // Arrange: locate the fixture assembly and its XML doc using runtime type resolution
        var assemblyPath = typeof(SampleClass).Assembly.Location;
        var xmlDocPath = Path.ChangeExtension(assemblyPath, ".xml");
        var outputDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            // Act
            var exitCode = Program.Main([
                "dotnet",
                "--assembly", assemblyPath,
                "--xml-doc", xmlDocPath,
                "--output", outputDir,
            ]);

            // Assert: tool exits successfully and produces the mandatory api.md entrypoint
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(Path.Combine(outputDir, "api.md")), "Expected api.md in output directory");
        }
        finally
        {
            // Clean up the temporary output directory
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Validates that supplying an unrecognized <c>--visibility</c> value exits
    ///     with a non-zero code.
    /// </summary>
    [Fact]
    public void Program_Main_WithInvalidVisibility_ReturnsNonZeroExitCode()
    {
        // Arrange: supply a visibility value that does not map to any ApiVisibility member
        var assemblyPath = typeof(SampleClass).Assembly.Location;
        var outputDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        // Act
        var exitCode = Program.Main([
            "dotnet",
            "--assembly", assemblyPath,
            "--output", outputDir,
            "--visibility", "InvalidValue",
        ]);

        // Assert: invalid visibility must produce a non-zero exit code
        Assert.NotEqual(0, exitCode);
    }

    /// <summary>
    ///     Validates that supplying a path to a non-existent assembly exits with
    ///     a non-zero code and writes a descriptive error message.
    /// </summary>
    [Fact]
    public void Program_Main_WithMissingAssembly_PrintsErrorAndFails()
    {
        // Arrange: use a path that is guaranteed not to exist
        var outputDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var originalError = Console.Error;
        var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);

            // Act
            var exitCode = Program.Main([
                "dotnet",
                "--assembly", "path/does/not/exist.dll",
                "--output", outputDir,
            ]);

            // Assert: missing assembly must produce a non-zero exit code and a descriptive error message
            Assert.NotEqual(0, exitCode);
            Assert.False(string.IsNullOrWhiteSpace(errorWriter.ToString()), "Expected error message on stderr");
        }
        finally
        {
            // Restore the original error stream regardless of outcome
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Validates that invoking the tool with no arguments exits with a non-zero
    ///     code because no language subcommand was specified.
    /// </summary>
    [Fact]
    public void Program_Main_WithNoArguments_ReturnsNonZeroExitCode()
    {
        // Act: pass an empty argument array
        var exitCode = Program.Main([]);

        // Assert: missing subcommand must produce a non-zero exit code
        Assert.NotEqual(0, exitCode);
    }

    /// <summary>
    ///     Validates that the <c>--version</c> flag prints version information and
    ///     exits with code 0.
    /// </summary>
    [Fact]
    public void Program_Main_WithVersionFlag_PrintsVersionAndExitsZero()
    {
        // Arrange: capture stdout so the test does not pollute the test runner output
        var originalOut = Console.Out;
        var outputWriter = new StringWriter();

        try
        {
            Console.SetOut(outputWriter);

            // Act
            var exitCode = Program.Main(["--version"]);

            // Assert: --version must exit 0 and print something to stdout
            Assert.Equal(0, exitCode);
            Assert.False(string.IsNullOrWhiteSpace(outputWriter.ToString()), "Expected version text on stdout");
        }
        finally
        {
            // Restore the original output stream regardless of outcome
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Validates that invoking the <c>cpp</c> subcommand exits with a non-zero
    ///     code because the cpp language is not yet implemented.
    /// </summary>
    [Fact]
    public void Program_Main_WithCppSubcommand_ReturnsNonZeroExitCode()
    {
        // Arrange: provide --output to pass the output validation before the language check
        var outputDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        // Act
        var exitCode = Program.Main(["cpp", "--output", outputDir]);

        // Assert: cpp is not yet implemented so the exit code must be non-zero
        Assert.NotEqual(0, exitCode);
    }

    /// <summary>
    ///     Validates that <c>--silent</c> and <c>--log</c> are accepted alongside the
    ///     <c>dotnet</c> subcommand, that the tool exits with code 0, and that the log
    ///     file is created and non-empty.
    /// </summary>
    [Fact]
    public void Program_Main_WithSilentAndLog_DotNetCommand_ExitsZero()
    {
        // Arrange
        var assemblyPath = typeof(SampleClass).Assembly.Location;
        var xmlDocPath = Path.ChangeExtension(assemblyPath, ".xml");
        var outputDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var logFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".log");

        try
        {
            // Act
            var exitCode = Program.Main([
                "--silent",
                "--log", logFile,
                "dotnet",
                "--assembly", assemblyPath,
                "--xml-doc", xmlDocPath,
                "--output", outputDir,
            ]);

            // Assert: exits zero; log file exists and is non-empty
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(logFile), "Expected log file to be created");
            Assert.True(new FileInfo(logFile).Length > 0, "Expected log file to be non-empty");
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }

            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Validates that the <c>--help</c> flag prints usage information and exits with code 0.
    /// </summary>
    [Fact]
    public void Program_Main_WithHelpFlag_PrintsHelpAndExitsZero()
    {
        // Arrange: capture stdout to inspect the help output
        var originalOut = Console.Out;
        var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);

            // Act
            var exitCode = Program.Main(["--help"]);

            // Assert: --help must exit 0 and print usage and options sections
            Assert.Equal(0, exitCode);
            var output = writer.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Options:", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Validates that <c>--validate</c> runs self-validation tests and exits with code 0
    ///     when all internal tests pass.
    /// </summary>
    [Fact]
    public void Program_Main_WithValidateFlag_ExitsZero()
    {
        // Arrange: capture stdout to suppress validation output from the test runner
        var originalOut = Console.Out;
        var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);

            // Act
            var exitCode = Program.Main(["--validate"]);

            // Assert: self-validation must complete without errors
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Validates that standard flags such as <c>--help</c> are accepted after the language
    ///     subcommand token (single-pass parser requirement).
    /// </summary>
    [Fact]
    public void Program_Main_WithHelpAfterSubcommand_PrintsHelpAndExitsZero()
    {
        // Arrange: capture stdout to inspect the help output
        var originalOut = Console.Out;
        var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);

            // Act: --help appears after the language token to exercise the single-pass parser
            var exitCode = Program.Main(["dotnet", "--help"]);

            // Assert: --help must still short-circuit to help display regardless of position
            Assert.Equal(0, exitCode);
            Assert.Contains("Usage:", writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
