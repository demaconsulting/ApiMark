using ApiMark.Tool.Cli;
using Xunit;

namespace ApiMark.Tool.Tests.Cli;

/// <summary>
///     Subsystem tests for the Cli subsystem (Context and HelpCommand working together with Program).
/// </summary>
public class CliSubsystemTests
{
    /// <summary>
    ///     Test that the help command prints a requested topic from the built-in help cards.
    /// </summary>
    [Fact]
    public void CliSubsystem_HelpCommand_TopicRequested_PrintsTopic()
    {
        // Arrange: a help command that writes to memory
        using var output = new StringWriter();
        using var error = new StringWriter();
        var command = HelpCommand.Create(output, error);

        // Act: ask for the command-line topic
        var answered = command.TryRun(["help", "command-line"], out var exitCode);

        // Assert: the topic card is printed and the exit code is success
        Assert.True(answered);
        Assert.Equal(0, exitCode);
        Assert.Contains("# Command-line options", output.ToString());
    }
}
