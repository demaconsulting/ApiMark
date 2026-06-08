using Microsoft.Extensions.Logging;

namespace ApiMark.DotNet.Fixtures;

/// <summary>
///     A fixture class for testing intra-doc type links and the External Types section.
/// </summary>
public class TypeLinkFixture
{
    /// <summary>
    ///     Gets a sample class instance.
    /// </summary>
    /// <returns>A new <see cref="SampleClass"/> instance.</returns>
    public SampleClass GetSampleClass() => new();

    /// <summary>
    ///     Logs a message using the supplied logger.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="message">The message to log.</param>
    public void Log(ILogger<TypeLinkFixture> logger, string message) =>
        logger.LogInformation("{Message}", message);
}
