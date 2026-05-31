namespace ApiMark.Core;

/// <summary>
///     Factory interface for creating per-file Markdown writers.
/// </summary>
/// <remarks>
///     Decouples language generators from the file system. The file-system
///     implementation (FileMarkdownWriterFactory) writes to disk; test doubles
///     capture writes in memory. Callers inject the factory into
///     IApiGenerator.Generate so that the same generator code works in both
///     production and tests.
/// </remarks>
public interface IMarkdownWriterFactory
{
    /// <summary>
    ///     Creates a Markdown writer for a single output file.
    /// </summary>
    /// <param name="subFolder">
    ///     Subfolder path relative to the output root. Pass an empty string to
    ///     create a root-level file. Path separators should use '/' (forward slash).
    /// </param>
    /// <param name="name">
    ///     File name without extension. Must not be null, empty, or whitespace.
    /// </param>
    /// <returns>
    ///     A new <see cref="IMarkdownWriter"/> positioned at the start of the file
    ///     and ready for write calls. The caller is responsible for disposing the
    ///     returned writer.
    /// </returns>
    /// <remarks>
    ///     Implementations must create any required output directories before returning
    ///     the writer.
    /// </remarks>
    /// <exception cref="ArgumentException">
    ///     Thrown by implementations when <paramref name="name"/> is null, empty, or whitespace.
    /// </exception>
    IMarkdownWriter CreateMarkdown(string subFolder, string name);
}
