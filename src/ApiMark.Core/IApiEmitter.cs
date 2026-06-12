namespace ApiMark.Core;

/// <summary>
///     Contract for the second stage of the two-stage documentation pipeline:
///     receives pre-parsed symbol data and emits Markdown output.
/// </summary>
/// <remarks>
///     An <see cref="IApiEmitter"/> is created by <see cref="IApiGenerator.Parse"/> after
///     all parsing is complete. Callers then invoke <see cref="Emit"/> with the desired
///     <see cref="EmitConfig"/> to produce the Markdown output. Separating parse from emit
///     allows the same parsed data to drive different output formats without re-parsing.
/// </remarks>
public interface IApiEmitter
{
    /// <summary>
    ///     Emits the complete Markdown documentation tree for the pre-parsed component.
    /// </summary>
    /// <param name="factory">
    ///     Factory used to create per-file Markdown writers. Must not be null.
    ///     For <see cref="OutputFormat.GradualDisclosure"/>, the generator creates one file
    ///     per namespace, type, and member. For <see cref="OutputFormat.SingleFile"/>,
    ///     only <c>api.md</c> is created via <c>factory.CreateMarkdown("", "api")</c>.
    /// </param>
    /// <param name="config">
    ///     Emit configuration controlling the output format and heading depth offset.
    ///     Must not be null.
    /// </param>
    /// <param name="context">
    ///     Output channel for informational and error messages. Must not be null.
    /// </param>
    void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context);
}
