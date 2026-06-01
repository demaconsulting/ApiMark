using ApiMark.Core;
using Xunit;

namespace ApiMark.Core.Tests;

/// <summary>
///     Verifies the concrete <see cref="FileMarkdownWriterFactory"/> implementation,
///     confirming that it creates files and directories on the file system correctly.
/// </summary>
public sealed class FileMarkdownWriterFactoryTests : IDisposable
{
    /// <summary>Temporary directory created for this test class instance.</summary>
    private readonly string _tempDirectory;

    /// <summary>
    ///     Creates a unique temporary directory for this test class instance so
    ///     that tests do not interfere with one another.
    /// </summary>
    public FileMarkdownWriterFactoryTests()
    {
        // Use a unique subfolder under the system temp path to avoid collisions
        // between parallel test runs
        _tempDirectory = Path.Join(
            Path.GetTempPath(),
            "ApiMarkTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    ///     Removes the temporary directory and all files created during the test.
    /// </summary>
    public void Dispose()
    {
        // Best-effort cleanup; suppress exceptions so a cleanup failure does not
        // mask the original test failure
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Ignore — cleanup failure should not fail the test run
        }
    }

    /// <summary>
    ///     Verifies that constructing <see cref="FileMarkdownWriterFactory"/> with a
    ///     null output directory throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void FileMarkdownWriterFactory_Constructor_NullDirectory_ThrowsArgumentException()
    {
        // Arrange / Act / Assert: null must be rejected at construction time
        Assert.Throws<ArgumentException>(() => new FileMarkdownWriterFactory(null!));
    }

    /// <summary>
    ///     Verifies that constructing <see cref="FileMarkdownWriterFactory"/> with a
    ///     whitespace-only output directory throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void FileMarkdownWriterFactory_Constructor_WhitespaceDirectory_ThrowsArgumentException()
    {
        // Arrange / Act / Assert: whitespace-only strings must be rejected at construction time
        Assert.Throws<ArgumentException>(() => new FileMarkdownWriterFactory("   "));
    }

    /// <summary>
    ///     Verifies that calling <see cref="FileMarkdownWriterFactory.CreateMarkdown"/>
    ///     with a null file name throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void FileMarkdownWriterFactory_CreateMarkdown_NullName_ThrowsArgumentException()
    {
        // Arrange: create a factory pointing at the temp directory
        var factory = new FileMarkdownWriterFactory(_tempDirectory);

        // Act / Assert: null name must be rejected before any I/O is attempted
        Assert.Throws<ArgumentException>(() => factory.CreateMarkdown("", null!));
    }

    /// <summary>
    ///     Verifies that passing an empty subfolder writes the file directly under
    ///     the output root and that the file exists after the writer is disposed.
    /// </summary>
    [Fact]
    public void FileMarkdownWriterFactory_CreateMarkdown_RootLevel_CreatesFile()
    {
        // Arrange: create a factory pointing at the temp directory
        var factory = new FileMarkdownWriterFactory(_tempDirectory);

        // Act: create a root-level writer, write content, and dispose it
        using (var writer = factory.CreateMarkdown("", "api"))
        {
            writer.WriteHeading(1, "API Reference");
        }

        // Assert: the file must exist at the expected root-level path
        var expectedPath = Path.Join(_tempDirectory, "api.md");
        Assert.True(File.Exists(expectedPath), $"Expected file '{expectedPath}' to exist after dispose.");
    }

    /// <summary>
    ///     Verifies that passing a non-empty subfolder creates the directory and
    ///     writes the file inside it.
    /// </summary>
    [Fact]
    public void FileMarkdownWriterFactory_CreateMarkdown_WithSubFolder_CreatesDirectoryAndFile()
    {
        // Arrange: create a factory pointing at the temp directory
        var factory = new FileMarkdownWriterFactory(_tempDirectory);

        // Act: create a writer in a subfolder
        using (var writer = factory.CreateMarkdown("namespaces", "MyNamespace"))
        {
            writer.WriteParagraph("Namespace documentation.");
        }

        // Assert: both the directory and the file must exist
        var expectedDir = Path.Join(_tempDirectory, "namespaces");
        var expectedFile = Path.Join(expectedDir, "MyNamespace.md");
        Assert.True(Directory.Exists(expectedDir), $"Expected directory '{expectedDir}' to exist.");
        Assert.True(File.Exists(expectedFile), $"Expected file '{expectedFile}' to exist.");
    }

    /// <summary>
    ///     Verifies that the factory creates the output root directory if it does not
    ///     exist at the time of the first <see cref="FileMarkdownWriterFactory.CreateMarkdown"/> call.
    /// </summary>
    [Fact]
    public void FileMarkdownWriterFactory_CreateMarkdown_NonExistentDirectory_CreatesDirectory()
    {
        // Arrange: choose a directory path that does not yet exist
        var nonExistentDir = Path.Join(
            _tempDirectory,
            "new-output-" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(nonExistentDir), "Pre-condition: directory must not exist before the test.");

        var factory = new FileMarkdownWriterFactory(nonExistentDir);

        // Act: create a writer — the factory must create the directory on demand
        using (var writer = factory.CreateMarkdown("", "index"))
        {
            writer.WriteParagraph("Created on demand.");
        }

        // Assert: the directory and file must both exist after the writer is disposed
        Assert.True(Directory.Exists(nonExistentDir), "Factory must create the output directory if it does not exist.");
        Assert.True(File.Exists(Path.Join(nonExistentDir, "index.md")));
    }
}
