namespace ApiMark.Core;

/// <summary>
///     File-system implementation of <see cref="IMarkdownWriter"/> that writes
///     Markdown content to a <see cref="StreamWriter"/>.
/// </summary>
/// <remarks>
///     Instances are created exclusively by <see cref="FileMarkdownWriterFactory"/>
///     and should not be constructed directly by callers. Each instance owns its
///     underlying <see cref="StreamWriter"/> and flushes and closes it on disposal.
///     Once disposed, any subsequent write call will throw <see cref="ObjectDisposedException"/>.
/// </remarks>
internal sealed class FileMarkdownWriter : IMarkdownWriter
{
    /// <summary>The underlying stream writer this instance writes Markdown to.</summary>
    private readonly StreamWriter _writer;

    /// <summary>Whether this instance has been disposed.</summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance wrapping the supplied <paramref name="writer"/>.
    /// </summary>
    /// <param name="writer">
    ///     The <see cref="StreamWriter"/> to write Markdown content to. Must not be null.
    ///     Ownership is transferred to this instance; the writer will be disposed when
    ///     this <see cref="FileMarkdownWriter"/> is disposed.
    /// </param>
    internal FileMarkdownWriter(StreamWriter writer)
    {
        _writer = writer;
    }

    /// <summary>
    ///     Writes a Markdown heading at the specified depth.
    /// </summary>
    /// <param name="level">
    ///     Heading depth 1–4 (# through ####). Out-of-range values produce a
    ///     syntactically incorrect heading but do not throw.
    /// </param>
    /// <param name="text">Heading text to display. Must not be null.</param>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteHeading(int level, string text)
    {
        // Guard against use-after-dispose to honour the IDisposable contract
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Emit the ATX heading prefix followed by the text and a blank line to
        // separate the heading from the next block element
        var prefix = new string('#', level);
        _writer.WriteLine($"{prefix} {text}");
        _writer.WriteLine();
    }

    /// <summary>
    ///     Writes a fenced code block representing an API signature.
    /// </summary>
    /// <param name="language">
    ///     Language identifier placed on the opening fence (e.g. "csharp").
    ///     Must not be null; use an empty string for an unlabelled fence.
    /// </param>
    /// <param name="code">Signature text. Must not be null.</param>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteSignature(string language, string code)
    {
        // Guard against use-after-dispose
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Emit a standard fenced code block with the language hint followed by a
        // blank line so that Markdown renderers correctly separate this block from
        // the next element
        _writer.WriteLine($"```{language}");
        _writer.WriteLine(code);
        _writer.WriteLine("```");
        _writer.WriteLine();
    }

    /// <summary>
    ///     Writes a prose paragraph of documentation text.
    /// </summary>
    /// <param name="text">Paragraph body. Must not be null.</param>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteParagraph(string text)
    {
        // Guard against use-after-dispose
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Emit the text followed by a blank line to close the Markdown paragraph
        _writer.WriteLine(text);
        _writer.WriteLine();
    }

    /// <summary>
    ///     Writes a pipe-delimited Markdown table with a header separator row.
    /// </summary>
    /// <param name="headers">Column header labels. Must not be null or empty.</param>
    /// <param name="rows">
    ///     Data rows, each containing the same number of cells as
    ///     <paramref name="headers"/>. Must not be null; may be empty.
    /// </param>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteTable(string[] headers, IEnumerable<string[]> rows)
    {
        // Guard against use-after-dispose
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Emit the header row with pipe delimiters
        _writer.WriteLine("| " + string.Join(" | ", headers) + " |");

        // Emit the mandatory separator row that signals this is a table, not a
        // paragraph; one dash cell per column is sufficient for valid GFM syntax
        _writer.WriteLine("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");

        // Emit each data row with the same pipe-delimited structure
        foreach (var row in rows)
        {
            _writer.WriteLine("| " + string.Join(" | ", row) + " |");
        }

        // Close the table block with a trailing blank line
        _writer.WriteLine();
    }

    /// <summary>
    ///     Writes a fenced code block containing a usage example.
    /// </summary>
    /// <param name="language">
    ///     Language identifier placed on the opening fence. Must not be null;
    ///     use an empty string for an unlabelled fence.
    /// </param>
    /// <param name="code">Example code. Must not be null.</param>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteCodeBlock(string language, string code)
    {
        // Guard against use-after-dispose
        ObjectDisposedException.ThrowIf(_disposed, this);

        // The fenced code block format is identical to WriteSignature; delegate to avoid duplication
        WriteSignature(language, code);
    }

    /// <summary>
    ///     Writes a relative navigation link to another documentation file.
    /// </summary>
    /// <param name="text">Visible link label. Must not be null or empty.</param>
    /// <param name="relativePath">
    ///     Relative path to the target file, written verbatim into the link href.
    ///     Must not be null.
    /// </param>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteLink(string text, string relativePath)
    {
        // Guard against use-after-dispose
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Emit a standard inline Markdown link followed by a blank line
        _writer.WriteLine($"[{text}]({relativePath})");
        _writer.WriteLine();
    }

    /// <summary>
    ///     Flushes all pending writes and releases the underlying
    ///     <see cref="StreamWriter"/> and its file handle.
    /// </summary>
    /// <remarks>
    ///     Safe to call multiple times; subsequent calls are no-ops.
    /// </remarks>
    public void Dispose()
    {
        // Avoid double-disposal; the underlying StreamWriter is not resilient to it
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Flush remaining buffered content and close the file handle
        _writer.Flush();
        _writer.Dispose();
    }
}
