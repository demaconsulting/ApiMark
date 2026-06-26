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

    /// <summary>
    ///     Verifies that passing a null message to <see cref="IContext.WriteLine"/>
    ///     throws <see cref="ArgumentNullException"/>, enforcing the null contract
    ///     defined on the <see cref="IContext"/> interface.
    /// </summary>
    [Fact]
    public void InMemoryContext_WriteLine_NullMessage_ThrowsArgumentNullException()
    {
        // Arrange: a fresh in-memory context
        var context = new InMemoryContext();

        // Act / Assert: null message must be rejected with ArgumentNullException
        Assert.Throws<ArgumentNullException>(() => context.WriteLine(null!));
    }

    /// <summary>
    ///     Verifies that passing a null message to <see cref="IContext.WriteError"/>
    ///     throws <see cref="ArgumentNullException"/>, enforcing the null contract
    ///     defined on the <see cref="IContext"/> interface.
    /// </summary>
    [Fact]
    public void InMemoryContext_WriteError_NullMessage_ThrowsArgumentNullException()
    {
        // Arrange: a fresh in-memory context
        var context = new InMemoryContext();

        // Act / Assert: null message must be rejected with ArgumentNullException
        Assert.Throws<ArgumentNullException>(() => context.WriteError(null!));
    }

    /// <summary>
    ///     Verifies that multiple <see cref="IContext.WriteLine"/> and
    ///     <see cref="IContext.WriteError"/> calls produce messages in the exact order
    ///     they were written, with no reordering across channels.
    /// </summary>
    [Fact]
    public void InMemoryContext_MultipleMessages_MaintainCallOrder()
    {
        // Arrange: a fresh in-memory context
        var context = new InMemoryContext();

        // Act: interleave informational and error messages in a defined sequence
        context.WriteLine("line-1");
        context.WriteError("error-1");
        context.WriteLine("line-2");
        context.WriteError("error-2");
        context.WriteLine("line-3");

        // Assert: Lines must contain the informational messages in call order
        Assert.Equal(3, context.Lines.Count);
        Assert.Equal("line-1", context.Lines[0]);
        Assert.Equal("line-2", context.Lines[1]);
        Assert.Equal("line-3", context.Lines[2]);

        // Assert: Errors must contain the error messages in call order
        Assert.Equal(2, context.Errors.Count);
        Assert.Equal("error-1", context.Errors[0]);
        Assert.Equal("error-2", context.Errors[1]);
    }
}
