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
    ///     Heading level in the range 1–6. Values outside this range throw
    ///     <see cref="ArgumentOutOfRangeException"/>: zero or negative are always
    ///     invalid; values above 6 are not defined by CommonMark and will not render
    ///     as headings in standard Markdown processors.
    /// </param>
    /// <param name="text">Heading text to display. Must not be null or empty.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="level"/> is less than 1 or greater than 6.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteHeading(int level, string text)
    {
        // Guard against use-after-dispose to honour the IDisposable contract
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Reject levels outside the 1–6 range defined by CommonMark ATX headings
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(level);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, 6);

        // Reject null or empty text — a heading with no text is not valid Markdown
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("Heading text must not be empty.", nameof(text));
        }

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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="language"/> or <paramref name="code"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteSignature(string language, string code)
    {
        // Guard against use-after-dispose
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Guard against null parameters so callers get a clear diagnostic
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(code);

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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteParagraph(string text)
    {
        // Guard against use-after-dispose
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Guard against null parameter
        ArgumentNullException.ThrowIfNull(text);

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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="headers"/> or <paramref name="rows"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="headers"/> is empty, when any element in
    ///     <paramref name="headers"/> is null, when any row in
    ///     <paramref name="rows"/> is null, when any row has a different number of
    ///     cells than <paramref name="headers"/>, or when any cell within a row is null.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteTable(string[] headers, IEnumerable<string[]> rows)
    {
        // Guard against use-after-dispose
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Guard against null parameters
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        // Reject an empty headers array — a headerless table is not valid Markdown
        if (headers.Length == 0)
        {
            throw new ArgumentException("Headers array must not be empty.", nameof(headers));
        }

        // Reject any header element that is null — a null header cannot produce a valid column label
        for (var i = 0; i < headers.Length; i++)
        {
            if (headers[i] == null)
            {
                throw new ArgumentException($"Header at index {i} must not be null.", nameof(headers));
            }
        }

        // Materialize rows once so the column count of each row can be validated before
        // any output is written; this prevents partially-written malformed tables
        var rowList = rows.ToList();

        // Reject any row that is null or whose column count does not match the header count
        for (var i = 0; i < rowList.Count; i++)
        {
            if (rowList[i] == null)
            {
                throw new ArgumentException("Table rows must not contain null entries.", nameof(rows));
            }

            if (rowList[i].Length != headers.Length)
            {
                throw new ArgumentException(
                    $"Row {i} has {rowList[i].Length} cell(s) but {headers.Length} were expected to match the header count.",
                    nameof(rows));
            }

            // Reject any cell within the row that is null — a null cell cannot be rendered as Markdown
            for (var j = 0; j < rowList[i].Length; j++)
            {
                if (rowList[i][j] == null)
                {
                    throw new ArgumentException($"Row {i}, cell {j} must not be null.", nameof(rows));
                }
            }
        }

        // Emit the header row with pipe delimiters; escape any literal pipe characters
        // in cell values so they do not break the table structure in Markdown renderers
        _writer.WriteLine("| " + string.Join(" | ", headers.Select(h => h.Replace("|", @"\|", StringComparison.Ordinal))) + " |");

        // Emit the mandatory separator row that signals this is a table, not a
        // paragraph; one dash cell per column is sufficient for valid GFM syntax.
        // Separator cells are fixed strings and never require pipe escaping.
        _writer.WriteLine("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");

        // Emit each data row, escaping pipe characters in every cell value
        foreach (var row in rowList)
        {
            _writer.WriteLine("| " + string.Join(" | ", row.Select(cell => cell.Replace("|", @"\|", StringComparison.Ordinal))) + " |");
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="language"/> or <paramref name="code"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteCodeBlock(string language, string code)
    {
        // Guard against use-after-dispose
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Guard against null parameters so callers get a clear diagnostic
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(code);

        // Both WriteSignature and WriteCodeBlock produce identical fenced code block output;
        // WriteCodeBlock delegates here to avoid duplication. The methods are distinct at the
        // API level only; post-processors may differentiate them in future.
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="relativePath"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteLink(string text, string relativePath)
    {
        // Guard against use-after-dispose
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Guard against null parameters so callers get a clear diagnostic
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(relativePath);

        // Reject empty link text — a link with no visible label is not valid Markdown
        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("Link text must not be empty.", nameof(text));
        }

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
