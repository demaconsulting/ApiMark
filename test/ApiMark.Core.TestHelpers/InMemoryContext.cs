using ApiMark.Core;

namespace ApiMark.Core.TestHelpers;

/// <summary>
///     In-memory test double for <see cref="IContext"/> that captures messages
///     written by generators for assertion in unit tests.
/// </summary>
/// <remarks>
///     This implementation avoids any console or file-system interaction, making it
///     suitable for fast unit tests that need to verify generator output or diagnostic
///     messages without performing I/O. After a generator has called
///     <see cref="WriteLine"/> or <see cref="WriteError"/>, tests can inspect
///     <see cref="Lines"/> or <see cref="Errors"/> to verify the expected messages
///     were emitted.
/// </remarks>
public sealed class InMemoryContext : IContext
{
    /// <summary>Mutable backing store for informational messages.</summary>
    private readonly List<string> _lines = [];

    /// <summary>Mutable backing store for error and warning messages.</summary>
    private readonly List<string> _errors = [];

    /// <summary>
    ///     Gets the informational messages written via <see cref="WriteLine"/>,
    ///     in the order they were written.
    /// </summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>
    ///     Gets the error and warning messages written via <see cref="WriteError"/>,
    ///     in the order they were written.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    ///     Captures an informational message by appending it to <see cref="Lines"/>.
    /// </summary>
    /// <param name="message">The message to capture. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    public void WriteLine(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _lines.Add(message);
    }

    /// <summary>
    ///     Captures an error or warning message by appending it to <see cref="Errors"/>.
    /// </summary>
    /// <param name="message">The error or warning message to capture. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    public void WriteError(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _errors.Add(message);
    }
}
