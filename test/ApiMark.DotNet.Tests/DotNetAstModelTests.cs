// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Unit tests for <see cref="DotNetAstModel"/>.</summary>
public class DotNetAstModelTests
{
    /// <summary>Builds DotNetGeneratorOptions pointing at the fixture assembly.</summary>
    private static DotNetGeneratorOptions BuildOptions() => new()
    {
        AssemblyPath = FixturePaths.GetFixtureDll(),
        XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
        Visibility = ApiVisibility.Public,
    };

    /// <summary>Validates that <see cref="DotNetAstModel.AllNamespaces"/> returns namespaces in alphabetical order.</summary>
    [Fact]
    public void DotNetAstModel_AllNamespaces_ReturnsAlphabeticallySorted()
    {
        // Arrange
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        var namespaces = emitter.Model.AllNamespaces;

        // Assert
        Assert.Equal(namespaces.OrderBy(n => n, StringComparer.Ordinal).ToList(), namespaces);
    }

    /// <summary>Validates that <see cref="DotNetAstModel.ByNamespace"/> contains the fixture namespace.</summary>
    [Fact]
    public void DotNetAstModel_ByNamespace_ContainsFixtureNamespace()
    {
        // Arrange
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act / Assert
        Assert.True(emitter.Model.ByNamespace.ContainsKey("ApiMark.DotNet.Fixtures"));
    }

    /// <summary>Validates that <see cref="DotNetAstModel.RootNamespaces"/> is non-empty.</summary>
    [Fact]
    public void DotNetAstModel_RootNamespaces_ContainsFixtureNamespace()
    {
        // Arrange
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act / Assert
        Assert.Contains("ApiMark.DotNet.Fixtures", emitter.Model.RootNamespaces);
    }

    /// <summary>Validates that <see cref="DotNetAstModel.Options"/> returns the same options instance passed at construction.</summary>
    [Fact]
    public void DotNetAstModel_Options_ReturnsOptionsPassedAtConstruction()
    {
        // Arrange
        var options = BuildOptions();
        var emitter = (DotNetEmitter)new DotNetGenerator(options).Parse(new InMemoryContext());

        // Act / Assert
        Assert.Same(options, emitter.Model.Options);
    }

    /// <summary>Validates that <see cref="DotNetAstModel.Assembly"/> is non-null with the expected name.</summary>
    [Fact]
    public void DotNetAstModel_Assembly_ReturnsLoadedAssembly()
    {
        // Arrange
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act / Assert
        Assert.NotNull(emitter.Model.Assembly);
        Assert.Contains("Fixtures", emitter.Model.Assembly.Name.Name, StringComparison.Ordinal);
    }

    /// <summary>Validates that <see cref="DotNetAstModel.Resolver"/> is non-null.</summary>
    [Fact]
    public void DotNetAstModel_Resolver_AfterParse_IsNotNull()
    {
        // Arrange
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act / Assert
        Assert.NotNull(emitter.Model.Resolver);
    }

    /// <summary>Validates that <see cref="DotNetAstModel.XmlDocs"/> is non-null after construction.</summary>
    [Fact]
    public void DotNetAstModel_XmlDocs_AfterParse_IsNotNull()
    {
        // Arrange
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act / Assert
        Assert.NotNull(emitter.Model.XmlDocs);
    }

    /// <summary>Validates that <see cref="DotNetAstModel.NamespaceDescriptions"/> is non-null after construction.</summary>
    [Fact]
    public void DotNetAstModel_NamespaceDescriptions_AfterParse_IsNotNull()
    {
        // Arrange
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act / Assert
        Assert.NotNull(emitter.Model.NamespaceDescriptions);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetAstModel.CrefTargets"/> is constructed eagerly during
    ///     <see cref="DotNetGenerator.Parse"/> and can resolve a known fixture type.
    /// </summary>
    [Fact]
    public void DotNetAstModel_CrefTargets_AfterParse_ResolvesKnownFixtureType()
    {
        // Arrange
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        var resolved = emitter.Model.CrefTargets.TryResolveType("T:ApiMark.DotNet.Fixtures.SampleClass", out var type);

        // Assert
        Assert.True(resolved);
        Assert.Equal("SampleClass", type.Name);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetAstModel.MemberPageIndex"/> and
    ///     <see cref="DotNetAstModel.EmittedTypeIds"/> are populated (via
    ///     <c>DotNetGenerator.Parse</c>'s post-construction setup step) once a
    ///     <see cref="DotNetEmitterGradualDisclosure"/> has processed the model, and that the
    ///     index contains an entry for a known visible fixture member.
    /// </summary>
    [Fact]
    public void DotNetAstModel_MemberPageIndexAndEmittedTypeIds_AfterGradualDisclosureEmit_ArePopulated()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.NotEmpty(emitter.Model.MemberPageIndex);
        Assert.NotEmpty(emitter.Model.EmittedTypeIds);
        Assert.Contains("M:ApiMark.DotNet.Fixtures.SampleClass.Reset", emitter.Model.MemberPageIndex.Keys);
        Assert.Contains("T:ApiMark.DotNet.Fixtures.SampleClass", emitter.Model.EmittedTypeIds);
    }

    /// <summary>
    ///     Validates that the collection-type properties of <see cref="DotNetAstModel"/> satisfy the
    ///     read-only interface constraints at runtime — confirming the compiler's static guarantees
    ///     hold through the parse pipeline.
    /// </summary>
    [Fact]
    public void DotNetAstModel_Collections_ExposeReadOnlyInterfaces()
    {
        // Arrange
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());
        var model = emitter.Model;

        // Assert: each collection-type property must satisfy IReadOnly* at runtime
        Assert.IsType<IReadOnlyList<string>>(model.AllNamespaces, exactMatch: false);
        Assert.IsType<IReadOnlyDictionary<string, IReadOnlyList<Mono.Cecil.TypeDefinition>>>(model.ByNamespace, exactMatch: false);
        Assert.IsType<IReadOnlyList<string>>(model.RootNamespaces, exactMatch: false);
    }
}
