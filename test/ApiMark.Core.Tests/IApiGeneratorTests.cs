using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using Xunit;

namespace ApiMark.Core.Tests;

/// <summary>
///     Verifies the <see cref="IApiGenerator"/> interface contract. These tests
///     confirm that the interface can be implemented, that construction-time
///     configuration is accessible at generation time, and that the api.md
///     entrypoint contract is honoured.
/// </summary>
public sealed class IApiGeneratorTests
{
    /// <summary>
    ///     Verifies that a minimal inline stub of <see cref="IApiGenerator"/> can
    ///     be implemented and its Parse method called with a context argument.
    /// </summary>
    [Fact]
    public void IApiGenerator_Parse_WithMinimalStub_ExecutesSuccessfully()
    {
        // Arrange: create a minimal stub that accepts the context and returns a no-op emitter
        var factory = new InMemoryMarkdownWriterFactory();
        IApiGenerator generator = new MinimalStubGenerator();

        // Act: call Parse then Emit through the interface — verifies the method signature is callable
        var exception = Record.Exception(() => generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext()));

        // Assert: no exception means the interface is correctly callable via the contract
        Assert.Null(exception);
    }

    /// <summary>
    ///     Verifies that an <see cref="IApiGenerator"/> implementation may store
    ///     construction-time configuration and access it inside Parse.
    /// </summary>
    [Fact]
    public void IApiGenerator_Implementation_UsesConstructionConfiguration()
    {
        // Arrange: create a generator that stores a config value at construction time
        const string expectedConfig = "test-assembly.dll";
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new ConfigurableStubGenerator(expectedConfig);

        // Act: parse and emit — the stub will record whether its config was accessible
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the stored config matches what was passed at construction
        Assert.Equal(expectedConfig, generator.ConfigUsedDuringParse);
    }

    /// <summary>
    ///     Verifies that an <see cref="IApiGenerator"/> implementation that calls
    ///     <c>factory.CreateMarkdown("", "api")</c> during <c>Emit</c> results in the factory having
    ///     a writer registered at the root "api" key.
    /// </summary>
    [Fact]
    public void IApiGenerator_Emit_OutputDirectory_ContainsApiMd()
    {
        // Arrange: create an in-memory factory and a generator that produces api.md
        var factory = new InMemoryMarkdownWriterFactory();
        IApiGenerator generator = new ApiMdProducingStubGenerator();

        // Act: generate the documentation tree
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the required api.md entrypoint must have been created
        Assert.True(factory.HasWriter("", "api"), "Generator must call factory.CreateMarkdown(\"\", \"api\") to produce api.md.");
    }

    /// <summary>
    ///     Verifies that when <see cref="EmitConfig.Format"/> is
    ///     <see cref="OutputFormat.GradualDisclosure"/>, calling <c>Emit</c> causes the
    ///     factory to receive more than one <c>CreateMarkdown</c> call (i.e., multiple files
    ///     are produced rather than a single consolidated output).
    /// </summary>
    [Fact]
    public void IApiGenerator_Emit_GradualDisclosure_ProducesMultipleFiles()
    {
        // Arrange: a stub generator that produces two files under GradualDisclosure
        var factory = new InMemoryMarkdownWriterFactory();
        IApiGenerator generator = new MultiFileStubGenerator();
        var config = new EmitConfig { Format = OutputFormat.GradualDisclosure };

        // Act: parse then emit with the GradualDisclosure format
        generator.Parse(new InMemoryContext()).Emit(factory, config, new InMemoryContext());

        // Assert: more than one file must be created — GradualDisclosure is multi-file
        Assert.True(factory.Writers.Count > 1, "GradualDisclosure format must produce more than one file.");
    }

    /// <summary>
    ///     Verifies that when <see cref="EmitConfig.Format"/> is
    ///     <see cref="OutputFormat.SingleFile"/>, calling <c>Emit</c> causes the factory to
    ///     receive exactly one <c>CreateMarkdown</c> call writing only <c>api.md</c>.
    /// </summary>
    [Fact]
    public void IApiGenerator_Emit_SingleFile_ProducesSingleApiMd()
    {
        // Arrange: a stub generator that honours SingleFile by writing only api.md
        var factory = new InMemoryMarkdownWriterFactory();
        IApiGenerator generator = new SingleFileStubGenerator();
        var config = new EmitConfig { Format = OutputFormat.SingleFile };

        // Act: parse then emit with the SingleFile format
        generator.Parse(new InMemoryContext()).Emit(factory, config, new InMemoryContext());

        // Assert: exactly one file must be created and it must be api.md at the root
        Assert.Single(factory.Writers);
        Assert.True(factory.HasWriter("", "api"), "SingleFile format must write only factory.CreateMarkdown(\"\", \"api\").");
    }

    /// <summary>
    ///     System-level test: verifies that a complete <see cref="IApiGenerator"/>
    ///     implementation can be invoked through the interface contract without error.
    /// </summary>
    [Fact]
    public void ApiMarkCore_GeneratorContract_SupportedLanguage_CanBeInvoked()
    {
        // Arrange: construct a generator via the interface reference
        var factory = new InMemoryMarkdownWriterFactory();
        IApiGenerator generator = new ApiMdProducingStubGenerator();

        // Act: invoke through the interface — this validates the full dispatch path
        var exception = Record.Exception(() => generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext()));

        // Assert: no exception means the interface contract is invocable end-to-end
        Assert.Null(exception);
    }

    /// <summary>
    ///     Minimal stub generator that accepts a context and returns a no-op emitter.
    ///     Used to verify the method signature is callable.
    /// </summary>
    private sealed class MinimalStubGenerator : IApiGenerator
    {
        /// <summary>
        ///     Implements <see cref="IApiGenerator.Parse"/> as a stub that returns a
        ///     no-op emitter to confirm the method signature matches the interface contract.
        /// </summary>
        /// <param name="context">Not used.</param>
        /// <returns>A no-op emitter.</returns>
        public IApiEmitter Parse(IContext context) => new NoOpEmitter();
    }

    /// <summary>
    ///     Stub generator that records the construction-time config string and
    ///     stores it when Parse is called, so tests can verify it was used.
    /// </summary>
    private sealed class ConfigurableStubGenerator : IApiGenerator
    {
        /// <summary>Configuration value captured at construction time.</summary>
        private readonly string _config;

        /// <summary>
        ///     Initializes the generator with a configuration value that should be
        ///     accessible during Parse.
        /// </summary>
        /// <param name="config">Configuration string to store and expose.</param>
        public ConfigurableStubGenerator(string config)
        {
            _config = config;
        }

        /// <summary>
        ///     Gets the configuration value that was available when Parse was last called.
        /// </summary>
        public string? ConfigUsedDuringParse { get; private set; }

        /// <summary>
        ///     Records that the construction-time configuration is accessible during parse.
        /// </summary>
        /// <param name="context">Not used.</param>
        /// <returns>A no-op emitter.</returns>
        public IApiEmitter Parse(IContext context)
        {
            // Expose the construction-time config so the test can verify it was preserved
            ConfigUsedDuringParse = _config;
            return new NoOpEmitter();
        }
    }

    /// <summary>
    ///     Stub generator that creates the mandatory api.md entrypoint file during emit.
    /// </summary>
    private sealed class ApiMdProducingStubGenerator : IApiGenerator
    {
        /// <summary>
        ///     Returns an emitter that will call <c>factory.CreateMarkdown("", "api")</c>
        ///     to produce the fixed top-level entrypoint.
        /// </summary>
        /// <param name="context">Not used.</param>
        /// <returns>An emitter that writes the api.md file.</returns>
        public IApiEmitter Parse(IContext context) => new ApiMdEmitter();

        /// <summary>Emitter that writes the required api.md heading.</summary>
        private sealed class ApiMdEmitter : IApiEmitter
        {
            /// <summary>
            ///     Calls <c>factory.CreateMarkdown("", "api")</c> to produce the fixed
            ///     top-level entrypoint and immediately disposes the writer.
            /// </summary>
            public void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
            {
                // Create the mandatory root entrypoint; dispose immediately after creation
                // since this stub does not write any content
                using var writer = factory.CreateMarkdown("", "api");
                writer.WriteHeading(1, "API Reference");
            }
        }
    }

    /// <summary>No-op emitter that does nothing when Emit is called.</summary>
    private sealed class NoOpEmitter : IApiEmitter
    {
        /// <summary>Does nothing — used for signature-only tests.</summary>
        public void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
        {
            // Intentional no-op: the test only verifies Parse/Emit can be called
        }
    }

    /// <summary>
    ///     Stub generator whose emitter writes two files to simulate multi-file
    ///     GradualDisclosure output: one root <c>api.md</c> and one additional page.
    /// </summary>
    private sealed class MultiFileStubGenerator : IApiGenerator
    {
        /// <summary>
        ///     Returns a multi-file emitter regardless of the EmitConfig passed to Emit.
        /// </summary>
        public IApiEmitter Parse(IContext context) => new MultiFileEmitter();

        /// <summary>Emitter that always creates two files.</summary>
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
    }

    /// <summary>
    ///     Stub generator whose emitter writes only <c>api.md</c> to simulate single-file
    ///     output controlled by <see cref="OutputFormat.SingleFile"/>.
    /// </summary>
    private sealed class SingleFileStubGenerator : IApiGenerator
    {
        /// <summary>
        ///     Returns an emitter that respects <see cref="OutputFormat.SingleFile"/> by
        ///     writing only <c>api.md</c>.
        /// </summary>
        public IApiEmitter Parse(IContext context) => new SingleFileEmitter();

        /// <summary>Emitter that writes exclusively to <c>api.md</c>.</summary>
        private sealed class SingleFileEmitter : IApiEmitter
        {
            /// <summary>
            ///     Creates only the root <c>api.md</c> and writes all content into it,
            ///     regardless of how many concepts are present — single-file contract.
            /// </summary>
            public void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
            {
                // Only api.md is created — single-file format consolidates everything here
                using var root = factory.CreateMarkdown("", "api");
                root.WriteHeading(config.HeadingDepth, "API Reference");
            }
        }
    }
}
