// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

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
        Assert.NotEmpty(emitter.Model.RootNamespaces);
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
    public void DotNetAstModel_Resolver_IsNotNull()
    {
        // Arrange
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act / Assert
        Assert.NotNull(emitter.Model.Resolver);
    }
}
