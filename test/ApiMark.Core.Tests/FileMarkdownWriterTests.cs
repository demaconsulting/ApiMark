using ApiMark.Core;
using Xunit;

namespace ApiMark.Core.Tests;

/// <summary>
///     Verifies the output content written by <see cref="FileMarkdownWriter"/> to
///     the file system, confirming that each write method produces correct Markdown syntax.
/// </summary>
public sealed class FileMarkdownWriterTests : IDisposable
{
    /// <summary>Temporary directory created for this test class instance.</summary>
    private readonly string _tempDirectory;

    /// <summary>
    ///     Creates a unique temporary directory for this test class instance so
    ///     that file-content tests do not interfere with one another.
    /// </summary>
    public FileMarkdownWriterTests()
    {
        // Unique subfolder prevents collisions between parallel test runs
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ApiMarkWriterTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    /// <summary>
    ///     Removes the temporary directory and all files created during the test.
    /// </summary>
    public void Dispose()
    {
        // Best-effort cleanup; suppress exceptions so a cleanup failure does not
        // mask the original test result
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Ignore
        }
    }

    /// <summary>
    ///     Helper: creates a factory targeting the temp directory, creates a writer
    ///     for the given file name, invokes the write action, disposes the writer,
    ///     and returns the file content.
    /// </summary>
    /// <param name="fileName">Name (without extension) of the output file.</param>
    /// <param name="writeAction">Action that performs write calls on the writer.</param>
    /// <returns>The full text content of the written file.</returns>
    private string WriteAndReadFile(string fileName, Action<IMarkdownWriter> writeAction)
    {
        // Create a factory and writer, perform writes, then dispose to flush
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using (var writer = factory.CreateMarkdown("", fileName))
        {
            writeAction(writer);
        }

        // Read the file content back for assertion
        return File.ReadAllText(Path.Combine(_tempDirectory, fileName + ".md"));
    }

    /// <summary>
    ///     Verifies that WriteHeading at level 1 produces a single-# ATX heading.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteHeading_Level1_WritesCorrectMarkdown()
    {
        // Arrange / Act: write a level-1 heading and read the result
        var content = WriteAndReadFile("heading1", w => w.WriteHeading(1, "My Heading"));

        // Assert: the content must contain the ATX level-1 heading syntax
        Assert.Contains("# My Heading", content);
    }

    /// <summary>
    ///     Verifies that WriteHeading at level 3 produces a triple-# ATX heading.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteHeading_Level3_WritesCorrectMarkdown()
    {
        // Arrange / Act: write a level-3 heading and read the result
        var content = WriteAndReadFile("heading3", w => w.WriteHeading(3, "My Heading"));

        // Assert: the content must contain the ATX level-3 heading syntax
        Assert.Contains("### My Heading", content);
    }

    /// <summary>
    ///     Verifies that WriteSignature produces a fenced code block with the
    ///     correct language tag.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteSignature_ValidArgs_WritesCodeFence()
    {
        // Arrange / Act: write a signature and read the result
        var content = WriteAndReadFile("signature", w =>
            w.WriteSignature("csharp", "public void DoWork();"));

        // Assert: the content must contain the opening fence with the language tag
        // and the signature code
        Assert.Contains("```csharp", content);
        Assert.Contains("public void DoWork();", content);
        Assert.Contains("```", content);
    }

    /// <summary>
    ///     Verifies that WriteParagraph writes the paragraph text to the file.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteParagraph_ValidText_WritesParagraphText()
    {
        // Arrange / Act: write a paragraph and read the result
        var content = WriteAndReadFile("paragraph", w =>
            w.WriteParagraph("This is a documentation paragraph."));

        // Assert: the paragraph text must appear in the file content
        Assert.Contains("This is a documentation paragraph.", content);
    }

    /// <summary>
    ///     Verifies that WriteTable produces a pipe-delimited GFM table with a
    ///     separator row.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteTable_ValidArgs_WritesPipeTable()
    {
        // Arrange: prepare headers and rows
        string[] headers = ["Name", "Type", "Description"];
        string[][] rows = [["value", "int", "The input."], ["result", "string", "The output."]];

        // Act: write the table and read the file
        var content = WriteAndReadFile("table", w => w.WriteTable(headers, rows));

        // Assert: the content must contain pipe-delimited header and separator rows
        Assert.Contains("| Name | Type | Description |", content);
        Assert.Contains("| --- | --- | --- |", content);
        Assert.Contains("| value | int | The input. |", content);
        Assert.Contains("| result | string | The output. |", content);
    }

    /// <summary>
    ///     Verifies that WriteCodeBlock produces a fenced code block with the
    ///     correct language tag.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteCodeBlock_ValidArgs_WritesCodeFence()
    {
        // Arrange / Act: write a code block and read the result
        var content = WriteAndReadFile("codeblock", w =>
            w.WriteCodeBlock("csharp", "var x = Compute(42);"));

        // Assert: the content must contain the opening fence with the language tag
        Assert.Contains("```csharp", content);
        Assert.Contains("var x = Compute(42);", content);
    }

    /// <summary>
    ///     Verifies that WriteLink produces a standard inline Markdown link with the
    ///     correct text and path.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteLink_ValidArgs_WritesMarkdownLink()
    {
        // Arrange / Act: write a link and read the result
        var content = WriteAndReadFile("link", w =>
            w.WriteLink("Back to Index", "../api.md"));

        // Assert: the content must contain the [text](path) Markdown link syntax
        Assert.Contains("[Back to Index](../api.md)", content);
    }

    /// <summary>
    ///     Verifies that after <see cref="IMarkdownWriter.Dispose"/> is called the
    ///     file handle is released and the file can be opened for reading by another caller.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_Dispose_AfterWrite_FlushesAndClosesFile()
    {
        // Arrange: create and dispose a writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        var writer = factory.CreateMarkdown("", "flush-test");
        writer.WriteParagraph("Content to flush.");
        writer.Dispose();

        // Act: attempt to open the file for reading — this would fail if the handle
        // were still held by the writer
        var exception = Record.Exception(() =>
        {
            using var fs = File.Open(
                Path.Combine(_tempDirectory, "flush-test.md"),
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);

            // Assert: the file must not be empty, confirming content was flushed
            Assert.True(fs.Length > 0, "File must contain content after disposal.");
        });

        // Assert: no exception means the file handle was released successfully
        Assert.Null(exception);
    }
}
