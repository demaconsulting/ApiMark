using ApiMark.Core;

namespace ApiMark.Core.TestHelpers;

/// <summary>
///     In-memory test double for <see cref="IMarkdownWriterFactory"/> that creates
///     <see cref="InMemoryMarkdownWriter"/> instances keyed by subfolder and name.
/// </summary>
/// <remarks>
///     This implementation avoids any file-system interaction, making it suitable
///     for fast unit tests that verify generator output without performing I/O.
///     After a generator has called <see cref="CreateMarkdown"/> tests can inspect
///     <see cref="Writers"/> or use the <see cref="HasWriter"/> and
///     <see cref="GetWriter"/> helpers to verify the expected files were created
///     with the expected content.
///     Path keys are compared using ordinal (case-sensitive) string comparison;
///     callers must use the same casing in <see cref="CreateMarkdown"/>,
///     <see cref="HasWriter"/>, and <see cref="GetWriter"/>.
/// </remarks>
public sealed class InMemoryMarkdownWriterFactory : IMarkdownWriterFactory
{
    /// <summary>Mutable backing store keyed by normalized path.</summary>
    private readonly Dictionary<string, InMemoryMarkdownWriter> _writers = new(StringComparer.Ordinal);

    /// <summary>
    ///     Gets a read-only view of all writers created by this factory, keyed by
    ///     their normalized path (see <see cref="NormalizePath"/> for the format).
    /// </summary>
    /// <value>
    ///     A dictionary whose keys are the normalized paths and whose values are the
    ///     corresponding <see cref="InMemoryMarkdownWriter"/> instances.
    /// </value>
    public IReadOnlyDictionary<string, InMemoryMarkdownWriter> Writers => _writers;

    /// <summary>
    ///     Creates an <see cref="InMemoryMarkdownWriter"/> for the given subfolder
    ///     and file name, stores it internally, and returns it.
    /// </summary>
    /// <param name="subFolder">
    ///     Subfolder relative to the output root. Pass an empty string or whitespace
    ///     to target the root level.
    /// </param>
    /// <param name="name">File name without extension. Must not be null, empty, or whitespace.</param>
    /// <returns>
    ///     A new <see cref="InMemoryMarkdownWriter"/> ready for write calls. The caller
    ///     is responsible for disposing the returned writer.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="name"/> is null, empty, or whitespace.
    /// </exception>
    public IMarkdownWriter CreateMarkdown(string subFolder, string name)
    {
        // Validate the file name so callers get a clear error rather than a silent wrong-path key
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("File name must not be null or whitespace.", nameof(name));
        }

        // Normalize the key so that both HasWriter and GetWriter use consistent addressing
        var key = NormalizePath(subFolder, name);

        // Create and register a new in-memory writer for this path
        var writer = new InMemoryMarkdownWriter();
        _writers[key] = writer;
        return writer;
    }

    /// <summary>
    ///     Returns <see langword="true"/> if a writer was created for the given
    ///     subfolder and file name.
    /// </summary>
    /// <param name="subFolder">
    ///     Subfolder passed to <see cref="CreateMarkdown"/>. Pass an empty string
    ///     to check for a root-level file.
    /// </param>
    /// <param name="name">File name without extension.</param>
    /// <returns>
    ///     <see langword="true"/> when a writer with the matching normalized path
    ///     exists; <see langword="false"/> otherwise.
    /// </returns>
    public bool HasWriter(string subFolder, string name) =>
        _writers.ContainsKey(NormalizePath(subFolder, name));

    /// <summary>
    ///     Returns the writer created for the given subfolder and file name.
    /// </summary>
    /// <param name="subFolder">
    ///     Subfolder passed to <see cref="CreateMarkdown"/>. Pass an empty string
    ///     for a root-level file.
    /// </param>
    /// <param name="name">File name without extension.</param>
    /// <returns>The <see cref="InMemoryMarkdownWriter"/> for the specified path.</returns>
    /// <exception cref="KeyNotFoundException">
    ///     Thrown when no writer has been created for the specified path. Use
    ///     <see cref="HasWriter"/> to check existence before calling this method
    ///     when the writer may not exist.
    /// </exception>
    public InMemoryMarkdownWriter GetWriter(string subFolder, string name) =>
        _writers[NormalizePath(subFolder, name)];

    /// <summary>
    ///     Produces the canonical dictionary key for a subfolder and file name pair.
    /// </summary>
    /// <param name="subFolder">
    ///     Subfolder path. Null, empty, or whitespace is treated as root (no prefix).
    /// </param>
    /// <param name="name">File name without extension.</param>
    /// <returns>
    ///     A forward-slash–delimited key of the form <c>subFolder/name</c>, or just
    ///     <c>name</c> when <paramref name="subFolder"/> is absent.
    /// </returns>
    /// <remarks>
    ///     Keeping normalization in one place ensures HasWriter, GetWriter, and
    ///     CreateMarkdown all agree on what constitutes the same path.
    /// </remarks>
    private static string NormalizePath(string subFolder, string name) =>
        string.IsNullOrWhiteSpace(subFolder) ? name : $"{subFolder}/{name}";
}
