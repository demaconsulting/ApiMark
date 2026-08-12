using ApiMark.DotNet.Fixtures;
using ApiMark.Tool;
using Xunit;

namespace ApiMark.Tool.Tests;

/// <summary>Integration tests for <see cref="Program"/>.</summary>
[Collection("Console")]
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
    ///     Validates that invoking the <c>dotnet</c> subcommand with a repeatable
    ///     <c>--exclude</c> flag exits with code 0 and omits the matching type's page
    ///     from the generated output.
    /// </summary>
    [Fact]
    public void Program_Main_DotNetWithExcludeFlag_ExcludesMatchingTypeFromOutput()
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
                "--exclude", "ApiMark.DotNet.Fixtures.SampleClass",
            ]);

            // Assert: tool exits successfully, still produces the entrypoint, but omits
            // the page for the excluded type
            Assert.Equal(0, exitCode);
            Assert.True(
                File.Exists(Path.Join(outputDir, "api.md")),
                "Expected api.md in output directory");
            Assert.False(
                File.Exists(Path.Join(outputDir, "ApiMark.DotNet.Fixtures", "SampleClass.md")),
                "Expected SampleClass.md to be omitted when excluded via --exclude");
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
    ///     with a non-zero code and writes an error message containing the invalid value.
    /// </summary>
    [Fact]
    public void Program_Main_WithInvalidVisibility_ReturnsNonZeroExitCode()
    {
        // Arrange: supply a visibility value that does not map to any ApiVisibility member;
        // --xml-doc must also be present so the tool reaches the visibility validation step
        var assemblyPath = typeof(SampleClass).Assembly.Location;
        var xmlDocPath = Path.ChangeExtension(assemblyPath, ".xml");
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
                "--xml-doc", xmlDocPath,
                "--output", outputDir,
                "--visibility", "InvalidValue",
            ]);

            // Assert: invalid visibility must produce a non-zero exit code and a diagnostic
            // that names the offending value so the user can identify and correct the error
            Assert.NotEqual(0, exitCode);
            Assert.Contains("InvalidValue", errorWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            // Restore the original error stream regardless of outcome
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Validates that supplying a path to a non-existent assembly exits with
    ///     a non-zero code and writes a descriptive error message containing the path.
    /// </summary>
    [Fact]
    public void Program_Main_WithMissingAssembly_PrintsErrorAndFails()
    {
        // Arrange: use paths that are guaranteed not to exist; --xml-doc must also be present
        // so that validation reaches the generator stage where the missing assembly is detected
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
                "--xml-doc", "path/does/not/exist.xml",
                "--output", outputDir,
            ]);

            // Assert: missing assembly must produce a non-zero exit code and a descriptive error message
            // that identifies the assembly-not-found condition so the user can diagnose the problem
            Assert.NotEqual(0, exitCode);
            Assert.False(string.IsNullOrWhiteSpace(errorWriter.ToString()), "Expected error message on stderr");
            Assert.Contains("Assembly file not found", errorWriter.ToString(), StringComparison.Ordinal);
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
    ///     <c>dotnet</c> subcommand, that the tool exits with code 0, that the log
    ///     file is created and non-empty, and that no output is written to stdout or
    ///     stderr when <c>--silent</c> is active.
    /// </summary>
    [Fact]
    public void Program_Main_WithSilentAndLog_DotNetCommand_ExitsZero()
    {
        // Arrange
        var assemblyPath = typeof(SampleClass).Assembly.Location;
        var xmlDocPath = Path.ChangeExtension(assemblyPath, ".xml");
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var logFile = Path.Join(Path.GetTempPath(), Path.GetRandomFileName() + ".log");
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errWriter);

            // Act
            var exitCode = Program.Main([
                "--silent",
                "--log", logFile,
                "dotnet",
                "--assembly", assemblyPath,
                "--xml-doc", xmlDocPath,
                "--output", outputDir,
            ]);

            // Assert: exits zero; console is fully silent; log file exists and is non-empty
            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, outWriter.ToString());
            Assert.Equal(string.Empty, errWriter.ToString());
            Assert.True(File.Exists(logFile), "Expected log file to be created");
            Assert.True(new FileInfo(logFile).Length > 0, "Expected log file to be non-empty");
        }
        finally
        {
            // Restore console streams and clean up temporary paths regardless of outcome
            Console.SetOut(originalOut);
            Console.SetError(originalError);

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
    ///     Validates that the vhdl subcommand returns a non-zero exit code when no
    ///     --source pattern is provided.
    /// </summary>
    [Fact]
    public void Program_Main_WithVhdlSubcommand_MissingSourceFiles_ReturnsNonZeroExitCode()
    {
        // Arrange: provide --output but omit --source
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var originalError = Console.Error;
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetError(errorWriter);

            // Act
            var exitCode = Program.Main(["vhdl", "--output", outputDir]);

            // Assert: at least one non-exclusion source pattern is required
            Assert.NotEqual(0, exitCode);
            Assert.Contains("--source", errorWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Validates that <c>--format single-file --depth 4</c> exits with a non-zero code
    ///     and writes a diagnostic naming both flags, because member headings in single-file
    ///     output are at <c>depth+3</c> and a depth of 4 would produce H7, which exceeds H6.
    /// </summary>
    [Fact]
    public void Program_Main_WithSingleFileFormatAndDepth4_ReturnsNonZeroExitCode()
    {
        // Arrange: use the fixture assembly so the tool reaches the cross-argument validation
        // step; the error must be caught before the generator is constructed
        var assemblyPath = typeof(SampleClass).Assembly.Location;
        var xmlDocPath = Path.ChangeExtension(assemblyPath, ".xml");
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
                "--xml-doc", xmlDocPath,
                "--output", outputDir,
                "--format", "single-file",
                "--depth", "4",
            ]);

            // Assert: the cross-argument constraint must produce a non-zero exit and a
            // diagnostic that names --depth so the user can identify and correct the error
            Assert.NotEqual(0, exitCode);
            Assert.Contains("--depth", errorWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            // Restore the original error stream regardless of outcome
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Validates that <c>--format gradual --depth 4</c> exits with code 0 because the
    ///     single-file depth constraint does not apply to the gradual-disclosure format.
    /// </summary>
    [Fact]
    public void Program_Main_WithGradualFormatAndDepth4_ExitsZero()
    {
        // Arrange: use the fixture assembly with gradual format and depth 4 — this combination
        // is valid because gradual-disclosure emitters do not nest headings beyond depth+1
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
                "--format", "gradual",
                "--depth", "4",
            ]);

            // Assert: gradual format with depth 4 is valid and must exit zero
            Assert.Equal(0, exitCode);
        }
        finally
        {
            // Clean up the temporary output directory regardless of outcome
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Builds <c>--exclude</c> arguments that remove every top-level fixture type except
    ///     <paramref name="keepFullName"/>, scoping a documentation-coverage scan down to a
    ///     single, deterministic type without modifying the shared fixtures project.
    /// </summary>
    /// <param name="keepFullName">The fully-qualified fixture type name to keep visible.</param>
    /// <returns>A flat list of alternating <c>--exclude</c>/pattern argument pairs.</returns>
    private static string[] ExcludeAllFixtureTypesExcept(string keepFullName) =>
        typeof(SampleClass).Assembly.GetTypes()
            .Where(t => !t.IsNested)
            .Select(t => t.FullName!.Replace('+', '.'))
            .Where(fullName => fullName != keepFullName)
            .SelectMany(fullName => new[] { "--exclude", fullName })
            .ToArray();

    /// <summary>
    ///     Validates that <c>--enforce-docs</c> with the default <c>Warning</c> severity reports
    ///     undocumented items to standard output but still exits with code 0.
    /// </summary>
    [Fact]
    public void Program_Main_EnforceDocsWarningSeverity_ReportsViolationsButExitsZero()
    {
        // Arrange: scope the scan to SampleClass only, whose Refresh method and implicit
        // constructor are intentionally undocumented in the real fixture XML doc
        var assemblyPath = typeof(SampleClass).Assembly.Location;
        var xmlDocPath = Path.ChangeExtension(assemblyPath, ".xml");
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var originalOut = Console.Out;
        using var outWriter = new StringWriter();

        try
        {
            Console.SetOut(outWriter);

            // Act
            var exitCode = Program.Main([
                "dotnet",
                "--assembly", assemblyPath,
                "--xml-doc", xmlDocPath,
                "--output", outputDir,
                "--enforce-docs", "Public",
                .. ExcludeAllFixtureTypesExcept("ApiMark.DotNet.Fixtures.SampleClass"),
            ]);

            // Assert: default severity (Warning) reports but does not fail the build
            Assert.Equal(0, exitCode);
            Assert.Contains("[Undocumented]", outWriter.ToString(), StringComparison.Ordinal);
            Assert.Contains("Refresh", outWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Validates that <c>--enforce-docs-severity Error</c> causes a non-zero exit code and an
    ///     error message when undocumented items are found.
    /// </summary>
    [Fact]
    public void Program_Main_EnforceDocsErrorSeverity_ReturnsNonZeroExitCode()
    {
        // Arrange: same scope as the warning-severity test, but with Error severity
        var assemblyPath = typeof(SampleClass).Assembly.Location;
        var xmlDocPath = Path.ChangeExtension(assemblyPath, ".xml");
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var outWriter = new StringWriter();
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

            // Act
            var exitCode = Program.Main([
                "dotnet",
                "--assembly", assemblyPath,
                "--xml-doc", xmlDocPath,
                "--output", outputDir,
                "--enforce-docs", "Public",
                "--enforce-docs-severity", "Error",
                .. ExcludeAllFixtureTypesExcept("ApiMark.DotNet.Fixtures.SampleClass"),
            ]);

            // Assert: Error severity must fail the build when violations are found
            Assert.NotEqual(0, exitCode);
            Assert.Contains("undocumented", errorWriter.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Validates that <c>--enforce-docs</c> exits with code 0 and reports zero undocumented
    ///     items when the scoped type is fully documented.
    /// </summary>
    [Fact]
    public void Program_Main_EnforceDocsNoViolations_ExitsZeroAndReportsZeroUndocumented()
    {
        // Arrange: scope the scan to OuterClass (and its nested Inner type), both of which
        // declare fully documented, explicit constructors
        var assemblyPath = typeof(SampleClass).Assembly.Location;
        var xmlDocPath = Path.ChangeExtension(assemblyPath, ".xml");
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var originalOut = Console.Out;
        using var outWriter = new StringWriter();

        try
        {
            Console.SetOut(outWriter);

            // Act
            var exitCode = Program.Main([
                "dotnet",
                "--assembly", assemblyPath,
                "--xml-doc", xmlDocPath,
                "--output", outputDir,
                "--enforce-docs", "Public",
                "--enforce-docs-severity", "Error",
                .. ExcludeAllFixtureTypesExcept("ApiMark.DotNet.Fixtures.OuterClass"),
            ]);

            // Assert: fully-documented scope must exit zero even with Error severity configured
            Assert.Equal(0, exitCode);
            Assert.Contains("Documentation coverage: 0 undocumented", outWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Validates that supplying an unrecognized <c>--enforce-docs</c> value exits with a
    ///     non-zero code and writes an error message containing the invalid value.
    /// </summary>
    [Fact]
    public void Program_Main_WithInvalidEnforceDocsValue_ReturnsNonZeroExitCode()
    {
        // Arrange
        var assemblyPath = typeof(SampleClass).Assembly.Location;
        var xmlDocPath = Path.ChangeExtension(assemblyPath, ".xml");
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
                "--xml-doc", xmlDocPath,
                "--output", outputDir,
                "--enforce-docs", "InvalidTier",
            ]);

            // Assert
            Assert.NotEqual(0, exitCode);
            Assert.Contains("InvalidTier", errorWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Validates that supplying an unrecognized <c>--enforce-docs-severity</c> value exits
    ///     with a non-zero code and writes an error message containing the invalid value.
    /// </summary>
    [Fact]
    public void Program_Main_WithInvalidEnforceDocsSeverityValue_ReturnsNonZeroExitCode()
    {
        // Arrange
        var assemblyPath = typeof(SampleClass).Assembly.Location;
        var xmlDocPath = Path.ChangeExtension(assemblyPath, ".xml");
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
                "--xml-doc", xmlDocPath,
                "--output", outputDir,
                "--enforce-docs", "Public",
                "--enforce-docs-severity", "InvalidSeverity",
            ]);

            // Assert
            Assert.NotEqual(0, exitCode);
            Assert.Contains("InvalidSeverity", errorWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Validates that <c>--enforce-docs</c> is a graceful no-op for the <c>cpp</c> subcommand,
    ///     printing an informational note rather than failing an otherwise-valid build.
    /// </summary>
    [Fact]
    public void Program_Main_EnforceDocsWithCppSubcommand_PrintsInformationalNote()
    {
        // Arrange: --enforce-docs is dotnet-only; cpp must not fail because of it
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var originalOut = Console.Out;
        using var outWriter = new StringWriter();

        try
        {
            Console.SetOut(outWriter);

            // Act
            _ = Program.Main([
                "cpp",
                "--includes", ".",
                "--output", outputDir,
                "--enforce-docs", "Public",
            ]);

            // Assert: the informational note must be printed regardless of the cpp scan's outcome
            Assert.Contains("--enforce-docs is only supported for the dotnet subcommand", outWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Validates that <c>--enforce-docs</c> is a graceful no-op for the <c>vhdl</c> subcommand,
    ///     printing an informational note rather than failing an otherwise-valid build.
    /// </summary>
    [Fact]
    public void Program_Main_EnforceDocsWithVhdlSubcommand_PrintsInformationalNote()
    {
        // Arrange: --enforce-docs is dotnet-only; vhdl must not fail because of it
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var originalOut = Console.Out;
        using var outWriter = new StringWriter();

        try
        {
            Console.SetOut(outWriter);

            // Act
            _ = Program.Main([
                "vhdl",
                "--source", "*.vhd",
                "--output", outputDir,
                "--enforce-docs", "Public",
            ]);

            // Assert: the informational note must be printed regardless of the vhdl scan's outcome
            Assert.Contains("--enforce-docs is only supported for the dotnet subcommand", outWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Validates that an unrecognized <c>--enforce-docs</c> value is never parsed/validated
    ///     for the <c>cpp</c> subcommand: the flag is a no-op outside <c>dotnet</c> (regression
    ///     test for a bug where an invalid value threw an <see cref="ArgumentException"/> despite
    ///     the printed "ignoring" note). This does not assert overall exit code because the cpp
    ///     subcommand may independently fail for unrelated environmental reasons (for example,
    ///     clang not being installed); it only asserts that the invalid enforce-docs value itself
    ///     is not the cause of any failure.
    /// </summary>
    [Fact]
    public void Program_Main_InvalidEnforceDocsValueWithCppSubcommand_DoesNotThrowForEnforceDocs()
    {
        // Arrange: --enforce-docs is dotnet-only; an invalid value must not be parsed for cpp
        var outputDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var outWriter = new StringWriter();
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

            // Act
            _ = Program.Main([
                "cpp",
                "--includes", ".",
                "--output", outputDir,
                "--enforce-docs", "NotARealVisibilityTier",
            ]);

            // Assert: the note is printed, and the invalid enforce-docs value is never parsed —
            // so it must not appear in any error output (any failure must come from elsewhere)
            Assert.Contains("--enforce-docs is only supported for the dotnet subcommand", outWriter.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("NotARealVisibilityTier", errorWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }
}

