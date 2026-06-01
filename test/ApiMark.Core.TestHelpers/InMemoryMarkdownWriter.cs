using ApiMark.Core;

namespace ApiMark.Core.TestHelpers;

/// <summary>
///     Base record for all recorded Markdown write operations.
/// </summary>
/// <remarks>
///     Implementing as an abstract record hierarchy enables exhaustive pattern
///     matching in test assertions and keeps each operation type self-contained
///     with its arguments, rather than using a discriminated enum that forces
///     callers to inspect auxiliary fields.
/// </remarks>
#pragma warning disable S2094 // Intentionally empty abstract record: serves as discriminated-union base for the operation hierarchy
public abstract record MarkdownOperation;
#pragma warning restore S2094

/// <summary>
///     Records a call to <see cref="IMarkdownWriter.WriteHeading"/>.
/// </summary>
/// <param name="Level">The heading depth that was passed (1–4).</param>
/// <param name="Text">The heading text that was passed.</param>
public sealed record HeadingOperation(int Level, string Text) : MarkdownOperation;

/// <summary>
///     Records a call to <see cref="IMarkdownWriter.WriteSignature"/>.
/// </summary>
/// <param name="Language">The language identifier that was passed.</param>
/// <param name="Code">The signature code that was passed.</param>
public sealed record SignatureOperation(string Language, string Code) : MarkdownOperation;

/// <summary>
///     Records a call to <see cref="IMarkdownWriter.WriteParagraph"/>.
/// </summary>
/// <param name="Text">The paragraph text that was passed.</param>
public sealed record ParagraphOperation(string Text) : MarkdownOperation;

/// <summary>
///     Records a call to <see cref="IMarkdownWriter.WriteTable"/>.
/// </summary>
/// <param name="Headers">The column header labels that were passed.</param>
/// <param name="Rows">The data rows that were passed, captured as a read-only list.</param>
public sealed record TableOperation(string[] Headers, IReadOnlyList<string[]> Rows) : MarkdownOperation;

/// <summary>
///     Records a call to <see cref="IMarkdownWriter.WriteCodeBlock"/>.
/// </summary>
/// <param name="Language">The language identifier that was passed.</param>
/// <param name="Code">The example code that was passed.</param>
public sealed record CodeBlockOperation(string Language, string Code) : MarkdownOperation;

/// <summary>
///     Records a call to <see cref="IMarkdownWriter.WriteLink"/>.
/// </summary>
/// <param name="Text">The visible link label that was passed.</param>
/// <param name="RelativePath">The relative path that was passed.</param>
public sealed record LinkOperation(string Text, string RelativePath) : MarkdownOperation;

/// <summary>
///     In-memory test double for <see cref="IMarkdownWriter"/> that records every
///     write call for later inspection by test assertions.
/// </summary>
/// <remarks>
///     Rather than writing to the file system this implementation captures each
///     operation as a <see cref="MarkdownOperation"/> record. Tests can inspect
///     <see cref="Operations"/> to verify that a generator produces the expected
///     sequence of calls with the expected arguments, without requiring disk I/O.
/// </remarks>
public sealed class InMemoryMarkdownWriter : IMarkdownWriter
{
    /// <summary>Mutable backing store for recorded operations.</summary>
    private readonly List<MarkdownOperation> _operations = [];

    /// <summary>
    ///     Gets the ordered list of all write operations recorded since this
    ///     writer was created.
    /// </summary>
    /// <value>
    ///     A read-only view of the internal operations list. Operations appear in
    ///     the exact order the corresponding write methods were called.
    /// </value>
    public IReadOnlyList<MarkdownOperation> Operations => _operations;

    /// <summary>
    ///     Gets a value indicating whether <see cref="Dispose"/> has been called
    ///     on this writer.
    /// </summary>
    /// <value><see langword="true"/> after the first call to <see cref="Dispose"/>.</value>
    public bool IsDisposed { get; private set; }

    /// <summary>
    ///     Records a <see cref="HeadingOperation"/> with the supplied arguments.
    /// </summary>
    /// <param name="level">Heading depth 1–4.</param>
    /// <param name="text">Heading text.</param>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteHeading(int level, string text)
    {
        // Guard against use-after-dispose to match the IMarkdownWriter contract
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _operations.Add(new HeadingOperation(level, text));
    }

    /// <summary>
    ///     Records a <see cref="SignatureOperation"/> with the supplied arguments.
    /// </summary>
    /// <param name="language">Language identifier.</param>
    /// <param name="code">Signature code text.</param>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteSignature(string language, string code)
    {
        // Guard against use-after-dispose
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _operations.Add(new SignatureOperation(language, code));
    }

    /// <summary>
    ///     Records a <see cref="ParagraphOperation"/> with the supplied text.
    /// </summary>
    /// <param name="text">Paragraph body text.</param>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteParagraph(string text)
    {
        // Guard against use-after-dispose
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _operations.Add(new ParagraphOperation(text));
    }

    /// <summary>
    ///     Records a <see cref="TableOperation"/> capturing the headers and a
    ///     snapshot of the rows.
    /// </summary>
    /// <param name="headers">Column header labels.</param>
    /// <param name="rows">
    ///     Data rows. The sequence is materialized into a read-only list so that
    ///     lazy sequences evaluated after the call still produce correct results.
    /// </param>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteTable(string[] headers, IEnumerable<string[]> rows)
    {
        // Guard against use-after-dispose
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        // Snapshot both collections so that the recorded operation is immune to
        // post-call mutation: rows are materialized from any deferred-execution
        // sequence, and headers are copied defensively because the caller owns
        // the original array and may reuse or modify it after this call
        _operations.Add(new TableOperation(headers.ToArray(), rows.ToList()));
    }

    /// <summary>
    ///     Records a <see cref="CodeBlockOperation"/> with the supplied arguments.
    /// </summary>
    /// <param name="language">Language identifier.</param>
    /// <param name="code">Example code text.</param>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteCodeBlock(string language, string code)
    {
        // Guard against use-after-dispose
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _operations.Add(new CodeBlockOperation(language, code));
    }

    /// <summary>
    ///     Records a <see cref="LinkOperation"/> with the supplied arguments.
    /// </summary>
    /// <param name="text">Visible link label.</param>
    /// <param name="relativePath">Relative path target.</param>
    /// <exception cref="ObjectDisposedException">Thrown if this writer has been disposed.</exception>
    public void WriteLink(string text, string relativePath)
    {
        // Guard against use-after-dispose
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        _operations.Add(new LinkOperation(text, relativePath));
    }

    /// <summary>
    ///     Marks this writer as disposed. Does not clear recorded operations so
    ///     that tests can still inspect them after disposal.
    /// </summary>
    /// <remarks>Safe to call multiple times; subsequent calls are no-ops.</remarks>
    public void Dispose()
    {
        // Operations are intentionally preserved after disposal so that tests can
        // assert on the full sequence recorded during the lifetime of the writer
        IsDisposed = true;
    }
}
