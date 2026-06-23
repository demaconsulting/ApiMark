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
        _tempDirectory = Path.Join(
            Path.GetTempPath(),
            "ApiMarkWriterTests_" + Guid.NewGuid().ToString("N"));
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
        return File.ReadAllText(Path.Join(_tempDirectory, fileName + ".md"));
    }

    /// <summary>
    ///     Verifies that WriteHeading at level 1 produces a single-# ATX heading.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteHeading_Level1_WritesCorrectMarkdown()
    {
        // Arrange / Act: write a level-1 heading and read the result
        var content = WriteAndReadFile("heading1", w => w.WriteHeading(1, "My Heading"));

        // Assert: the content must be exactly the ATX level-1 heading followed by a blank line
        Assert.Equal("# My Heading" + Environment.NewLine + Environment.NewLine, content);
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
        using (var writer = factory.CreateMarkdown("", "flush-test"))
        {
            writer.WriteParagraph("Content to flush.");
        }

        // Act: attempt to open the file for reading — this would fail if the handle
        // were still held by the writer
        var exception = Record.Exception(() =>
        {
            using var fs = File.Open(
                Path.Join(_tempDirectory, "flush-test.md"),
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);

            // Assert: the file must not be empty, confirming content was flushed
            Assert.True(fs.Length > 0, "File must contain content after disposal.");
        });

        // Assert: no exception means the file handle was released successfully
        Assert.Null(exception);
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteHeading"/> throws
    ///     <see cref="ArgumentOutOfRangeException"/> when level is zero.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteHeading_ZeroLevel_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: create a factory and writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "heading-zero");

        // Act / Assert: level 0 is below the minimum of 1
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteHeading(0, "Title"));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteHeading"/> throws
    ///     <see cref="ArgumentOutOfRangeException"/> when level is greater than 6.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteHeading_SevenLevel_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: create a factory and writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "heading-seven");

        // Act / Assert: level 7 exceeds the maximum of 6 defined by CommonMark
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteHeading(7, "Title"));
    }

    /// <summary>
    ///     Verifies that calling <see cref="IMarkdownWriter.WriteHeading"/> after
    ///     <see cref="IMarkdownWriter.Dispose"/> throws <see cref="ObjectDisposedException"/>.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteHeading_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange: create and dispose a writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        var writer = factory.CreateMarkdown("", "disposed-heading");
        writer.Dispose();

        // Act / Assert: calling a write method after disposal must throw
        Assert.Throws<ObjectDisposedException>(() => writer.WriteHeading(1, "Title"));
    }

    /// <summary>
    ///     Verifies that calling <see cref="IMarkdownWriter.WriteSignature"/> after
    ///     <see cref="IMarkdownWriter.Dispose"/> throws <see cref="ObjectDisposedException"/>.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteSignature_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange: create and dispose a writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        var writer = factory.CreateMarkdown("", "disposed-signature");
        writer.Dispose();

        // Act / Assert: calling WriteSignature after disposal must throw
        Assert.Throws<ObjectDisposedException>(() => writer.WriteSignature("csharp", "public void Foo();"));
    }

    /// <summary>
    ///     Verifies that calling <see cref="IMarkdownWriter.WriteParagraph"/> after
    ///     <see cref="IMarkdownWriter.Dispose"/> throws <see cref="ObjectDisposedException"/>.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteParagraph_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange: create and dispose a writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        var writer = factory.CreateMarkdown("", "disposed-paragraph");
        writer.Dispose();

        // Act / Assert: calling WriteParagraph after disposal must throw
        Assert.Throws<ObjectDisposedException>(() => writer.WriteParagraph("Some text."));
    }

    /// <summary>
    ///     Verifies that calling <see cref="IMarkdownWriter.WriteTable"/> after
    ///     <see cref="IMarkdownWriter.Dispose"/> throws <see cref="ObjectDisposedException"/>.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteTable_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange: create and dispose a writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        var writer = factory.CreateMarkdown("", "disposed-table");
        writer.Dispose();
        string[] headers = ["Name", "Type"];
        string[][] rows = [["value", "int"]];

        // Act / Assert: calling WriteTable after disposal must throw
        Assert.Throws<ObjectDisposedException>(() => writer.WriteTable(headers, rows));
    }

    /// <summary>
    ///     Verifies that calling <see cref="IMarkdownWriter.WriteCodeBlock"/> after
    ///     <see cref="IMarkdownWriter.Dispose"/> throws <see cref="ObjectDisposedException"/>.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteCodeBlock_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange: create and dispose a writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        var writer = factory.CreateMarkdown("", "disposed-codeblock");
        writer.Dispose();

        // Act / Assert: calling WriteCodeBlock after disposal must throw
        Assert.Throws<ObjectDisposedException>(() => writer.WriteCodeBlock("csharp", "var x = 1;"));
    }

    /// <summary>
    ///     Verifies that calling <see cref="IMarkdownWriter.WriteLink"/> after
    ///     <see cref="IMarkdownWriter.Dispose"/> throws <see cref="ObjectDisposedException"/>.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteLink_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange: create and dispose a writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        var writer = factory.CreateMarkdown("", "disposed-link");
        writer.Dispose();

        // Act / Assert: calling WriteLink after disposal must throw
        Assert.Throws<ObjectDisposedException>(() => writer.WriteLink("Back", "../api.md"));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteSignature"/> throws
    ///     <see cref="ArgumentNullException"/> when the language parameter is null.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteSignature_NullLanguage_ThrowsArgumentNullException()
    {
        // Arrange: create a factory and writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "sig-null-lang");

        // Act / Assert: null language must be rejected immediately
        Assert.Throws<ArgumentNullException>(() => writer.WriteSignature(null!, "code"));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteSignature"/> throws
    ///     <see cref="ArgumentNullException"/> when the code parameter is null.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteSignature_NullCode_ThrowsArgumentNullException()
    {
        // Arrange: create a factory and writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "sig-null-code");

        // Act / Assert: null code must be rejected immediately
        Assert.Throws<ArgumentNullException>(() => writer.WriteSignature("csharp", null!));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteParagraph"/> throws
    ///     <see cref="ArgumentNullException"/> when the text parameter is null.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteParagraph_NullText_ThrowsArgumentNullException()
    {
        // Arrange: create a factory and writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "para-null-text");

        // Act / Assert: null text must be rejected immediately
        Assert.Throws<ArgumentNullException>(() => writer.WriteParagraph(null!));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteTable"/> throws
    ///     <see cref="ArgumentNullException"/> when the headers parameter is null.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteTable_NullHeaders_ThrowsArgumentNullException()
    {
        // Arrange: create a factory and writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "table-null-headers");

        // Act / Assert: null headers must be rejected immediately
        Assert.Throws<ArgumentNullException>(() => writer.WriteTable(null!, []));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteTable"/> throws
    ///     <see cref="ArgumentNullException"/> when the rows parameter is null.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteTable_NullRows_ThrowsArgumentNullException()
    {
        // Arrange: create a factory and writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "table-null-rows");

        // Act / Assert: null rows must be rejected immediately
        Assert.Throws<ArgumentNullException>(() => writer.WriteTable([], null!));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteCodeBlock"/> throws
    ///     <see cref="ArgumentNullException"/> when the language parameter is null.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteCodeBlock_NullLanguage_ThrowsArgumentNullException()
    {
        // Arrange: create a factory and writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "code-null-lang");

        // Act / Assert: null language must be rejected immediately
        Assert.Throws<ArgumentNullException>(() => writer.WriteCodeBlock(null!, "code"));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteCodeBlock"/> throws
    ///     <see cref="ArgumentNullException"/> when the code parameter is null.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteCodeBlock_NullCode_ThrowsArgumentNullException()
    {
        // Arrange: create a factory and writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "code-null-code");

        // Act / Assert: null code must be rejected immediately
        Assert.Throws<ArgumentNullException>(() => writer.WriteCodeBlock("csharp", null!));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteLink"/> throws
    ///     <see cref="ArgumentNullException"/> when the text parameter is null.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteLink_NullText_ThrowsArgumentNullException()
    {
        // Arrange: create a factory and writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "link-null-text");

        // Act / Assert: null text must be rejected immediately
        Assert.Throws<ArgumentNullException>(() => writer.WriteLink(null!, "api.md"));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteLink"/> throws
    ///     <see cref="ArgumentNullException"/> when the relativePath parameter is null.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteLink_NullRelativePath_ThrowsArgumentNullException()
    {
        // Arrange: create a factory and writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "link-null-path");

        // Act / Assert: null relativePath must be rejected immediately
        Assert.Throws<ArgumentNullException>(() => writer.WriteLink("Back", null!));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteTable"/> throws
    ///     <see cref="ArgumentException"/> when the headers array is empty.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteTable_EmptyHeaders_ThrowsArgumentException()
    {
        // Arrange: create a factory and writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "table-empty-headers");

        // Act / Assert: empty headers must be rejected — a headerless table is not valid Markdown
        Assert.Throws<ArgumentException>(() => writer.WriteTable([], []));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteTable"/> throws
    ///     <see cref="ArgumentException"/> when a row contains a different number of
    ///     cells than the headers array.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteTable_MismatchedRowLength_ThrowsArgumentException()
    {
        // Arrange: create a factory and writer; headers expect 2 columns, row has 3
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "table-mismatched-row");
        string[] headers = ["Name", "Type"];
        string[][] rows = [["value", "int", "extra-cell"]];

        // Act / Assert: a row with a different column count than headers must be rejected
        Assert.Throws<ArgumentException>(() => writer.WriteTable(headers, rows));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteHeading"/> throws
    ///     <see cref="ArgumentException"/> when the text parameter is empty.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteHeading_EmptyText_ThrowsArgumentException()
    {
        // Arrange: create a factory and writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "heading-empty-text");

        // Act / Assert: empty heading text is not valid Markdown and must be rejected
        Assert.Throws<ArgumentException>(() => writer.WriteHeading(1, ""));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteLink"/> throws
    ///     <see cref="ArgumentException"/> when the text parameter is empty.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteLink_EmptyText_ThrowsArgumentException()
    {
        // Arrange: create a factory and writer
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "link-empty-text");

        // Act / Assert: empty link text produces a Markdown link with no visible label and must be rejected
        Assert.Throws<ArgumentException>(() => writer.WriteLink("", "api.md"));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteTable"/> throws
    ///     <see cref="ArgumentException"/> when the rows sequence contains a null element.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteTable_NullRowElement_ThrowsArgumentException()
    {
        // Arrange: create a factory and writer; second element in rows is null
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "table-null-row-element");
        string[] headers = ["Name", "Type"];
        string[][] rows = [["a", "b"], null!, ["c", "d"]];

        // Act / Assert: a null row element in the rows sequence must be rejected
        Assert.Throws<ArgumentException>(() => writer.WriteTable(headers, rows));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteTable"/> throws
    ///     <see cref="ArgumentException"/> when the headers array contains a null element.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteTable_NullHeaderElement_ThrowsArgumentException()
    {
        // Arrange: create a factory and writer; second header is null
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "table-null-header-element");
        string[] headers = ["Name", null!, "Description"];

        // Act / Assert: a null header element must be rejected before any output is written
        Assert.Throws<ArgumentException>(() => writer.WriteTable(headers, []));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteTable"/> throws
    ///     <see cref="ArgumentException"/> when a row in the rows sequence contains a null cell.
    /// </summary>
    [Fact]
    public void FileMarkdownWriter_WriteTable_NullCellElement_ThrowsArgumentException()
    {
        // Arrange: create a factory and writer; a cell within a row is null
        var factory = new FileMarkdownWriterFactory(_tempDirectory);
        using var writer = factory.CreateMarkdown("", "table-null-cell-element");
        string[] headers = ["Name", "Type"];
        string[][] rows = [["value", null!]];

        // Act / Assert: a null cell within a row must be rejected before any output is written
        Assert.Throws<ArgumentException>(() => writer.WriteTable(headers, rows));
    }
}
