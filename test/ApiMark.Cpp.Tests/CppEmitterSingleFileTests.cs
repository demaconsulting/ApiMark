// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.Cpp;
using ApiMark.Cpp.CppAst;
using Xunit;

namespace ApiMark.Cpp.Tests;

/// <summary>Unit tests for <see cref="CppEmitterSingleFile"/>.</summary>
public class CppEmitterSingleFileTests
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

    /// <summary>Validates that the single-file emitter creates exactly one writer.</summary>
    [Fact]
    public void CppEmitterSingleFile_Emit_MinimalData_CreatesExactlyOneWriter()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildMinimalData();

        // Act
        new CppEmitterSingleFile(emitter, nsDecls, resolver).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        Assert.Single(factory.Writers);
    }

    /// <summary>Validates that the single-file emitter creates the api writer only.</summary>
    [Fact]
    public void CppEmitterSingleFile_Emit_MinimalData_CreatesApiFileOnly()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildMinimalData();

        // Act
        new CppEmitterSingleFile(emitter, nsDecls, resolver).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("", "api"), "Expected api writer to be created");
    }

    /// <summary>Validates that the api file contains a library-name heading.</summary>
    [Fact]
    public void CppEmitterSingleFile_Emit_MinimalData_ApiFileContainsLibraryNameHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildMinimalData();

        // Act
        new CppEmitterSingleFile(emitter, nsDecls, resolver).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("TestLib", StringComparison.Ordinal));
    }

    /// <summary>Validates that the api file contains a namespace-level heading.</summary>
    [Fact]
    public void CppEmitterSingleFile_Emit_MinimalData_ApiFileContainsNamespaceHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildMinimalData();

        // Act
        new CppEmitterSingleFile(emitter, nsDecls, resolver).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("testlib", StringComparison.Ordinal));
    }
}
