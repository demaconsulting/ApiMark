namespace ApiMark.Core;

/// <summary>
///     Minimal output channel that language generators use to emit informational
///     and error messages without depending on <see cref="Console"/>.
/// </summary>
/// <remarks>
///     Decoupling generators from <see cref="Console"/> allows callers to capture,
///     suppress, or redirect output — for example in tests that use an in-memory
///     implementation, or in hosts that route messages to a log file or build system
///     diagnostic channel.
/// </remarks>
public interface IContext
{
    /// <summary>
    ///     Writes an informational message to the output channel.
    /// </summary>
    /// <param name="message">The message to write. Must not be null.</param>
    void WriteLine(string message);

    /// <summary>
    ///     Writes an error or warning message to the output channel.
    /// </summary>
    /// <param name="message">The error or warning message to write. Must not be null.</param>
    void WriteError(string message);
}
