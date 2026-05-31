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
    ///     Validates that <see cref="ApiMarkTask.BuildArguments"/> produces an argument string
    ///     containing the <c>--includes</c> flag with include paths converted from the
    ///     MSBuild semicolon-separated format to the comma-separated format expected by the tool.
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

        // Assert: the --includes flag must appear and semicolons must be converted to commas
        Assert.Contains("--includes", args);
        Assert.Contains("/include1,/include2", args);
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
        var toolDllPath = Path.Combine(testDir, "ApiMark.Tool.dll");
        var fixtureAssembly = Path.Combine(testDir, "ApiMark.DotNet.Fixtures.dll");
        var xmlDocPath = Path.ChangeExtension(fixtureAssembly, ".xml");
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

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
                File.Exists(Path.Combine(outputDir, "api.md")),
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
}
