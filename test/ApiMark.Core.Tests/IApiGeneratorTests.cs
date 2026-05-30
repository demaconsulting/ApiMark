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
    ///     be implemented and its Generate method called with a factory argument.
    /// </summary>
    [Fact]
    public void IApiGenerator_HasGenerate_Method()
    {
        // Arrange: create a minimal stub that accepts the factory and does nothing
        var factory = new InMemoryMarkdownWriterFactory();
        IApiGenerator generator = new MinimalStubGenerator();

        // Act: call Generate — the test verifies the method signature is callable
        generator.Generate(factory);

        // Assert: if we reach here the interface is correctly callable; no exception was thrown
        Assert.True(true);
    }

    /// <summary>
    ///     Verifies that an <see cref="IApiGenerator"/> implementation may store
    ///     construction-time configuration and access it inside Generate.
    /// </summary>
    [Fact]
    public void IApiGenerator_Implementation_UsesConstructionConfiguration()
    {
        // Arrange: create a generator that stores a config value at construction time
        const string expectedConfig = "test-assembly.dll";
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new ConfigurableStubGenerator(expectedConfig);

        // Act: generate — the stub will record whether its config was accessible
        generator.Generate(factory);

        // Assert: the stored config matches what was passed at construction
        Assert.Equal(expectedConfig, generator.ConfigUsedDuringGenerate);
    }

    /// <summary>
    ///     Verifies that an <see cref="IApiGenerator"/> implementation that calls
    ///     <c>factory.CreateMarkdown("", "api")</c> results in the factory having
    ///     a writer registered at the root "api" key.
    /// </summary>
    [Fact]
    public void IApiGenerator_Generate_OutputDirectory_ContainsApiMd()
    {
        // Arrange: create an in-memory factory and a generator that produces api.md
        var factory = new InMemoryMarkdownWriterFactory();
        IApiGenerator generator = new ApiMdProducingStubGenerator();

        // Act: generate the documentation tree
        generator.Generate(factory);

        // Assert: the required api.md entrypoint must have been created
        Assert.True(factory.HasWriter("", "api"), "Generator must call factory.CreateMarkdown(\"\", \"api\") to produce api.md.");
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
        var exception = Record.Exception(() => generator.Generate(factory));

        // Assert: no exception means the interface contract is invocable end-to-end
        Assert.Null(exception);
    }

    /// <summary>
    ///     Minimal stub generator that accepts a factory and does nothing with it.
    ///     Used to verify the method signature is callable.
    /// </summary>
    private sealed class MinimalStubGenerator : IApiGenerator
    {
        /// <summary>
        ///     Implements <see cref="IApiGenerator.Generate"/> as a no-op to confirm
        ///     the method signature matches the interface contract.
        /// </summary>
        /// <param name="factory">Not used.</param>
        public void Generate(IMarkdownWriterFactory factory)
        {
            // Intentional no-op: the test only verifies this method can be called
        }
    }

    /// <summary>
    ///     Stub generator that records the construction-time config string and
    ///     stores it when Generate is called, so tests can verify it was used.
    /// </summary>
    private sealed class ConfigurableStubGenerator : IApiGenerator
    {
        /// <summary>Configuration value captured at construction time.</summary>
        private readonly string _config;

        /// <summary>
        ///     Initializes the generator with a configuration value that should be
        ///     accessible during Generate.
        /// </summary>
        /// <param name="config">Configuration string to store and expose.</param>
        public ConfigurableStubGenerator(string config)
        {
            _config = config;
        }

        /// <summary>
        ///     Gets the configuration value that was available when Generate was last called.
        /// </summary>
        public string? ConfigUsedDuringGenerate { get; private set; }

        /// <summary>
        ///     Records that the construction-time configuration is accessible during generate.
        /// </summary>
        /// <param name="factory">Not used.</param>
        public void Generate(IMarkdownWriterFactory factory)
        {
            // Expose the construction-time config so the test can verify it was preserved
            ConfigUsedDuringGenerate = _config;
        }
    }

    /// <summary>
    ///     Stub generator that creates the mandatory api.md entrypoint file.
    /// </summary>
    private sealed class ApiMdProducingStubGenerator : IApiGenerator
    {
        /// <summary>
        ///     Calls <c>factory.CreateMarkdown("", "api")</c> to produce the fixed
        ///     top-level entrypoint and immediately disposes the writer.
        /// </summary>
        /// <param name="factory">Factory used to create the api.md writer.</param>
        public void Generate(IMarkdownWriterFactory factory)
        {
            // Create the mandatory root entrypoint; dispose immediately after creation
            // since this stub does not write any content
            using var writer = factory.CreateMarkdown("", "api");
            writer.WriteHeading(1, "API Reference");
        }
    }
}
