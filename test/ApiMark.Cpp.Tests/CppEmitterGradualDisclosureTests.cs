// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.Cpp;
using ApiMark.Cpp.CppAst;
using Xunit;

namespace ApiMark.Cpp.Tests;

/// <summary>Unit tests for <see cref="CppEmitterGradualDisclosure"/>.</summary>
public class CppEmitterGradualDisclosureTests
{
    /// <summary>Builds a minimal set of namespace declarations for testing without invoking clang.</summary>
    private static (CppEmitter emitter, SortedDictionary<string, CppEmitter.NamespaceDeclarations> nsDecls, CppTypeLinkResolver resolver) BuildMinimalData()
    {
        var options = new CppGeneratorOptions
        {
            LibraryName = "TestLib",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
        };
        var nsDecls = new SortedDictionary<string, CppEmitter.NamespaceDeclarations>(StringComparer.Ordinal);
        var ns = new CppEmitter.NamespaceDeclarations("testlib", new CppDocComment("A test library.", null, [], null));
        ns.Classes.Add(new CppClass("Widget", [], [], [], [], [], [], false, false, null,
            new CppDocComment("A widget.", null, [], null)));
        nsDecls["testlib"] = ns;
        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "testlib::Widget", "testlib/Widget" },
        });
        var emitter = new CppEmitter(options, nsDecls, resolver);
        return (emitter, nsDecls, resolver);
    }

    /// <summary>Validates that the gradual-disclosure emitter creates the api index page.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_MinimalData_CreatesApiIndexPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildMinimalData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("", "api"), "Expected api index page to be created");
    }

    /// <summary>Validates that the gradual-disclosure emitter creates a namespace page.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_MinimalData_CreatesNamespacePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildMinimalData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: a namespace page exists for the "testlib" namespace key
        Assert.True(
            factory.Writers.Keys.Any(k => k.Contains("testlib", StringComparison.Ordinal)),
            "Expected a namespace page containing 'testlib'");
    }

    /// <summary>Validates that the gradual-disclosure emitter creates a type page for Widget.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_MinimalData_CreatesTypePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildMinimalData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(
            factory.Writers.Keys.Any(k => k.Contains("Widget", StringComparison.Ordinal)),
            "Expected a type page containing 'Widget'");
    }

    /// <summary>Validates that the api index page heading contains the library name.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_MinimalData_ApiIndexContainsLibraryNameHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildMinimalData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: api page heading contains the library name
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("TestLib", StringComparison.Ordinal));
    }
}
