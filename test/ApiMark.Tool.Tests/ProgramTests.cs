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
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());

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
            Assert.True(
                File.Exists(Path.Join(outputDir, "api.md")),
                "Expected api.md in output directory");
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
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());

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
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

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
        using var outputWriter = new StringWriter();

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
    ///     Validates that invoking the <c>dotnet</c> subcommand without the required
    ///     <c>--xml-doc</c> option exits with a non-zero code and a clear diagnostic
    ///     that names the missing option.
    /// </summary>
    [Fact]
    public void Program_Main_WithDotNetSubcommand_MissingXmlDoc_ReturnsNonZeroExitCode()
    {
        // Arrange: provide --assembly and --output but omit --xml-doc
        var assemblyPath = typeof(SampleClass).Assembly.Location;
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);

            // Act
            var exitCode = Program.Main([
                "dotnet",
                "--assembly", assemblyPath,
                "--output", outputDir,
            ]);

            // Assert: --xml-doc is required for dotnet, so the exit code must be non-zero
            // and the diagnostic message must name the missing option
            Assert.NotEqual(0, exitCode);
            Assert.Contains("--xml-doc", errorWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            // Restore the original error stream regardless of outcome
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Validates that invoking the <c>cpp</c> subcommand without the required
    ///     <c>--includes</c> option exits with a non-zero code and a clear diagnostic
    ///     that names the missing option.
    /// </summary>
    [Fact]
    public void Program_Main_WithCppSubcommand_MissingIncludes_ReturnsNonZeroExitCode()
    {
        // Arrange: provide --output to pass the output validation, but omit --includes
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);

            // Act
            var exitCode = Program.Main(["cpp", "--output", outputDir]);

            // Assert: --includes is required for cpp, so the exit code must be non-zero
            // and the diagnostic message must name the missing option
            Assert.NotEqual(0, exitCode);
            Assert.Contains("--includes", errorWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            // Restore the original error stream regardless of outcome
            Console.SetError(originalError);
        }
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
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var logFile = Path.Join(Path.GetTempPath(), Path.GetRandomFileName() + ".log");

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
        using var writer = new StringWriter();

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
        using var writer = new StringWriter();

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
        using var writer = new StringWriter();

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

    /// <summary>
    ///     Validates that the removed <c>--search-paths</c> flag is no longer recognized and
    ///     produces an "Unsupported argument" diagnostic on stderr.
    /// </summary>
    [Fact]
    public void Program_Main_CppWithSearchPathsFlag_ThrowsArgumentException()
    {
        // Arrange: provide --search-paths which was removed in the redesign
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);

            // Act
            var exitCode = Program.Main(["cpp", "--search-paths", "/sdk", "--output", outputDir]);

            // Assert: must fail with the unsupported-argument diagnostic so the test
            // confirms the specific flag was rejected rather than some other failure
            Assert.NotEqual(0, exitCode);
            Assert.Contains("--search-paths", errorWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Validates that the <c>--api-headers</c> flag is recognized by the parser and
    ///     does not cause an argument exception; when <c>--includes</c> is absent the tool
    ///     still fails with the expected includes-required diagnostic.
    /// </summary>
    [Fact]
    public void Program_Main_CppWithApiHeadersFlag_FlagIsAccepted()
    {
        // Arrange: provide --api-headers but omit --includes so parsing completes but validation fails
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);

            // Act
            var exitCode = Program.Main(["cpp", "--api-headers", "**/*.h", "--output", outputDir]);

            // Assert: fails due to missing --includes (not due to unknown flag),
            // confirming --api-headers was accepted by the parser
            Assert.NotEqual(0, exitCode);
            Assert.Contains("--includes", errorWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Validates that the removed <c>--include-patterns</c> flag is no longer recognized
    ///     and produces an "Unsupported argument" diagnostic on stderr.
    /// </summary>
    [Fact]
    public void Program_Main_CppWithIncludePatternsFlag_ThrowsArgumentException()
    {
        // Arrange: provide --include-patterns which was removed in the redesign
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);

            // Act
            var exitCode = Program.Main([
                "cpp",
                "--include-patterns", "*.h",
                "--output", outputDir,
            ]);

            // Assert: must fail with the unsupported-argument diagnostic so the test
            // confirms the specific flag was rejected rather than some other failure
            Assert.NotEqual(0, exitCode);
            Assert.Contains("--include-patterns", errorWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}
