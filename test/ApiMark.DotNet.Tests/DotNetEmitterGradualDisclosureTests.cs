// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Unit tests for <see cref="DotNetEmitterGradualDisclosure"/>.</summary>
public class DotNetEmitterGradualDisclosureTests
{
    /// <summary>Builds DotNetGeneratorOptions pointing at the fixture assembly.</summary>
    private static DotNetGeneratorOptions BuildOptions() => new()
    {
        AssemblyPath = FixturePaths.GetFixtureDll(),
        XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
        Visibility = ApiVisibility.Public,
    };

    /// <summary>Validates that the gradual-disclosure emitter creates the api index page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesApiIndexPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("", "api"), "Expected api index page to be created");
    }

    /// <summary>Validates that the gradual-disclosure emitter creates a namespace page for the fixture namespace.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesNamespacePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: root namespace page exists as a root-level writer
        Assert.True(
            factory.Writers.Keys.Any(k => k.EndsWith("ApiMark.DotNet.Fixtures", StringComparison.Ordinal)),
            "Expected a namespace page containing 'ApiMark.DotNet.Fixtures'");
    }

    /// <summary>Validates that the gradual-disclosure emitter creates a type page for SampleClass.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesTypePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(
            factory.Writers.Keys.Any(k => k.Contains("SampleClass", StringComparison.Ordinal)),
            "Expected a type page containing 'SampleClass'");
    }

    /// <summary>Validates that the api index page heading contains the assembly name.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_ApiIndexContainsAssemblyNameHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: api page heading contains assembly name text
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("Fixtures", StringComparison.Ordinal));
    }
}
