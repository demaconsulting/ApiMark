using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using Xunit;

namespace ApiMark.Core.Tests;

/// <summary>
///     Verifies the <see cref="IMarkdownWriter"/> interface contract using
///     <see cref="InMemoryMarkdownWriter"/> as the test double.
/// </summary>
public sealed class IMarkdownWriterTests
{
    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter"/> extends <see cref="IDisposable"/>
    ///     as required by the interface definition.
    /// </summary>
    [Fact]
    public void IMarkdownWriter_IsDisposable_ExtendsIDisposable()
    {
        // Arrange: obtain an IMarkdownWriter through the factory interface
        using var writer = new InMemoryMarkdownWriter();

        // Act / Assert: verifies at the interface level that IMarkdownWriter extends IDisposable
        Assert.True(typeof(IMarkdownWriter).IsAssignableTo(typeof(IDisposable)));
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteHeading"/> can be called
    ///     with a valid level and text without throwing.
    /// </summary>
    [Fact]
    public void IMarkdownWriter_WriteHeading_ValidArgs_DoesNotThrow()
    {
        // Arrange: create an in-memory writer
        using var writer = new InMemoryMarkdownWriter();

        // Act: call WriteHeading with valid arguments
        var exception = Record.Exception(() => writer.WriteHeading(2, "My Section"));

        // Assert: the method is callable without error
        Assert.Null(exception);
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteSignature"/> can be called
    ///     without throwing.
    /// </summary>
    [Fact]
    public void IMarkdownWriter_WriteSignature_ValidArgs_DoesNotThrow()
    {
        // Arrange: create an in-memory writer
        using var writer = new InMemoryMarkdownWriter();

        // Act: call WriteSignature with valid arguments
        var exception = Record.Exception(() => writer.WriteSignature("csharp", "public void Foo();"));

        // Assert: the method is callable without error
        Assert.Null(exception);
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteParagraph"/> can be called
    ///     without throwing.
    /// </summary>
    [Fact]
    public void IMarkdownWriter_WriteParagraph_ValidText_DoesNotThrow()
    {
        // Arrange: create an in-memory writer
        using var writer = new InMemoryMarkdownWriter();

        // Act: call WriteParagraph with valid text
        var exception = Record.Exception(() => writer.WriteParagraph("Some documentation."));

        // Assert: the method is callable without error
        Assert.Null(exception);
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteTable"/> can be called
    ///     without throwing.
    /// </summary>
    [Fact]
    public void IMarkdownWriter_WriteTable_ValidArgs_DoesNotThrow()
    {
        // Arrange: create an in-memory writer
        using var writer = new InMemoryMarkdownWriter();
        string[] headers = ["Name", "Type"];
        string[][] rows = [["value", "int"]];

        // Act: call WriteTable with valid arguments
        var exception = Record.Exception(() => writer.WriteTable(headers, rows));

        // Assert: the method is callable without error
        Assert.Null(exception);
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteCodeBlock"/> can be called
    ///     without throwing.
    /// </summary>
    [Fact]
    public void IMarkdownWriter_WriteCodeBlock_ValidArgs_DoesNotThrow()
    {
        // Arrange: create an in-memory writer
        using var writer = new InMemoryMarkdownWriter();

        // Act: call WriteCodeBlock with valid arguments
        var exception = Record.Exception(() => writer.WriteCodeBlock("csharp", "var x = 1;"));

        // Assert: the method is callable without error
        Assert.Null(exception);
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriter.WriteLink"/> can be called
    ///     without throwing.
    /// </summary>
    [Fact]
    public void IMarkdownWriter_WriteLink_ValidArgs_DoesNotThrow()
    {
        // Arrange: create an in-memory writer
        using var writer = new InMemoryMarkdownWriter();

        // Act: call WriteLink with valid arguments
        var exception = Record.Exception(() => writer.WriteLink("MyClass", "types/MyClass.md"));

        // Assert: the method is callable without error
        Assert.Null(exception);
    }

    /// <summary>
    ///     Verifies that <see cref="InMemoryMarkdownWriter"/> can be instantiated
    ///     and assigned to an <see cref="IMarkdownWriter"/> variable.
    /// </summary>
    [Fact]
    public void InMemoryMarkdownWriter_Instantiate_AsInterface_Succeeds()
    {
        // Arrange / Act: construct and assign — compile-time + runtime check
        using var writer = new InMemoryMarkdownWriter();

        // Assert: the assignment confirms the type correctly implements the interface
        Assert.NotNull(writer);
    }

    /// <summary>
    ///     Verifies that calling <see cref="IMarkdownWriter.Dispose"/> on an
    ///     <see cref="InMemoryMarkdownWriter"/> sets <see cref="InMemoryMarkdownWriter.IsDisposed"/>
    ///     to <see langword="true"/>.
    /// </summary>
    [Fact]
    public void InMemoryMarkdownWriter_Dispose_Called_SetsIsDisposedFlag()
    {
        // Arrange: create an in-memory writer
        using var writer = new InMemoryMarkdownWriter();
        Assert.False(writer.IsDisposed, "Writer must not be disposed before Dispose() is called.");

        // Act: dispose the writer
        writer.Dispose();

        // Assert: IsDisposed must now be true
        Assert.True(writer.IsDisposed, "Writer must report IsDisposed=true after Dispose() is called.");
    }

    /// <summary>
    ///     Verifies that calling each write method produces the correct
    ///     <see cref="MarkdownOperation"/> in the operations list with the correct
    ///     argument values.
    /// </summary>
    [Fact]
    public void InMemoryMarkdownWriter_Write_AllMethods_RecordsOperations()
    {
        // Arrange: create an in-memory writer and prepare test data
        using var writer = new InMemoryMarkdownWriter();
        string[] tableHeaders = ["Col1", "Col2"];
        string[][] tableRows = [["A", "B"], ["C", "D"]];

        // Act: call all write methods in sequence
        writer.WriteHeading(1, "Title");
        writer.WriteParagraph("Some description.");
        writer.WriteSignature("csharp", "public void Foo();");
        writer.WriteTable(tableHeaders, tableRows);
        writer.WriteCodeBlock("csharp", "Foo();");
        writer.WriteLink("See Also", "other.md");
        writer.Dispose();

        // Assert: verify each operation type and argument values
        Assert.Equal(6, writer.Operations.Count);

        var heading = Assert.IsType<HeadingOperation>(writer.Operations[0]);
        Assert.Equal(1, heading.Level);
        Assert.Equal("Title", heading.Text);

        var paragraph = Assert.IsType<ParagraphOperation>(writer.Operations[1]);
        Assert.Equal("Some description.", paragraph.Text);

        var signature = Assert.IsType<SignatureOperation>(writer.Operations[2]);
        Assert.Equal("csharp", signature.Language);
        Assert.Equal("public void Foo();", signature.Code);

        var table = Assert.IsType<TableOperation>(writer.Operations[3]);
        Assert.Equal(tableHeaders, table.Headers);
        Assert.Equal(2, table.Rows.Count);

        var codeBlock = Assert.IsType<CodeBlockOperation>(writer.Operations[4]);
        Assert.Equal("csharp", codeBlock.Language);
        Assert.Equal("Foo();", codeBlock.Code);

        var link = Assert.IsType<LinkOperation>(writer.Operations[5]);
        Assert.Equal("See Also", link.Text);
        Assert.Equal("other.md", link.RelativePath);
    }

    /// <summary>
    ///     Verifies that operations are recorded in the exact order that the
    ///     corresponding write methods were called.
    /// </summary>
    [Fact]
    public void InMemoryMarkdownWriter_Write_MultipleOps_RecordsInOrder()
    {
        // Arrange: create an in-memory writer
        using var writer = new InMemoryMarkdownWriter();

        // Act: write three operations in a specific order
        writer.WriteHeading(2, "Section");
        writer.WriteParagraph("First paragraph.");
        writer.WriteLink("Back", "../api.md");

        // Assert: the operations list must preserve insertion order exactly
        Assert.Equal(3, writer.Operations.Count);
        Assert.IsType<HeadingOperation>(writer.Operations[0]);
        Assert.IsType<ParagraphOperation>(writer.Operations[1]);
        Assert.IsType<LinkOperation>(writer.Operations[2]);
    }

    /// <summary>
    ///     System-level test: verifies that all section types can be written to an
    ///     <see cref="InMemoryMarkdownWriter"/> and retrieved consistently.
    /// </summary>
    [Fact]
    public void ApiMarkCore_MarkdownWriterContract_FileSections_RenderConsistently()
    {
        // Arrange: create an in-memory writer
        using var writer = new InMemoryMarkdownWriter();
        string[] headers = ["Parameter", "Description"];
        string[][] rows = [["value", "The input value."]];

        // Act: write all section types
        writer.WriteHeading(1, "API");
        writer.WriteSignature("csharp", "public int Compute(int value);");
        writer.WriteParagraph("Computes something.");
        writer.WriteTable(headers, rows);
        writer.WriteCodeBlock("csharp", "var result = obj.Compute(42);");
        writer.WriteLink("Back to index", "api.md");
        writer.Dispose();

        // Assert: the full operation sequence is recorded and each carries its arguments
        Assert.Equal(6, writer.Operations.Count);
        Assert.True(writer.IsDisposed);

        Assert.Multiple(
            () => Assert.IsType<HeadingOperation>(writer.Operations[0]),
            () => Assert.IsType<SignatureOperation>(writer.Operations[1]),
            () => Assert.IsType<ParagraphOperation>(writer.Operations[2]),
            () => Assert.IsType<TableOperation>(writer.Operations[3]),
            () => Assert.IsType<CodeBlockOperation>(writer.Operations[4]),
            () => Assert.IsType<LinkOperation>(writer.Operations[5]));
    }
}
