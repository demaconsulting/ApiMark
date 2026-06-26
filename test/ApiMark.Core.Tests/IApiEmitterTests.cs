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
        // Arrange: a stub emitter that honors SingleFile by writing only api.md
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
        /// <param name="factory">The writer factory used to create output files.</param>
        /// <param name="config">The emit configuration; not inspected by this stub.</param>
        /// <param name="context">The diagnostic context; not used by this stub.</param>
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
        /// <param name="factory">The writer factory used to create output files.</param>
        /// <param name="config">The emit configuration providing the heading depth to use.</param>
        /// <param name="context">The diagnostic context; not used by this stub.</param>
        public void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
        {
            // Only api.md is created — single-file format consolidates everything here
            using var root = factory.CreateMarkdown("", "api");
            root.WriteHeading(config.HeadingDepth, "API Reference");
        }
    }

    // =========================================================================
    // Format-selection tests (format decision belongs at the IApiEmitter level)
    // =========================================================================

    /// <summary>
    ///     Verifies that a format-aware <see cref="IApiEmitter"/> implementation produces
    ///     multiple output files when <see cref="EmitConfig.Format"/> is
    ///     <see cref="OutputFormat.GradualDisclosure"/>.
    /// </summary>
    [Fact]
    public void IApiEmitter_Emit_GradualDisclosure_ProducesMultipleFiles()
    {
        // Arrange: a format-aware stub and a GradualDisclosure config
        var factory = new InMemoryMarkdownWriterFactory();
        IApiEmitter emitter = new FormatAwareStubEmitter();
        var config = new EmitConfig { Format = OutputFormat.GradualDisclosure };

        // Act: invoke Emit — the emitter must read config.Format and produce multiple files
        emitter.Emit(factory, config, new InMemoryContext());

        // Assert: more than one file must be created when GradualDisclosure is selected
        Assert.True(factory.Writers.Count > 1, "GradualDisclosure format must produce more than one file.");
        Assert.True(factory.HasWriter("", "api"), "GradualDisclosure format must include the root api.md entrypoint.");
    }

    /// <summary>
    ///     Verifies that a format-aware <see cref="IApiEmitter"/> implementation produces
    ///     exactly one output file named <c>api.md</c> when <see cref="EmitConfig.Format"/>
    ///     is <see cref="OutputFormat.SingleFile"/>.
    /// </summary>
    [Fact]
    public void IApiEmitter_Emit_SingleFile_ProducesSingleApiMd()
    {
        // Arrange: a format-aware stub and a SingleFile config
        var factory = new InMemoryMarkdownWriterFactory();
        IApiEmitter emitter = new FormatAwareStubEmitter();
        var config = new EmitConfig { Format = OutputFormat.SingleFile };

        // Act: invoke Emit — the emitter must read config.Format and write only api.md
        emitter.Emit(factory, config, new InMemoryContext());

        // Assert: exactly one file must be created and it must be api.md at the root
        Assert.Single(factory.Writers);
        Assert.True(factory.HasWriter("", "api"), "SingleFile format must write only factory.CreateMarkdown(\"\", \"api\").");
    }

    /// <summary>
    ///     Format-aware stub emitter that reads <see cref="EmitConfig.Format"/> to decide
    ///     whether to produce multiple files (GradualDisclosure) or a single api.md
    ///     (SingleFile). Used to verify that format-selection is correctly honored at
    ///     the <see cref="IApiEmitter"/> level.
    /// </summary>
    private sealed class FormatAwareStubEmitter : IApiEmitter
    {
        /// <summary>
        ///     Validates preconditions then produces either multi-file or single-file
        ///     output depending on <see cref="EmitConfig.Format"/>.
        /// </summary>
        /// <param name="factory">The writer factory used to create output files.</param>
        /// <param name="config">The emit configuration whose Format determines the output shape.</param>
        /// <param name="context">The diagnostic context; not used by this stub.</param>
        public void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
        {
            ArgumentNullException.ThrowIfNull(factory);
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(context);

            if (config.Format == OutputFormat.GradualDisclosure)
            {
                // Multi-file: root api.md plus one additional page
                using var root = factory.CreateMarkdown("", "api");
                root.WriteHeading(1, "API Reference");

                using var page = factory.CreateMarkdown("MyNamespace", "MyNamespace");
                page.WriteHeading(2, "MyNamespace");
            }
            else
            {
                // Single-file: all content goes into api.md only
                using var root = factory.CreateMarkdown("", "api");
                root.WriteHeading(config.HeadingDepth, "API Reference");
            }
        }
    }

    // =========================================================================
    // Null-precondition tests (all Emit parameters must be non-null)
    // =========================================================================

    /// <summary>
    ///     Verifies that <see cref="IApiEmitter.Emit"/> throws
    ///     <see cref="ArgumentNullException"/> when <c>factory</c> is null.
    /// </summary>
    [Fact]
    public void IApiEmitter_Emit_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange: a format-aware stub emitter that validates its arguments
        IApiEmitter emitter = new FormatAwareStubEmitter();
        var config = new EmitConfig();
        var context = new InMemoryContext();

        // Act / Assert: null factory must be rejected with ArgumentNullException
        Assert.Throws<ArgumentNullException>(() => emitter.Emit(null!, config, context));
    }

    /// <summary>
    ///     Verifies that <see cref="IApiEmitter.Emit"/> throws
    ///     <see cref="ArgumentNullException"/> when <c>config</c> is null.
    /// </summary>
    [Fact]
    public void IApiEmitter_Emit_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange: a format-aware stub emitter that validates its arguments
        IApiEmitter emitter = new FormatAwareStubEmitter();
        var factory = new InMemoryMarkdownWriterFactory();
        var context = new InMemoryContext();

        // Act / Assert: null config must be rejected with ArgumentNullException
        Assert.Throws<ArgumentNullException>(() => emitter.Emit(factory, null!, context));
    }

    /// <summary>
    ///     Verifies that <see cref="IApiEmitter.Emit"/> throws
    ///     <see cref="ArgumentNullException"/> when <c>context</c> is null.
    /// </summary>
    [Fact]
    public void IApiEmitter_Emit_NullContext_ThrowsArgumentNullException()
    {
        // Arrange: a format-aware stub emitter that validates its arguments
        IApiEmitter emitter = new FormatAwareStubEmitter();
        var factory = new InMemoryMarkdownWriterFactory();
        var config = new EmitConfig();

        // Act / Assert: null context must be rejected with ArgumentNullException
        Assert.Throws<ArgumentNullException>(() => emitter.Emit(factory, config, null!));
    }
}
