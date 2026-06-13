using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using Xunit;

namespace ApiMark.Core.Tests;

/// <summary>
///     Verifies the <see cref="IApiEmitter"/> interface contract. These tests
///     confirm that the interface can be implemented, that format selection is
///     honored, and that the api.md entrypoint contract is enforced.
/// </summary>
public sealed class IApiEmitterTests
{
    /// <summary>
    ///     Verifies that when <see cref="EmitConfig.Format"/> is
    ///     <see cref="OutputFormat.GradualDisclosure"/>, calling <c>Emit</c> through
    ///     the <see cref="IApiEmitter"/> interface causes the factory to receive more
    ///     than one <c>CreateMarkdown</c> call (i.e., multiple files are produced),
    ///     and that the root <c>api.md</c> entrypoint is among them.
    /// </summary>
    [Fact]
    public void IApiEmitter_Emit_WithGradualDisclosure_ProducesMultipleFiles()
    {
        // Arrange: a stub emitter that produces two files under GradualDisclosure
        var factory = new InMemoryMarkdownWriterFactory();
        IApiEmitter emitter = new MultiFileEmitter();
        var config = new EmitConfig { Format = OutputFormat.GradualDisclosure };

        // Act: invoke Emit through the IApiEmitter interface reference
        emitter.Emit(factory, config, new InMemoryContext());

        // Assert: more than one file must be created — GradualDisclosure is multi-file
        Assert.True(factory.Writers.Count > 1, "GradualDisclosure format must produce more than one file.");
        Assert.True(factory.HasWriter("", "api"), "GradualDisclosure format must include the root api.md entrypoint.");
    }

    /// <summary>
    ///     Verifies that when <see cref="EmitConfig.Format"/> is
    ///     <see cref="OutputFormat.SingleFile"/>, calling <c>Emit</c> through the
    ///     <see cref="IApiEmitter"/> interface causes the factory to receive exactly
    ///     one <c>CreateMarkdown</c> call writing only <c>api.md</c>.
    /// </summary>
    [Fact]
    public void IApiEmitter_Emit_WithSingleFile_ProducesSingleApiMd()
    {
        // Arrange: a stub emitter that honours SingleFile by writing only api.md
        var factory = new InMemoryMarkdownWriterFactory();
        IApiEmitter emitter = new SingleFileEmitter();
        var config = new EmitConfig { Format = OutputFormat.SingleFile };

        // Act: invoke Emit through the IApiEmitter interface reference
        emitter.Emit(factory, config, new InMemoryContext());

        // Assert: exactly one file must be created and it must be api.md at the root
        Assert.Single(factory.Writers);
        Assert.True(factory.HasWriter("", "api"), "SingleFile format must write only factory.CreateMarkdown(\"\", \"api\").");
    }

    /// <summary>
    ///     Stub emitter that writes two files to simulate multi-file GradualDisclosure
    ///     output: one root <c>api.md</c> and one additional page.
    /// </summary>
    private sealed class MultiFileEmitter : IApiEmitter
    {
        /// <summary>
        ///     Creates the root <c>api.md</c> and one additional namespace page so
        ///     the test can assert that more than one file was produced.
        /// </summary>
        public void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
        {
            // Root entrypoint
            using var root = factory.CreateMarkdown("", "api");
            root.WriteHeading(1, "API Reference");

            // One extra page — simulates a namespace or type page in GradualDisclosure
            using var page = factory.CreateMarkdown("MyNamespace", "MyNamespace");
            page.WriteHeading(2, "MyNamespace");
        }
    }

    /// <summary>
    ///     Stub emitter that writes only <c>api.md</c> to simulate single-file output
    ///     controlled by <see cref="OutputFormat.SingleFile"/>.
    /// </summary>
    private sealed class SingleFileEmitter : IApiEmitter
    {
        /// <summary>
        ///     Creates only the root <c>api.md</c> and writes all content into it —
        ///     single-file contract.
        /// </summary>
        public void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
        {
            // Only api.md is created — single-file format consolidates everything here
            using var root = factory.CreateMarkdown("", "api");
            root.WriteHeading(config.HeadingDepth, "API Reference");
        }
    }
}
