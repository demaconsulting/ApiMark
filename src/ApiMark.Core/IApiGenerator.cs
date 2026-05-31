namespace ApiMark.Core;

/// <summary>
///     Contract every language-specific generator must implement.
/// </summary>
/// <remarks>
///     Decouples callers (ApiMarkTask and Program) from any concrete language module.
///     A caller constructs a generator with language-specific options, then calls Generate —
///     the caller never needs to know which language it is processing.
/// </remarks>
public interface IApiGenerator
{
    /// <summary>
    ///     Generates the full Markdown documentation tree for a configured software component.
    /// </summary>
    /// <param name="factory">
    ///     Factory used to create per-file Markdown writers for each output file.
    ///     Must not be null. The factory is responsible for creating output directories.
    ///     The generator MUST call factory.CreateMarkdown("", "api") to produce the
    ///     fixed top-level entrypoint file "api.md".
    /// </param>
    /// <remarks>
    ///     The output MUST include a file named "api.md" at the root (created via
    ///     factory.CreateMarkdown("", "api")) as the fixed entrypoint. Additional
    ///     files are language-module-specific.
    /// </remarks>
    void Generate(IMarkdownWriterFactory factory);
}
