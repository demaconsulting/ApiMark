using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using Xunit;

namespace ApiMark.Core.Tests;

/// <summary>
///     Verifies the <see cref="IMarkdownWriterFactory"/> interface contract using
///     <see cref="InMemoryMarkdownWriterFactory"/> as the test double.
/// </summary>
public sealed class IMarkdownWriterFactoryTests
{
    /// <summary>
    ///     Verifies the system-level contract that <see cref="IMarkdownWriterFactory"/> can produce
    ///     writers at both the root level (empty subFolder) and within a named subfolder, satisfying
    ///     the decoupling contract needed by language generators.
    /// </summary>
    [Fact]
    public void ApiMarkCore_WriterFactory_CanCreate_RootAndSubfolderWriters()
    {
        // Arrange: obtain the factory via the interface type to exercise the contract
        var factory = new InMemoryMarkdownWriterFactory();

        // Act: create a root-level writer and a subfolder writer
        using var rootWriter = factory.CreateMarkdown("", "api");
        using var subWriter = factory.CreateMarkdown("types", "MyType");

        // Assert: both writers must be non-null and independently usable
        Assert.NotNull(rootWriter);
        Assert.NotNull(subWriter);
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriterFactory.CreateMarkdown"/> returns
    ///     a non-null <see cref="IMarkdownWriter"/> when given valid arguments.
    /// </summary>
    [Fact]
    public void IMarkdownWriterFactory_HasCreateMarkdown_Method()
    {
        // Arrange: create an in-memory factory as the interface implementation under test
        var factory = new InMemoryMarkdownWriterFactory();

        // Act: invoke CreateMarkdown and capture the result
        using var writer = factory.CreateMarkdown("", "test-file");

        // Assert: a non-null writer must be returned for the interface contract to hold
        Assert.NotNull(writer);
    }

    /// <summary>
    ///     Verifies that passing an empty string as the subfolder produces a writer
    ///     accessible at the root level — i.e. no subfolder prefix is prepended to the key.
    /// </summary>
    [Fact]
    public void IMarkdownWriterFactory_CreateMarkdown_EmptySubFolder_IsRootLevel()
    {
        // Arrange: create an in-memory factory
        var factory = new InMemoryMarkdownWriterFactory();

        // Act: create a root-level writer by passing empty subfolder
        using var writer = factory.CreateMarkdown("", "index");

        // Assert: the factory must report a writer at the root-level path
        Assert.True(factory.HasWriter("", "index"), "Writer created with empty subFolder must be accessible at root level.");
    }

    /// <summary>
    ///     Verifies that <see cref="InMemoryMarkdownWriterFactory"/> compiles and can
    ///     be assigned to an <see cref="IMarkdownWriterFactory"/> variable.
    /// </summary>
    [Fact]
    public void InMemoryMarkdownWriterFactory_Constructor_Default_ImplementsInterface()
    {
        // Arrange / Act: construct and assign — this is a compile-time + runtime check
        IMarkdownWriterFactory factory = new InMemoryMarkdownWriterFactory();

        // Assert: the assignment succeeds (confirms the type implements the interface)
        Assert.NotNull(factory);
    }

    /// <summary>
    ///     Verifies that <see cref="IMarkdownWriterFactory.CreateMarkdown"/> returns
    ///     a usable writer when a non-empty subfolder is provided.
    /// </summary>
    [Fact]
    public void InMemoryMarkdownWriterFactory_CreateMarkdown_ValidArgs_ReturnsNonNullWriter()
    {
        // Arrange: create the in-memory factory
        var factory = new InMemoryMarkdownWriterFactory();

        // Act: create a writer in a non-empty subfolder
        using var writer = factory.CreateMarkdown("types", "MyClass");

        // Assert: the returned writer must be non-null and usable
        Assert.NotNull(writer);
        writer.WriteParagraph("Hello from subfolder.");
    }

    /// <summary>
    ///     Verifies that both a root-level writer and a subfolder writer can be created
    ///     in a single factory session and are each retrievable by their expected keys.
    /// </summary>
    [Fact]
    public void InMemoryMarkdownWriterFactory_CreateMarkdown_MultipleFiles_AllRegistered()
    {
        // Arrange: create an in-memory factory
        var factory = new InMemoryMarkdownWriterFactory();

        // Act: create one root-level writer and one subfolder writer
        using var rootWriter = factory.CreateMarkdown("", "api");
        using var subWriter = factory.CreateMarkdown("namespaces", "MyNamespace");

        // Assert: both writers must be retrievable from the factory
        Assert.True(factory.HasWriter("", "api"), "Root-level writer must be registered.");
        Assert.True(factory.HasWriter("namespaces", "MyNamespace"), "Subfolder writer must be registered.");
        Assert.Equal(2, factory.Writers.Count);
    }
}
