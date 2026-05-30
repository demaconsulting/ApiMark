namespace ApiMark.Core;

/// <summary>
///     Per-file Markdown writing interface used by language generators to emit
///     structured documentation content.
/// </summary>
/// <remarks>
///     Each instance is scoped to a single output file. Callers write sections
///     in declaration order and then dispose the writer to flush and close the
///     file. After disposal the writer MUST NOT be used again; implementations
///     are free to throw <see cref="ObjectDisposedException"/> on any subsequent call.
/// </remarks>
public interface IMarkdownWriter : IDisposable
{
    /// <summary>
    ///     Writes a Markdown heading at the specified depth.
    /// </summary>
    /// <param name="level">
    ///     Heading depth. Valid values are 1–4 (corresponding to # through ####).
    ///     Callers must pass a value in this range; behavior for out-of-range
    ///     values is implementation-defined.
    /// </param>
    /// <param name="text">
    ///     Heading text. Must not be null or empty. Inline Markdown within the
    ///     text is passed through unchanged.
    /// </param>
    void WriteHeading(int level, string text);

    /// <summary>
    ///     Writes a fenced code block representing the API signature of a member.
    /// </summary>
    /// <param name="language">
    ///     Language identifier for syntax highlighting (e.g. "csharp", "cpp").
    ///     Must not be null. An empty string produces a plain fence with no hint.
    /// </param>
    /// <param name="code">
    ///     The signature text to display. Must not be null. Multi-line content
    ///     is passed through as-is inside the fence.
    /// </param>
    /// <remarks>
    ///     Distinct from <see cref="WriteCodeBlock"/> to allow rendering engines
    ///     or post-processors to distinguish signatures from usage examples.
    /// </remarks>
    void WriteSignature(string language, string code);

    /// <summary>
    ///     Writes a prose paragraph of documentation text.
    /// </summary>
    /// <param name="text">
    ///     Paragraph body. Must not be null. Inline Markdown (bold, links, etc.)
    ///     within the text is passed through unchanged.
    /// </param>
    void WriteParagraph(string text);

    /// <summary>
    ///     Writes a pipe-delimited Markdown table.
    /// </summary>
    /// <param name="headers">
    ///     Column header labels. Must not be null and must contain at least one
    ///     element. The length of this array determines the expected column count.
    /// </param>
    /// <param name="rows">
    ///     Sequence of data rows. Each row must contain the same number of
    ///     elements as <paramref name="headers"/>. Must not be null; may be empty.
    /// </param>
    void WriteTable(string[] headers, IEnumerable<string[]> rows);

    /// <summary>
    ///     Writes a fenced code block containing a usage example.
    /// </summary>
    /// <param name="language">
    ///     Language identifier for syntax highlighting. Must not be null.
    ///     An empty string produces a plain fence with no hint.
    /// </param>
    /// <param name="code">
    ///     Example code. Must not be null. Multi-line content is passed through
    ///     as-is inside the fence.
    /// </param>
    /// <remarks>
    ///     Distinct from <see cref="WriteSignature"/> to allow rendering engines
    ///     or post-processors to distinguish usage examples from API signatures.
    /// </remarks>
    void WriteCodeBlock(string language, string code);

    /// <summary>
    ///     Writes a relative navigation link to another documentation file.
    /// </summary>
    /// <param name="text">
    ///     Visible link label. Must not be null or empty.
    /// </param>
    /// <param name="relativePath">
    ///     Relative path to the target Markdown file, using forward slashes.
    ///     Must not be null. The path is written verbatim into the link href;
    ///     callers are responsible for correct relative addressing.
    /// </param>
    void WriteLink(string text, string relativePath);
}
