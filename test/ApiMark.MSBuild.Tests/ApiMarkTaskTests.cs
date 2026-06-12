using Microsoft.Build.Framework;
using NSubstitute;
using Xunit;

namespace ApiMark.MSBuild.Tests;

/// <summary>Integration and unit tests for <see cref="ApiMarkTask"/>.</summary>
public class ApiMarkTaskTests
{
    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.ResolveLanguage"/> returns <c>"cpp"</c> when
    ///     <see cref="ApiMarkTask.ProjectExtension"/> is <c>.vcxproj</c> and
    ///     <see cref="ApiMarkTask.ApiMarkLanguage"/> is not set.
    /// </summary>
    [Fact]
    public void ApiMarkTask_Language_InferredAsCpp_ForVcxproj()
    {
        // Arrange: project extension is .vcxproj with no explicit language override
        var task = new ApiMarkTask
        {
            ProjectExtension = ".vcxproj",
            ToolDllPath = "dummy.dll",
        };

        // Act
        var language = task.ResolveLanguage();

        // Assert: .vcxproj projects must infer the cpp generator
        Assert.Equal("cpp", language);
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.ResolveLanguage"/> returns <c>"dotnet"</c> when
    ///     <see cref="ApiMarkTask.ProjectExtension"/> is <c>.csproj</c> and
    ///     <see cref="ApiMarkTask.ApiMarkLanguage"/> is not set.
    /// </summary>
    [Fact]
    public void ApiMarkTask_Language_InferredAsDotNet_ForCsproj()
    {
        // Arrange: project extension is .csproj with no explicit language override
        var task = new ApiMarkTask
        {
            ProjectExtension = ".csproj",
            ToolDllPath = "dummy.dll",
        };

        // Act
        var language = task.ResolveLanguage();

        // Assert: .csproj projects must infer the dotnet generator
        Assert.Equal("dotnet", language);
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.BuildArguments"/> produces an argument string
    ///     containing <c>--assembly</c> and <c>--xml-doc</c> flags with the configured paths when
    ///     the language is <c>dotnet</c>.
    /// </summary>
    [Fact]
    public void ApiMarkTask_DotNet_SpawnsToolWithCorrectAssemblyAndXmlDocArguments()
    {
        // Arrange: configure assembly and XML doc paths for a dotnet invocation
        var task = new ApiMarkTask
        {
            ProjectExtension = ".csproj",
            ToolDllPath = "dummy.dll",
            ApiMarkAssemblyPath = "/some/path/api.dll",
            ApiMarkXmlDocPath = "/some/path/api.xml",
        };

        // Act
        var args = task.BuildArguments("dotnet");

        // Assert: both required dotnet flags must appear with their configured values
        Assert.Contains("--assembly", args);
        Assert.Contains("/some/path/api.dll", args);
        Assert.Contains("--xml-doc", args);
        Assert.Contains("/some/path/api.xml", args);
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.BuildArguments"/> emits a separate
    ///     <c>--includes</c> flag for each path in <see cref="ApiMarkTask.ApiMarkIncludePaths"/>
    ///     when the property is semicolon-separated, rather than joining them into a single
    ///     comma-separated value.
    /// </summary>
    [Fact]
    public void ApiMarkTask_Cpp_SpawnsToolWithCorrectIncludePathArguments()
    {
        // Arrange: configure semicolon-separated include paths for a cpp invocation
        var task = new ApiMarkTask
        {
            ProjectExtension = ".vcxproj",
            ToolDllPath = "dummy.dll",
            ApiMarkIncludePaths = "/include1;/include2",
        };

        // Act
        var args = task.BuildArguments("cpp");

        // Assert: each path must appear as a separate --includes <path> pair
        var argList = args.ToList();
        var firstIdx = argList.IndexOf("--includes");
        var lastIdx = argList.LastIndexOf("--includes");
        Assert.True(firstIdx >= 0, "--includes must be present");
        Assert.NotEqual(firstIdx, lastIdx);
        Assert.Equal("/include1", argList[firstIdx + 1]);
        Assert.Equal("/include2", argList[lastIdx + 1]);
        Assert.DoesNotContain("/include1,/include2", args);
        Assert.DoesNotContain("/include1;/include2", args);
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.BuildArguments"/> appends the <c>--output</c>
    ///     flag with the value of <see cref="ApiMarkTask.ApiMarkOutputDir"/> when that property
    ///     is set.
    /// </summary>
    [Fact]
    public void ApiMarkTask_OutputDir_ForwardedToToolAsOutputArgument()
    {
        // Arrange: set an explicit output directory alongside the required dotnet paths
        var task = new ApiMarkTask
        {
            ProjectExtension = ".csproj",
            ToolDllPath = "dummy.dll",
            ApiMarkAssemblyPath = "/some/api.dll",
            ApiMarkXmlDocPath = "/some/api.xml",
            ApiMarkOutputDir = "/some/output",
        };

        // Act
        var args = task.BuildArguments("dotnet");

        // Assert: the --output flag and the configured directory must appear
        Assert.Contains("--output", args);
        Assert.Contains("/some/output", args);
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.BuildArguments"/> appends the <c>--visibility</c>
    ///     flag with the value of <see cref="ApiMarkTask.ApiMarkVisibility"/> when that property
    ///     is set.
    /// </summary>
    [Fact]
    public void ApiMarkTask_Visibility_ForwardedToToolAsVisibilityArgument()
    {
        // Arrange: set an explicit visibility filter alongside the required dotnet paths
        var task = new ApiMarkTask
        {
            ProjectExtension = ".csproj",
            ToolDllPath = "dummy.dll",
            ApiMarkAssemblyPath = "/some/api.dll",
            ApiMarkXmlDocPath = "/some/api.xml",
            ApiMarkVisibility = "Public",
        };

        // Act
        var args = task.BuildArguments("dotnet");

        // Assert: the --visibility flag and the configured value must appear
        Assert.Contains("--visibility", args);
        Assert.Contains("Public", args);
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.Execute"/> returns <c>true</c> immediately and
    ///     logs no errors when <see cref="ApiMarkTask.DisableApiMark"/> is <c>true</c>, confirming
    ///     that the tool is never invoked.
    /// </summary>
    [Fact]
    public void ApiMarkTask_DisableApiMark_True_SkipsToolInvocation()
    {
        // Arrange: set DisableApiMark and use a non-existent ToolDllPath to confirm it is never checked
        var buildEngine = Substitute.For<IBuildEngine>();
        var task = new ApiMarkTask
        {
            BuildEngine = buildEngine,
            DisableApiMark = true,
            ProjectExtension = ".csproj",
            ToolDllPath = "path/does/not/exist.dll",
        };

        // Act
        var result = task.Execute();

        // Assert: must return true with no errors — the tool path is never validated or invoked
        Assert.True(result);
        buildEngine.DidNotReceive().LogErrorEvent(Arg.Any<BuildErrorEventArgs>());
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.BuildArguments"/> appends the <c>--include-obsolete</c>
    ///     flag when <see cref="ApiMarkTask.ApiMarkIncludeObsolete"/> is <c>true</c>.
    /// </summary>
    [Fact]
    public void ApiMarkTask_IncludeObsolete_True_ForwardsIncludeObsoleteFlag()
    {
        // Arrange: set IncludeObsolete alongside the required dotnet paths
        var task = new ApiMarkTask
        {
            ProjectExtension = ".csproj",
            ToolDllPath = "dummy.dll",
            ApiMarkAssemblyPath = "/some/api.dll",
            ApiMarkXmlDocPath = "/some/api.xml",
            ApiMarkIncludeObsolete = true,
        };

        // Act
        var args = task.BuildArguments("dotnet");

        // Assert: the --include-obsolete flag must appear in the argument string
        Assert.Contains("--include-obsolete", args);
    }

    /// <summary>
    ///     Validates that building a .NET project via <see cref="ApiMarkTask.Execute"/> with a real
    ///     fixture assembly spawns <c>ApiMark.Tool</c> and produces the expected <c>api.md</c>
    ///     output file.
    /// </summary>
    [Fact]
    public void ApiMarkTask_Execute_WithDotNetProject_GeneratesDocumentation()
    {
        // Arrange: locate runtime artifacts in the test output directory
        var testDir = Path.GetDirectoryName(typeof(ApiMarkTaskTests).Assembly.Location)!;
        var toolDllPath = Path.Join(testDir, "ApiMark.Tool.dll");
        var fixtureAssembly = Path.Join(testDir, "ApiMark.DotNet.Fixtures.dll");
        var xmlDocPath = Path.ChangeExtension(fixtureAssembly, ".xml");
        var outputDir = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            var buildEngine = Substitute.For<IBuildEngine>();
            var task = new ApiMarkTask
            {
                BuildEngine = buildEngine,
                ToolDllPath = toolDllPath,
                ProjectExtension = ".csproj",
                ApiMarkAssemblyPath = fixtureAssembly,
                ApiMarkXmlDocPath = xmlDocPath,
                ApiMarkOutputDir = outputDir,
            };

            // Act: execute the task — this spawns the real ApiMark.Tool child process
            var result = task.Execute();

            // Assert: task returns true and api.md is written to the output directory
            Assert.True(result);
            Assert.True(
                File.Exists(Path.Join(outputDir, "api.md")),
                "Expected api.md to be created in the output directory.");
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
    ///     Validates that <see cref="ApiMarkTask.BuildArguments"/> appends the <c>--library-name</c>
    ///     flag with the configured value when <see cref="ApiMarkTask.ApiMarkLibraryName"/> is set.
    /// </summary>
    [Fact]
    public void ApiMarkTask_Cpp_LibraryName_ForwardedToTool()
    {
        // Arrange: configure a library name alongside the required cpp include paths
        var task = new ApiMarkTask
        {
            ProjectExtension = ".vcxproj",
            ToolDllPath = "dummy.dll",
            ApiMarkIncludePaths = "/include",
            ApiMarkLibraryName = "MyAwesomeLib",
        };

        // Act
        var args = task.BuildArguments("cpp");

        // Assert: the --library-name flag and the configured value must appear
        Assert.Contains("--library-name", args);
        Assert.Contains("MyAwesomeLib", args);
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.BuildArguments"/> converts semicolons to commas
    ///     in <see cref="ApiMarkTask.ApiMarkDefines"/> when forwarding the <c>--defines</c> flag.
    /// </summary>
    [Fact]
    public void ApiMarkTask_Cpp_Defines_SemicolonsConvertedToCommas()
    {
        // Arrange: configure semicolon-separated defines for a cpp invocation
        var task = new ApiMarkTask
        {
            ProjectExtension = ".vcxproj",
            ToolDllPath = "dummy.dll",
            ApiMarkIncludePaths = "/include",
            ApiMarkDefines = "MYLIB_API=;NDEBUG",
        };

        // Act
        var args = task.BuildArguments("cpp");

        // Assert: the --defines flag must appear and semicolons must be converted to commas
        Assert.Contains("--defines", args);
        Assert.Contains("MYLIB_API=,NDEBUG", args);
        Assert.DoesNotContain("MYLIB_API=;NDEBUG", args);
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.BuildArguments"/> appends the <c>--cpp-standard</c>
    ///     flag with the configured value when <see cref="ApiMarkTask.ApiMarkCppStandard"/> is set.
    /// </summary>
    [Fact]
    public void ApiMarkTask_Cpp_CppStandard_ForwardedToTool()
    {
        // Arrange: configure an explicit C++ standard for a cpp invocation
        var task = new ApiMarkTask
        {
            ProjectExtension = ".vcxproj",
            ToolDllPath = "dummy.dll",
            ApiMarkIncludePaths = "/include",
            ApiMarkCppStandard = "c++20",
        };

        // Act
        var args = task.BuildArguments("cpp");

        // Assert: the --cpp-standard flag and the configured standard value must appear
        Assert.Contains("--cpp-standard", args);
        Assert.Contains("c++20", args);
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.Execute"/> returns <c>true</c> with no errors when
    ///     <see cref="ApiMarkTask.ApiMarkIncludePaths"/> is empty for a <c>.vcxproj</c> project,
    ///     confirming that C++ generation is skipped gracefully rather than failing the build.
    /// </summary>
    [Fact]
    public void ApiMarkTask_Cpp_EmptyIncludePaths_SkipsExecution()
    {
        // Arrange: vcxproj project with no include paths and a non-existent tool DLL to confirm
        // the tool is never invoked
        var buildEngine = Substitute.For<IBuildEngine>();
        var task = new ApiMarkTask
        {
            BuildEngine = buildEngine,
            ProjectExtension = ".vcxproj",
            ToolDllPath = "path/does/not/exist.dll",
            ApiMarkIncludePaths = string.Empty,
        };

        // Act: execute the task — should skip gracefully without touching the tool DLL path
        var result = task.Execute();

        // Assert: must return true with no errors logged
        Assert.True(result);
        buildEngine.DidNotReceive().LogErrorEvent(Arg.Any<BuildErrorEventArgs>());
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.BuildArguments"/> passes a
    ///     <see cref="ApiMarkTask.ApiMarkLibraryDescription"/> containing double-quote characters
    ///     as a verbatim list element, without applying any backslash escaping.
    /// </summary>
    [Fact]
    public void ApiMarkTask_BuildArguments_LibraryDescriptionWithDoubleQuote_PassedVerbatim()
    {
        // Arrange: a library description containing embedded double-quote characters
        var task = new ApiMarkTask
        {
            ProjectExtension = ".vcxproj",
            ToolDllPath = "dummy.dll",
            ApiMarkIncludePaths = "/include",
            ApiMarkLibraryName = "MyLib",
            ApiMarkLibraryDescription = "My library (v\"2\")",
        };

        // Act
        var args = task.BuildArguments("cpp");

        // Assert: the description is present verbatim — ArgumentList handles OS-level quoting,
        // so the caller must not apply any backslash escaping
        Assert.Contains("My library (v\"2\")", args);
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.BuildArguments"/> emits a separate
    ///     <c>--api-headers</c> flag for each pattern in <see cref="ApiMarkTask.ApiMarkApiHeaders"/>,
    ///     preserving order and forwarding <c>!</c>-prefixed exclusion patterns verbatim.
    /// </summary>
    [Fact]
    public void ApiMarkTask_Cpp_ApiHeaders_ForwardedAsIndividualFlags()
    {
        // Arrange: two patterns — a catch-all followed by an exclusion pattern
        var task = new ApiMarkTask
        {
            ProjectExtension = ".vcxproj",
            ToolDllPath = "dummy.dll",
            ApiMarkIncludePaths = "/include",
            ApiMarkApiHeaders = "**/*.h;!**/detail/**",
        };

        // Act
        var args = task.BuildArguments("cpp");

        // Assert: each pattern must appear as its own --api-headers <pattern> pair in order
        var argList = args.ToList();
        var firstIdx = argList.IndexOf("--api-headers");
        var lastIdx = argList.LastIndexOf("--api-headers");
        Assert.True(firstIdx >= 0, "--api-headers must be present");
        Assert.NotEqual(firstIdx, lastIdx);
        Assert.Equal("**/*.h", argList[firstIdx + 1]);

        // The exclusion pattern must be forwarded verbatim with the ! prefix intact
        Assert.Equal("!**/detail/**", argList[lastIdx + 1]);
        Assert.DoesNotContain("**/*.h;!**/detail/**", args);
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.BuildArguments"/> appends the
    ///     <c>--library-description</c> flag with the configured value when
    ///     <see cref="ApiMarkTask.ApiMarkLibraryDescription"/> is set.
    /// </summary>
    [Fact]
    public void ApiMarkTask_Cpp_LibraryDescription_ForwardedToTool()
    {
        // Arrange: configure a library description alongside the required cpp include paths
        var task = new ApiMarkTask
        {
            ProjectExtension = ".vcxproj",
            ToolDllPath = "dummy.dll",
            ApiMarkIncludePaths = "/include",
            ApiMarkLibraryDescription = "A fast geometry library.",
        };

        // Act
        var args = task.BuildArguments("cpp");

        // Assert: the --library-description flag and the configured value must appear
        Assert.Contains("--library-description", args);
        Assert.Contains("A fast geometry library.", args);
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.BuildArguments"/> appends the <c>--clang-path</c>
    ///     flag with the configured value when <see cref="ApiMarkTask.ApiMarkClangPath"/> is set.
    /// </summary>
    [Fact]
    public void ApiMarkTask_Cpp_ClangPath_ForwardedToTool()
    {
        // Arrange: configure an explicit clang path alongside the required cpp include paths
        var task = new ApiMarkTask
        {
            ProjectExtension = ".vcxproj",
            ToolDllPath = "dummy.dll",
            ApiMarkIncludePaths = "/include",
            ApiMarkClangPath = "/usr/local/bin/clang",
        };

        // Act
        var args = task.BuildArguments("cpp");

        // Assert: the --clang-path flag and the configured path must appear
        Assert.Contains("--clang-path", args);
        Assert.Contains("/usr/local/bin/clang", args);
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.Execute"/> returns <c>true</c> with no errors
    ///     when <see cref="ApiMarkTask.ApiMarkXmlDocPath"/> is empty for a <c>.csproj</c> project,
    ///     confirming that .NET generation is skipped gracefully rather than failing the build.
    /// </summary>
    [Fact]
    public void ApiMarkTask_DotNet_EmptyXmlDocPath_SkipsExecution()
    {
        // Arrange: csproj project with no XML doc path and a non-existent tool DLL to confirm
        // the tool is never invoked
        var buildEngine = Substitute.For<IBuildEngine>();
        var task = new ApiMarkTask
        {
            BuildEngine = buildEngine,
            ProjectExtension = ".csproj",
            ToolDllPath = "path/does/not/exist.dll",
            ApiMarkXmlDocPath = string.Empty,
        };

        // Act: execute the task — should skip gracefully without touching the tool DLL path
        var result = task.Execute();

        // Assert: must return true with no errors logged
        Assert.True(result);
        buildEngine.DidNotReceive().LogErrorEvent(Arg.Any<BuildErrorEventArgs>());
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.BuildArguments"/> appends the <c>--format</c>
    ///     flag with the configured value when <see cref="ApiMarkTask.ApiMarkFormat"/> is set.
    /// </summary>
    [Fact]
    public void ApiMarkTask_Format_ForwardedToToolAsFormatArgument()
    {
        // Arrange: set format alongside the required dotnet paths
        var task = new ApiMarkTask
        {
            ProjectExtension = ".csproj",
            ToolDllPath = "dummy.dll",
            ApiMarkAssemblyPath = "/some/api.dll",
            ApiMarkXmlDocPath = "/some/api.xml",
            ApiMarkFormat = "single-file",
        };

        // Act
        var args = task.BuildArguments("dotnet");

        // Assert: the --format flag and the configured value must appear
        Assert.Contains("--format", args);
        Assert.Contains("single-file", args);
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.BuildArguments"/> does not append any
    ///     <c>--format</c> flag when <see cref="ApiMarkTask.ApiMarkFormat"/> is null or empty.
    /// </summary>
    [Fact]
    public void ApiMarkTask_Format_NotForwarded_WhenNotSet()
    {
        // Arrange: no format configured
        var task = new ApiMarkTask
        {
            ProjectExtension = ".csproj",
            ToolDllPath = "dummy.dll",
            ApiMarkAssemblyPath = "/some/api.dll",
            ApiMarkXmlDocPath = "/some/api.xml",
        };

        // Act
        var args = task.BuildArguments("dotnet");

        // Assert: the --format flag must NOT appear
        Assert.DoesNotContain("--format", args);
    }

    /// <summary>
    ///     Validates that <see cref="ApiMarkTask.BuildArgumentsForOutput"/> overrides scalar
    ///     output directory, visibility, and format properties from item group metadata.
    /// </summary>
    [Fact]
    public void ApiMarkTask_BuildArgumentsForOutput_OverridesScalarPropertiesFromMetadata()
    {
        // Arrange: base scalar properties plus an output item that overrides them all
        var task = new ApiMarkTask
        {
            ProjectExtension = ".csproj",
            ToolDllPath = "dummy.dll",
            ApiMarkAssemblyPath = "/some/api.dll",
            ApiMarkXmlDocPath = "/some/api.xml",
            ApiMarkOutputDir = "/base/output",
            ApiMarkVisibility = "Public",
            ApiMarkFormat = "gradual",
        };

        var outputItem = Substitute.For<Microsoft.Build.Framework.ITaskItem>();
        outputItem.GetMetadata("OutputDir").Returns("/item/output");
        outputItem.GetMetadata("Visibility").Returns("All");
        outputItem.GetMetadata("Format").Returns("single-file");

        // Act
        var args = task.BuildArgumentsForOutput("dotnet", outputItem).ToList();

        // Assert: item metadata values must override scalar properties
        var outputIdx = args.IndexOf("--output");
        Assert.True(outputIdx >= 0, "--output flag must appear");
        Assert.Equal("/item/output", args[outputIdx + 1]);

        var visIdx = args.IndexOf("--visibility");
        Assert.True(visIdx >= 0, "--visibility flag must appear");
        Assert.Equal("All", args[visIdx + 1]);

        var formatIdx = args.IndexOf("--format");
        Assert.True(formatIdx >= 0, "--format flag must appear");
        Assert.Equal("single-file", args[formatIdx + 1]);
    }

    /// <summary>
    ///     Validates that scalar properties are restored after
    ///     <see cref="ApiMarkTask.BuildArgumentsForOutput"/> returns, so subsequent calls
    ///     still use the original values.
    /// </summary>
    [Fact]
    public void ApiMarkTask_BuildArgumentsForOutput_RestoresScalarPropertiesAfterCall()
    {
        // Arrange
        var task = new ApiMarkTask
        {
            ProjectExtension = ".csproj",
            ToolDllPath = "dummy.dll",
            ApiMarkAssemblyPath = "/api.dll",
            ApiMarkXmlDocPath = "/api.xml",
            ApiMarkOutputDir = "/original-output",
            ApiMarkVisibility = "Public",
            ApiMarkFormat = "gradual",
        };

        var outputItem = Substitute.For<Microsoft.Build.Framework.ITaskItem>();
        outputItem.GetMetadata("OutputDir").Returns("/override-output");
        outputItem.GetMetadata("Visibility").Returns("All");
        outputItem.GetMetadata("Format").Returns("single-file");

        // Act: call BuildArgumentsForOutput, then BuildArguments again
        task.BuildArgumentsForOutput("dotnet", outputItem);
        var afterArgs = task.BuildArguments("dotnet").ToList();

        // Assert: scalar properties must have been restored to their original values
        var outputIdx = afterArgs.IndexOf("--output");
        Assert.True(outputIdx >= 0);
        Assert.Equal("/original-output", afterArgs[outputIdx + 1]);

        var visIdx = afterArgs.IndexOf("--visibility");
        Assert.True(visIdx >= 0);
        Assert.Equal("Public", afterArgs[visIdx + 1]);

        var formatIdx = afterArgs.IndexOf("--format");
        Assert.True(formatIdx >= 0);
        Assert.Equal("gradual", afterArgs[formatIdx + 1]);
    }
}

