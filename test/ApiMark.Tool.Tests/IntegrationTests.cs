using ApiMark.Tool.Cli;
using ApiMark.Tool.SelfTest;
using DemaConsulting.HelpCards.Catalog;
using DemaConsulting.HelpCards.Testing;
using Xunit;

namespace ApiMark.Tool.Tests;

/// <summary>
///     System-level integration tests that run the ApiMark tool out of process via <c>dotnet</c>.
/// </summary>
public class IntegrationTests
{
    /// <summary>
    ///     The path of the tool assembly in the test output folder.
    /// </summary>
    private readonly string _dllPath;

    /// <summary>
    ///     Initializes the test by locating the tool assembly.
    /// </summary>
    public IntegrationTests()
    {
        // The DLL is in the same folder as the test assembly because the test project references the tool
        _dllPath = Path.Join(AppContext.BaseDirectory, "ApiMark.Tool.dll");

        Assert.True(File.Exists(_dllPath), $"Could not find the ApiMark tool DLL at {_dllPath}");
    }

    /// <summary>
    ///     Test that the help verb lists the help topics.
    /// </summary>
    [Fact]
    public void ApiMarkTool_HelpTopics_HelpVerb_ListsTopics()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: run the tool with the help verb
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "help");

        // Assert: every topic is listed with its command, without the banner
        Assert.Equal(0, exitCode);
        foreach (var topic in HelpCommand.RootCardIds)
        {
            Assert.Contains($"{HelpCommand.ToolName} help {topic}", output);
        }

        Assert.DoesNotContain("Copyright", output);
    }

    /// <summary>
    ///     Test that an unknown help topic returns the usage-error exit code.
    /// </summary>
    [Fact]
    public void ApiMarkTool_HelpTopics_UnknownTopic_ReturnsUsageError()
    {
        // Arrange: (none — constructor initializes _dllPath)

        // Act: ask for a topic that does not exist
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "help",
            "no-such-topic");

        // Assert: exit code 2 and a message that names the topic
        Assert.Equal(2, exitCode);
        Assert.Contains("no-such-topic", output);
    }

    /// <summary>
    ///     Test that every command example in the help cards gives the result that its card states.
    /// </summary>
    [Fact]
    public void ApiMarkTool_HelpExamples_AllCommandExamples_GiveStatedResults()
    {
        // Arrange: the catalog the tool ships, the input files that the examples read, and the sample
        // .NET assembly of the self-validation, which the .NET examples read as MyLib.dll and MyLib.xml
        var catalog = HelpCatalog.Create(HelpCommand.CreateOptions().GetCatalogConfiguration());
        var exampleFiles = Directory.GetFiles(Path.Join(AppContext.BaseDirectory, "TestData", "HelpExamples"));
        var sampleDirectory = Path.Join(Path.GetTempPath(), $"apimark_help_sample_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(sampleDirectory);
            var (assemblyPath, xmlDocPath) = Validation.WriteSampleAssembly(sampleDirectory);
            var inputs = exampleFiles
                .Select(file => (Source: file, Name: Path.GetFileName(file)))
                .Append((Source: assemblyPath, Name: "MyLib.dll"))
                .Append((Source: xmlDocPath, Name: "MyLib.xml"))
                .ToList();

            // Act: run every command example against the built tool in the runner's scratch folder; the runner
            // compares the exit codes and the 'expect' fragments. The inputs are copied in before every command
            // unless the card saved a file of the same name from its own source example, which wins.
            var report = HelpExampleRunner.Run(catalog, invocation =>
            {
                foreach (var (source, name) in inputs)
                {
                    var target = Path.Join(invocation.WorkingDirectory, name);
                    if (!File.Exists(target))
                    {
                        File.Copy(source, target);
                    }
                }

                var exitCode = Runner.RunInDirectory(
                    out var output,
                    invocation.WorkingDirectory,
                    "dotnet",
                    [_dllPath, .. invocation.Arguments]);
                return new HelpExampleOutcome(exitCode, output);
            });

            // Assert: the cards hold command examples, and every one gave the result its card states
            Assert.True(report.CommandsRun > 0, "The help cards contain no command examples.");
            report.AssertSucceeded();
        }
        finally
        {
            // Clean up the sample assembly folder
            if (Directory.Exists(sampleDirectory))
            {
                Directory.Delete(sampleDirectory, recursive: true);
            }
        }
    }
}
