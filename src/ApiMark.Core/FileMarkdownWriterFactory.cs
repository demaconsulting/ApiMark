namespace ApiMark.Core;

/// <summary>
///     File-system implementation of <see cref="IMarkdownWriterFactory"/> that
///     writes output files under a configured root directory.
/// </summary>
/// <remarks>
///     This is the production implementation of the factory interface. It creates
///     output directories on demand so that callers do not need to manage the
///     directory structure manually. Language generators should accept an injected
///     <see cref="IMarkdownWriterFactory"/> rather than instantiating this class
///     directly, enabling test doubles to be substituted without touching the disk.
/// </remarks>
public sealed class FileMarkdownWriterFactory : IMarkdownWriterFactory
{
    /// <summary>The root directory under which all output files are written.</summary>
    private readonly string _outputDirectory;

    /// <summary>
    ///     Initializes a new instance that writes files under <paramref name="outputDirectory"/>.
    /// </summary>
    /// <param name="outputDirectory">
    ///     Absolute or relative path to the root output directory. The directory
    ///     will be created on first use if it does not already exist. Must not be
    ///     null or whitespace.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="outputDirectory"/> is null, empty, or whitespace.
    /// </exception>
    public FileMarkdownWriterFactory(string outputDirectory)
    {
        // Validate upfront so callers get a clear error rather than an obscure
        // IO exception when the first file is created
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory must not be null or whitespace.", nameof(outputDirectory));
        }

        _outputDirectory = outputDirectory;
    }

    /// <summary>
    ///     Creates a <see cref="FileMarkdownWriter"/> that writes to
    ///     <c>{outputDirectory}/{subFolder}/{name}.md</c>.
    /// </summary>
    /// <param name="subFolder">
    ///     Subfolder path relative to the output root. Pass an empty string or
    ///     whitespace to place the file directly under the output root. Forward
    ///     slashes are acceptable; the implementation normalizes path separators.
    /// </param>
    /// <param name="name">File name without extension. Must not be null or empty.</param>
    /// <returns>
    ///     A new <see cref="FileMarkdownWriter"/> ready for write calls. The caller
    ///     is responsible for disposing the returned writer when finished.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="name"/> is null, empty, or whitespace.
    /// </exception>
    public IMarkdownWriter CreateMarkdown(string subFolder, string name)
    {
        // Validate the file name so IO errors are immediately attributable
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("File name must not be null or whitespace.", nameof(name));
        }

        // Build the target directory: combine the root with the optional subfolder using the
        // safe combiner that rejects rooted or traversal segments; forward slashes in the
        // subfolder are handled natively by Path.Join inside SafePathCombine.
        var targetDirectory = string.IsNullOrWhiteSpace(subFolder)
            ? _outputDirectory
            : PathHelpers.SafePathCombine(_outputDirectory, subFolder);

        // Create the directory tree if it does not already exist so that callers
        // never need to pre-create directories
        Directory.CreateDirectory(targetDirectory);

        // Compose the full file path using the safe combiner, appending the .md extension
        var filePath = PathHelpers.SafePathCombine(targetDirectory, name + ".md");

        // Use UTF-8 without a BOM so generated files are clean for downstream tools
        // and VCS diffs that do not tolerate the byte-order mark
        var streamWriter = new StreamWriter(filePath, append: false, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return new FileMarkdownWriter(streamWriter);
    }
}
