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

    /// <summary>Validates that the gradual-disclosure emitter creates a dedicated detail page for at least one visible member.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesMemberDetailPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: SampleClass has a Reset method — its detail page must exist
        Assert.True(
            factory.Writers.Keys.Any(k =>
                k.Contains("SampleClass", StringComparison.Ordinal) &&
                k.Contains("Reset", StringComparison.Ordinal)),
            "Expected a member detail page for SampleClass.Reset");
    }

    /// <summary>Validates that the gradual-disclosure emitter creates a combined page for case-colliding members.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_CaseCollision_CreatesCombinedPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: CaseCollisionClass has 'name' (field) and 'Name' (property) that collide on
        // case-insensitive filesystems; the combined page is keyed using the lower-invariant "name"
        Assert.True(
            factory.Writers.Keys.Any(k =>
                k.Contains("CaseCollisionClass", StringComparison.Ordinal) &&
                k.EndsWith("/name", StringComparison.OrdinalIgnoreCase)),
            "Expected a combined collision page for CaseCollisionClass members 'name' and 'Name'");
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
        Assert.Contains(headings, h => h.Text.Contains("ApiMark.DotNet.Fixtures API Reference", StringComparison.Ordinal));
    }

    /// <summary>Validates that a type with overloaded methods produces a consolidated overload page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesMethodOverloadPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: IntVsIntArrayClass has two overloads of Process() — they should share one page
        Assert.True(
            factory.Writers.Keys.Any(k =>
                k.Contains("IntVsIntArrayClass", StringComparison.Ordinal) &&
                k.EndsWith("/Process", StringComparison.OrdinalIgnoreCase)),
            "Expected a consolidated overload page for IntVsIntArrayClass.Process");
    }

    /// <summary>Validates that a type with operator overloads produces an operators.md page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesOperatorsPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: OperatorsStruct declares operator overloads — an operators.md page must be created
        Assert.True(
            factory.Writers.Keys.Any(k =>
                k.Contains("OperatorsStruct", StringComparison.Ordinal) &&
                k.EndsWith("/operators", StringComparison.OrdinalIgnoreCase)),
            "Expected an operators page for OperatorsStruct");
    }

    /// <summary>Validates that a type with a nested type produces a dedicated page for the nested type.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesNestedTypePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: OuterClass.Inner should produce a dedicated page under the OuterClass folder
        Assert.True(
            factory.Writers.Keys.Any(k =>
                k.Contains("OuterClass", StringComparison.Ordinal) &&
                k.Contains("Inner", StringComparison.Ordinal)),
            "Expected a dedicated page for OuterClass.Inner nested type");
    }

    /// <summary>Validates that a child namespace also produces a dedicated Markdown page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesChildNamespacePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the child namespace ApiMark.DotNet.Fixtures.Inner must produce its own page
        Assert.True(
            factory.Writers.Keys.Any(k => k.Contains("Inner", StringComparison.Ordinal) &&
                                          !k.Contains("OuterClass", StringComparison.Ordinal)),
            "Expected a dedicated page for the ApiMark.DotNet.Fixtures.Inner child namespace");
    }
}
