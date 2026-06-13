using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using Xunit;

namespace ApiMark.Core.Tests;

/// <summary>
///     Verifies the <see cref="IContext"/> interface contract. These tests confirm that
///     <see cref="InMemoryContext"/> correctly captures informational and error messages
///     in the appropriate in-memory lists so that generator unit tests can assert on
///     emitted output.
/// </summary>
public sealed class IContextTests
{
    /// <summary>
    ///     Verifies that a message passed to <see cref="IContext.WriteLine"/> appears in
    ///     <see cref="InMemoryContext.Lines"/>.
    /// </summary>
    [Fact]
    public void IContext_WriteLine_CapturesMessage_InLines()
    {
        // Arrange: a fresh in-memory context with no prior messages
        var context = new InMemoryContext();

        // Act: write one informational message through the interface
        context.WriteLine("hello");

        // Assert: the message must appear in Lines for downstream assertion
        Assert.Contains("hello", context.Lines);
    }

    /// <summary>
    ///     Verifies that a message passed to <see cref="IContext.WriteError"/> appears in
    ///     <see cref="InMemoryContext.Errors"/>.
    /// </summary>
    [Fact]
    public void IContext_WriteError_CapturesMessage_InErrors()
    {
        // Arrange: a fresh in-memory context with no prior messages
        var context = new InMemoryContext();

        // Act: write one error message through the interface
        context.WriteError("something went wrong");

        // Assert: the error message must appear in Errors for downstream assertion
        Assert.Contains("something went wrong", context.Errors);
    }

    /// <summary>
    ///     Verifies that an <see cref="InMemoryContext"/> correctly routes informational
    ///     messages to the informational channel and error messages to the error channel
    ///     without cross-contamination.
    /// </summary>
    [Fact]
    public void InMemoryContext_WriteLineAndWriteError_RouteToSeparateChannels()
    {
        // Arrange: a fresh in-memory context
        var context = new InMemoryContext();

        // Act: write to both channels
        context.WriteLine("info message");
        context.WriteError("error message");

        // Assert: informational message appears only in Lines
        Assert.Contains("info message", context.Lines);
        Assert.DoesNotContain("info message", context.Errors);

        // Assert: error message appears only in Errors
        Assert.Contains("error message", context.Errors);
        Assert.DoesNotContain("error message", context.Lines);
    }
}
