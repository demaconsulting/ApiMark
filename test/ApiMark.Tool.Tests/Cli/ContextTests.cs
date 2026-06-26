using ApiMark.Core;
using ApiMark.Tool.Cli;
using Xunit;

namespace ApiMark.Tool.Tests.Cli;

/// <summary>Unit tests for <see cref="Context"/>.</summary>
public sealed class ContextTests
{
    /// <summary>
    ///     Validates that --version sets the Version property to true.
    /// </summary>
    [Fact]
    public void Context_Create_WithVersionFlag_SetsVersionTrue()
    {
        // Arrange: supply the long-form version flag
        var args = new[] { "--version" };

        // Act
        using var context = Context.Create(args);

        // Assert: Version property must be true
        Assert.True(context.Version);
    }

    /// <summary>
    ///     Validates that -v (short form) sets the Version property to true.
    /// </summary>
    [Fact]
    public void Context_Create_WithShortVersionFlag_SetsVersionTrue()
    {
        // Arrange: supply the short-form version flag
        var args = new[] { "-v" };

        // Act
        using var context = Context.Create(args);

        // Assert: Version property must be true
        Assert.True(context.Version);
    }

    /// <summary>
    ///     Validates that --help sets the Help property to true.
    /// </summary>
    [Fact]
    public void Context_Create_WithHelpFlag_SetsHelpTrue()
    {
        // Arrange: supply the long-form help flag
        var args = new[] { "--help" };

        // Act
        using var context = Context.Create(args);

        // Assert: Help property must be true
        Assert.True(context.Help);
    }

    /// <summary>
    ///     Validates that -? and -h (short forms) both set the Help property to true.
    /// </summary>
    [Theory]
    [InlineData("-?")]
    [InlineData("-h")]
    public void Context_Create_WithHelpShortFlags_SetHelpTrue(string flag)
    {
        // Arrange: supply one of the short-form help flags
        var args = new[] { flag };

        // Act
        using var context = Context.Create(args);

        // Assert: Help property must be true for all short-form variants
        Assert.True(context.Help);
    }

    /// <summary>
    ///     Validates that --silent sets the Silent property to true.
    /// </summary>
    [Fact]
    public void Context_Create_WithSilentFlag_SetsSilentTrue()
    {
        // Arrange: supply the silent flag
        var args = new[] { "--silent" };

        // Act
        using var context = Context.Create(args);

        // Assert: Silent property must be true
        Assert.True(context.Silent);
    }

    /// <summary>
    ///     Validates that --validate sets the Validate property to true.
    /// </summary>
    [Fact]
    public void Context_Create_WithValidateFlag_SetsValidateTrue()
    {
        // Arrange: supply the validate flag
        var args = new[] { "--validate" };

        // Act
        using var context = Context.Create(args);

        // Assert: Validate property must be true
        Assert.True(context.Validate);
    }

    /// <summary>
    ///     Validates that the first positional non-flag token is set as the Language property.
    /// </summary>
    [Fact]
    public void Context_Create_WithLanguageSubcommand_SetsLanguage()
    {
        // Arrange: supply a positional language subcommand token
        var args = new[] { "dotnet" };

        // Act
        using var context = Context.Create(args);

        // Assert: Language property must match the supplied token
        Assert.Equal("dotnet", context.Language);
    }

    /// <summary>
    ///     Validates that --assembly sets the Assembly property to the supplied path.
    /// </summary>
    [Fact]
    public void Context_Create_WithAssemblyOption_SetsAssemblyPath()
    {
        // Arrange: supply an assembly path
        var args = new[] { "--assembly", "my.dll" };

        // Act
        using var context = Context.Create(args);

        // Assert: Assembly property must match the supplied path
        Assert.Equal("my.dll", context.Assembly);
    }

    /// <summary>
    ///     Validates that --xml-doc sets the XmlDoc property to the supplied path.
    /// </summary>
    [Fact]
    public void Context_Create_WithXmlDocOption_SetsXmlDocPath()
    {
        // Arrange: supply an XML documentation file path
        var args = new[] { "--xml-doc", "my.xml" };

        // Act
        using var context = Context.Create(args);

        // Assert: XmlDoc property must match the supplied path
        Assert.Equal("my.xml", context.XmlDoc);
    }

    /// <summary>
    ///     Validates that --output sets the Output property to the supplied directory path.
    /// </summary>
    [Fact]
    public void Context_Create_WithOutputOption_SetsOutputPath()
    {
        // Arrange: supply an output directory path
        var args = new[] { "--output", "out/dir" };

        // Act
        using var context = Context.Create(args);

        // Assert: Output property must match the supplied path
        Assert.Equal("out/dir", context.Output);
    }

    /// <summary>
    ///     Validates that --visibility sets the Visibility property to the supplied value.
    /// </summary>
    [Fact]
    public void Context_Create_WithVisibilityOption_SetsVisibility()
    {
        // Arrange: supply a visibility value
        var args = new[] { "--visibility", "PublicAndProtected" };

        // Act
        using var context = Context.Create(args);

        // Assert: Visibility property must match the supplied value
        Assert.Equal("PublicAndProtected", context.Visibility);
    }

    /// <summary>
    ///     Validates that --include-obsolete sets the IncludeObsolete property to true.
    /// </summary>
    [Fact]
    public void Context_Create_WithIncludeObsoleteFlag_SetsIncludeObsoleteTrue()
    {
        // Arrange: supply the include-obsolete flag
        var args = new[] { "--include-obsolete" };

        // Act
        using var context = Context.Create(args);

        // Assert: IncludeObsolete property must be true
        Assert.True(context.IncludeObsolete);
    }

    /// <summary>
    ///     Validates that --depth sets the HeadingDepth property to the supplied integer value.
    /// </summary>
    [Fact]
    public void Context_Create_WithDepthOption_SetsHeadingDepth()
    {
        // Arrange: supply a heading depth value within the valid range
        var args = new[] { "--depth", "3" };

        // Act
        using var context = Context.Create(args);

        // Assert: HeadingDepth property must match the supplied value
        Assert.Equal(3, context.HeadingDepth);
    }

    /// <summary>
    ///     Validates that --depth with a value outside the 1–6 range or a non-integer throws ArgumentException.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("7")]
    [InlineData("abc")]
    public void Context_Create_WithDepthOptionOutOfRange_ThrowsArgumentException(string value)
    {
        // Arrange: supply a heading depth value that is invalid (out of range or non-integer)
        var args = new[] { "--depth", value };

        // Act / Assert: invalid depth must throw ArgumentException
        Assert.Throws<ArgumentException>(() => Context.Create(args));
    }

    /// <summary>
    ///     Validates that --results and its alias --result both set the ResultsFile property.
    /// </summary>
    [Theory]
    [InlineData("--results")]
    [InlineData("--result")]
    public void Context_Create_WithResultsOption_SetsResultsFile(string flag)
    {
        // Arrange: supply a results file path using the specified flag variant
        var args = new[] { flag, "results.trx" };

        // Act
        using var context = Context.Create(args);

        // Assert: ResultsFile property must match the supplied path for both flag forms
        Assert.Equal("results.trx", context.ResultsFile);
    }

    /// <summary>
    ///     Validates that <c>--includes</c> with a single path sets the <see cref="Context.Includes"/>
    ///     property to a one-element array containing that path.
    /// </summary>
    [Fact]
    public void Context_Create_WithIncludesOption_SetsIncludes()
    {
        // Arrange: supply a single plain directory path via --includes
        var args = new[] { "--includes", "path/a" };

        // Act
        using var context = Context.Create(args);

        // Assert: Includes property must contain the single supplied path
        string[] expectedIncludes = ["path/a"];
        Assert.Equal(expectedIncludes, context.Includes);
    }

    /// <summary>
    ///     Validates that repeated <c>--includes</c> flags accumulate all paths in order.
    /// </summary>
    [Fact]
    public void Context_Create_WithRepeatedIncludesFlags_AccumulatesAllPathsInOrder()
    {
        // Arrange: three separate --includes flags, each with a plain path
        var args = new[] { "--includes", "path/a", "--includes", "path/b", "--includes", "path/c" };

        // Act
        using var context = Context.Create(args);

        // Assert: all three paths must appear in Includes in the supplied order
        string[] expectedIncludes = ["path/a", "path/b", "path/c"];
        Assert.Equal(expectedIncludes, context.Includes);
    }

    /// <summary>
    ///     Validates that an empty argument array produces a Context with all default values.
    /// </summary>
    [Fact]
    public void Context_Create_WithNoArguments_HasDefaultValues()
    {
        // Arrange: supply an empty argument array
        var args = Array.Empty<string>();

        // Act
        using var context = Context.Create(args);

        // Assert: all properties must have their documented default values
        Assert.Multiple(
            () => Assert.False(context.Version),
            () => Assert.False(context.Help),
            () => Assert.False(context.Silent),
            () => Assert.False(context.Validate),
            () => Assert.Null(context.Language),
            () => Assert.Null(context.Assembly),
            () => Assert.Null(context.XmlDoc),
            () => Assert.Null(context.Output),
            () => Assert.Null(context.ResultsFile),
            () => Assert.Equal("Public", context.Visibility),
            () => Assert.False(context.IncludeObsolete),
            () => Assert.Equal(1, context.HeadingDepth),
            () => Assert.Empty(context.Includes),
            () => Assert.Empty(context.ApiHeaders),
            () => Assert.Equal(0, context.ExitCode));
    }

    /// <summary>
    ///     Validates that an unknown flag throws ArgumentException.
    /// </summary>
    [Fact]
    public void Context_Create_WithUnknownFlag_ThrowsArgumentException()
    {
        // Arrange: supply a flag that is not recognized by the parser
        var args = new[] { "--not-a-flag" };

        // Act / Assert: unknown flag must throw ArgumentException
        Assert.Throws<ArgumentException>(() => Context.Create(args));
    }

    /// <summary>
    ///     Validates that calling WriteError sets ExitCode to 1.
    /// </summary>
    [Fact]
    public void Context_WriteError_SetsExitCodeToOne()
    {
        // Arrange: create a context with no arguments
        using var context = Context.Create([]);

        // Act: report an error through WriteError
        context.WriteError("oops");

        // Assert: ExitCode must be 1 after any error is reported
        Assert.Equal(1, context.ExitCode);
    }

    /// <summary>
    ///     Validates that --log opens the specified file and that output written
    ///     through WriteLine appears in the file after Dispose.
    /// </summary>
    [Fact]
    public void Context_Create_WithLogFile_OpensAndWritesToLog()
    {
        // Arrange: create a temporary file path for the log
        var tempPath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName() + ".log");

        try
        {
            // Act: create context with --log, write a line, and dispose to flush
            using (var context = Context.Create(["--silent", "--log", tempPath]))
            {
                context.WriteLine("test output");
            }

            // Assert: the log file must exist and contain the written line
            Assert.True(File.Exists(tempPath), "Log file must be created");
            var content = File.ReadAllText(tempPath);
            Assert.Contains("test output", content);
        }
        finally
        {
            // Clean up the temporary log file regardless of test outcome
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>
    ///     Validates that all global flags can be parsed together in a single argument array.
    /// </summary>
    [Fact]
    public void Context_Cli_ParsesAllGlobalFlags()
    {
        // Arrange: supply all global flags in a single argument array
        var args = new[] { "--version", "--help", "--silent", "--validate" };

        // Act
        using var context = Context.Create(args);

        // Assert: all corresponding properties must be set
        Assert.Multiple(
            () => Assert.True(context.Version),
            () => Assert.True(context.Help),
            () => Assert.True(context.Silent),
            () => Assert.True(context.Validate));
    }

    /// <summary>
    ///     Validates that repeated <c>--includes</c> flags accumulate into the
    ///     <see cref="Context.Includes"/> array in order.
    /// </summary>
    [Fact]
    public void Context_Create_WithRepeatedIncludes_AccumulatesAllPaths()
    {
        // Arrange: supply --includes twice, each with one plain directory path
        var args = new[] { "--includes", "/usr/include", "--includes", "/opt/include" };

        // Act
        using var context = Context.Create(args);

        // Assert: both paths must appear in Includes in the order they were given
        Assert.Equal(["/usr/include", "/opt/include"], context.Includes);
        Assert.Empty(context.ApiHeaders);
    }

    /// <summary>
    ///     Validates that repeated <c>--api-headers</c> flags accumulate into the
    ///     <see cref="Context.ApiHeaders"/> array in order, preserving <c>!</c> exclusion patterns.
    /// </summary>
    [Fact]
    public void Context_Create_WithRepeatedApiHeaders_AccumulatesAllPatternsInOrder()
    {
        // Arrange: supply --api-headers three times to establish an include/exclude/re-include sequence
        var args = new[]
        {
            "--api-headers", "**/*",
            "--api-headers", "!**/detail/**",
            "--api-headers", "**/detail/public_api.h",
        };

        // Act
        using var context = Context.Create(args);

        // Assert: all three patterns must appear in ApiHeaders in the supplied order
        string[] expected = ["**/*", "!**/detail/**", "**/detail/public_api.h"];
        Assert.Equal(expected, context.ApiHeaders);
    }

    /// <summary>
    ///     Validates that a <c>!</c>-prefixed exclusion pattern is forwarded verbatim through
    ///     <c>--api-headers</c> without stripping the <c>!</c> prefix.
    /// </summary>
    [Fact]
    public void Context_Create_WithApiHeadersExclusionPattern_ForwardsVerbatim()
    {
        // Arrange: supply an exclusion pattern via --api-headers
        var args = new[] { "--api-headers", "!**/internal/**" };

        // Act
        using var context = Context.Create(args);

        // Assert: the '!' prefix must be preserved so CppGenerator can apply gitignore semantics
        Assert.Equal(["!**/internal/**"], context.ApiHeaders);
    }

    /// <summary>
    ///     Validates that <c>--includes</c> no longer accepts comma-separated lists and each
    ///     invocation appends exactly one plain directory path.
    /// </summary>
    [Fact]
    public void Context_Create_WithSingleInclude_SetsSinglePath()
    {
        // Arrange: supply a single plain directory path via --includes
        var args = new[] { "--includes", "/usr/include" };

        // Act
        using var context = Context.Create(args);

        // Assert: exactly one path must be in Includes with no ApiHeaders
        Assert.Equal(["/usr/include"], context.Includes);
        Assert.Empty(context.ApiHeaders);
    }

    /// <summary>
    ///     Validates that <see cref="Context"/> defaults <see cref="Context.ApiHeaders"/>
    ///     to an empty array when no <c>--api-headers</c> flags are supplied.
    /// </summary>
    [Fact]
    public void Context_Create_WithNoArguments_HasEmptyApiHeaders()
    {
        // Arrange: empty argument array
        var args = Array.Empty<string>();

        // Act
        using var context = Context.Create(args);

        // Assert: ApiHeaders must default to empty when not specified
        Assert.Empty(context.ApiHeaders);
    }

    /// <summary>
    ///     Validates that <c>--library-name</c> sets the <see cref="Context.LibraryName"/> property.
    /// </summary>
    [Fact]
    public void Context_Create_WithLibraryNameOption_SetsLibraryName()
    {
        // Arrange: supply a library name via --library-name
        var args = new[] { "--library-name", "MyAwesomeLib" };

        // Act
        using var context = Context.Create(args);

        // Assert: LibraryName property must match the supplied value
        Assert.Equal("MyAwesomeLib", context.LibraryName);
    }

    /// <summary>
    ///     Validates that <c>--library-description</c> sets the
    ///     <see cref="Context.LibraryDescription"/> property.
    /// </summary>
    [Fact]
    public void Context_Create_WithLibraryDescriptionOption_SetsLibraryDescription()
    {
        // Arrange: supply a description via --library-description
        var args = new[] { "--library-description", "A fast geometry library." };

        // Act
        using var context = Context.Create(args);

        // Assert: LibraryDescription property must match the supplied value
        Assert.Equal("A fast geometry library.", context.LibraryDescription);
    }

    /// <summary>
    ///     Validates that <c>--defines</c> splits the comma-separated value and stores the
    ///     individual defines in the <see cref="Context.Defines"/> array.
    /// </summary>
    [Fact]
    public void Context_Create_WithDefinesOption_SetsDefines()
    {
        // Arrange: supply two comma-separated preprocessor defines
        var args = new[] { "--defines", "MYLIB_API=,NDEBUG" };

        // Act
        using var context = Context.Create(args);

        // Assert: Defines array must contain each define as a separate entry
        Assert.Equal(["MYLIB_API=", "NDEBUG"], context.Defines);
    }

    /// <summary>
    ///     Validates that <c>--cpp-standard</c> sets the <see cref="Context.CppStandard"/> property.
    /// </summary>
    [Fact]
    public void Context_Create_WithCppStandardOption_SetsCppStandard()
    {
        // Arrange: supply a C++ standard via --cpp-standard
        var args = new[] { "--cpp-standard", "c++20" };

        // Act
        using var context = Context.Create(args);

        // Assert: CppStandard property must match the supplied value
        Assert.Equal("c++20", context.CppStandard);
    }

    /// <summary>
    ///     Validates that <c>--format gradual</c> sets <see cref="Context.Format"/> to
    ///     <see cref="OutputFormat.GradualDisclosure"/>.
    /// </summary>
    [Fact]
    public void Context_Create_WithFormatGradual_SetsGradualDisclosureFormat()
    {
        // Arrange
        var args = new[] { "--format", "gradual" };

        // Act
        using var context = Context.Create(args);

        // Assert
        Assert.Equal(OutputFormat.GradualDisclosure, context.Format);
    }

    /// <summary>
    ///     Validates that <c>--format single-file</c> sets <see cref="Context.Format"/> to
    ///     <see cref="OutputFormat.SingleFile"/>.
    /// </summary>
    [Fact]
    public void Context_Create_WithFormatSingleFile_SetsSingleFileFormat()
    {
        // Arrange
        var args = new[] { "--format", "single-file" };

        // Act
        using var context = Context.Create(args);

        // Assert
        Assert.Equal(OutputFormat.SingleFile, context.Format);
    }

    /// <summary>
    ///     Validates that the default <see cref="Context.Format"/> is
    ///     <see cref="OutputFormat.GradualDisclosure"/> when <c>--format</c> is not specified.
    /// </summary>
    [Fact]
    public void Context_Create_WithNoFormatOption_DefaultsToGradualDisclosure()
    {
        // Arrange: no --format argument
        var args = Array.Empty<string>();

        // Act
        using var context = Context.Create(args);

        // Assert: default must be GradualDisclosure
        Assert.Equal(OutputFormat.GradualDisclosure, context.Format);
    }

    /// <summary>
    ///     Validates that <c>--format</c> with an unrecognized value throws
    ///     <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Context_Create_WithInvalidFormat_ThrowsArgumentException()
    {
        // Arrange
        var args = new[] { "--format", "unknown-value" };

        // Act / Assert
        Assert.Throws<ArgumentException>(() => Context.Create(args));
    }

    /// <summary>
    ///     Validates that <c>--clang-path</c> sets the <see cref="Context.ClangPath"/> property
    ///     to the supplied path.
    /// </summary>
    [Fact]
    public void Context_Create_WithClangPathOption_SetsClangPath()
    {
        // Arrange: supply an explicit clang binary path
        var args = new[] { "--clang-path", "/usr/bin/clang" };

        // Act
        using var context = Context.Create(args);

        // Assert: ClangPath property must match the supplied path
        Assert.Equal("/usr/bin/clang", context.ClangPath);
    }

    /// <summary>
    ///     Validates that a single --source flag sets the Sources array.
    /// </summary>
    [Fact]
    public void Context_Create_WithSourceOption_SetsSources()
    {
        // Arrange
        var args = new[] { "--source", "src/**/*.vhd" };

        // Act
        using var context = Context.Create(args);

        // Assert
        Assert.Equal(["src/**/*.vhd"], context.Sources);
    }

    /// <summary>
    ///     Validates that repeated --source flags accumulate all patterns in order.
    /// </summary>
    [Fact]
    public void Context_Create_WithRepeatedSource_AccumulatesAllPaths()
    {
        // Arrange
        var args = new[] { "--source", "src/**/*.vhd", "--source", "src/**/*.vhdl" };

        // Act
        using var context = Context.Create(args);

        // Assert
        Assert.Equal(["src/**/*.vhd", "src/**/*.vhdl"], context.Sources);
    }

    /// <summary>
    ///     Validates that a --source flag with a ! exclusion prefix is forwarded verbatim.
    /// </summary>
    [Fact]
    public void Context_Create_WithSourceExclusionPattern_ForwardsVerbatim()
    {
        // Arrange
        var args = new[] { "--source", "src/**/*.vhd", "--source", "!src/tb/**/*.vhd" };

        // Act
        using var context = Context.Create(args);

        // Assert
        Assert.Equal(["src/**/*.vhd", "!src/tb/**/*.vhd"], context.Sources);
    }

    /// <summary>
    ///     Validates that output written through <see cref="Context.WriteError"/> is also
    ///     captured in the log file when <c>--log</c> is specified.
    /// </summary>
    [Fact]
    public void Context_OpenLogFile_ErrorOutputAlsoWrittenToLog()
    {
        // Arrange: create a temporary file path for the log
        var tempPath = Path.Join(Path.GetTempPath(), Path.GetRandomFileName() + ".log");

        try
        {
            // Act: create context with --log, write an error message, and dispose to flush
            using (var context = Context.Create(["--silent", "--log", tempPath]))
            {
                context.WriteError("test error output");
            }

            // Assert: the log file must exist and contain the error message
            Assert.True(File.Exists(tempPath), "Log file must be created");
            var content = File.ReadAllText(tempPath);
            Assert.Contains("test error output", content);
        }
        finally
        {
            // Clean up the temporary log file regardless of test outcome
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>
    ///     Validates that <c>--depth 4</c> is accepted by <see cref="Context"/> because
    ///     the valid range is 1–6; format-specific constraints are enforced downstream.
    /// </summary>
    [Fact]
    public void Context_Create_WithDepth4_SetsHeadingDepth()
    {
        // Arrange: supply --depth 4 — this is within the 1–6 range accepted by Context
        var args = new[] { "--depth", "4" };

        // Act
        using var context = Context.Create(args);

        // Assert: depth 4 is valid in Context and must be stored without error
        Assert.Equal(4, context.HeadingDepth);
    }

    /// <summary>
    ///     Validates that <c>--depth 6</c> is accepted by <see cref="Context"/> because 6
    ///     is the upper bound of valid ATX heading levels in Markdown (H1–H6).
    /// </summary>
    [Fact]
    public void Context_Create_WithDepth6_SetsHeadingDepth()
    {
        // Arrange: supply --depth 6 — the maximum value in the 1–6 first-principles range
        var args = new[] { "--depth", "6" };

        // Act
        using var context = Context.Create(args);

        // Assert: depth 6 is the upper boundary and must be stored without error
        Assert.Equal(6, context.HeadingDepth);
    }

    /// <summary>
    ///     Validates that <c>--depth 3</c> with <c>--format single-file</c> is accepted
    ///     (boundary value must not be rejected).
    /// </summary>
    [Fact]
    public void Context_Create_WithDepth3AndSingleFileFormat_Succeeds()
    {
        // Arrange: supply depth at the boundary value alongside single-file format
        var args = new[] { "--format", "single-file", "--depth", "3" };

        // Act
        using var context = Context.Create(args);

        // Assert: depth 3 is valid for single-file and both properties must be set
        Assert.Equal(3, context.HeadingDepth);
        Assert.Equal(OutputFormat.SingleFile, context.Format);
    }

    /// <summary>
    ///     Validates that supplying a flag token (starting with <c>'-'</c>) as the value
    ///     of <c>--output</c> throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Context_Create_WithFlagValueForOutput_ThrowsArgumentException()
    {
        // Arrange: supply a flag token as the --output value — the parser must reject it
        // rather than silently consuming the flag as a path string
        var args = new[] { "dotnet", "--output", "--silent" };

        // Act / Assert: a flag token supplied as an option value must throw ArgumentException
        Assert.Throws<ArgumentException>(() => Context.Create(args));
    }

    /// <summary>
    ///     Validates that supplying a flag token (starting with <c>'-'</c>) as the value
    ///     of <c>--log</c> throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Context_Create_WithFlagValueForLog_ThrowsArgumentException()
    {
        // Arrange: supply a flag token as the --log value — the parser must reject it
        // rather than silently consuming the flag as a filename string
        var args = new[] { "--log", "--silent" };

        // Act / Assert: a flag token supplied as an option value must throw ArgumentException
        Assert.Throws<ArgumentException>(() => Context.Create(args));
    }
}
