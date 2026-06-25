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
    /// <summary>Builds a representative synthetic API model without invoking clang.</summary>
    private static (CppEmitter emitter, SortedDictionary<string, CppEmitter.NamespaceDeclarations> nsDecls, CppTypeLinkResolver resolver) BuildData()
    {
        var options = new CppGeneratorOptions
        {
            LibraryName = "TestLib",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
        };

        var nsDecls = new SortedDictionary<string, CppEmitter.NamespaceDeclarations>(StringComparer.Ordinal);
        var ns = new CppEmitter.NamespaceDeclarations("testlib", new CppDocComment("A test library.", null, [], null));
        ns.Classes.Add(new CppClass(
            "Widget",
            [],
            [],
            [
                new CppFunction("GetValue", "int", [], CppAccessibility.Public, false, false, false, false, false, false, null, new CppDocComment("Gets the current value.", null, [], null)),
            ],
            [],
            [],
            [],
            false,
            false,
            null,
            new CppDocComment("A widget.", null, [], null)));
        ns.FreeFunctions.Add(new CppFunction("MakeWidget", "Widget", [], CppAccessibility.Public, false, false, false, false, false, false, null, new CppDocComment("Creates a widget.", null, [], "A widget.")));
        ns.Enums.Add(new CppEnum("Color", [new CppEnumValue("Red", new CppDocComment("Red.", null, [], null))], false, null, new CppDocComment("Color options.", null, [], null)));
        nsDecls["testlib"] = ns;

        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "testlib::Widget", "testlib/Widget" },
            { "testlib::Color", "testlib/Color" },
        });

        return (new CppEmitter(options, nsDecls, resolver), nsDecls, resolver);
    }

    /// <summary>Validates that the single-file emitter creates exactly one writer.</summary>
    [Fact]
    public void CppEmitterSingleFile_Emit_MinimalData_CreatesExactlyOneWriter()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildData();

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
        var (emitter, nsDecls, resolver) = BuildData();

        // Act
        new CppEmitterSingleFile(emitter, nsDecls, resolver).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("", "api"));
    }

    /// <summary>Validates that the api file contains a library-name heading.</summary>
    [Fact]
    public void CppEmitterSingleFile_Emit_MinimalData_ApiFileContainsLibraryNameHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildData();

        // Act
        new CppEmitterSingleFile(emitter, nsDecls, resolver).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        var headings = factory.GetWriter("", "api").Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("TestLib", StringComparison.Ordinal));
    }

    /// <summary>Validates that the api file contains a namespace-level heading.</summary>
    [Fact]
    public void CppEmitterSingleFile_Emit_MinimalData_ApiFileContainsNamespaceHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildData();

        // Act
        new CppEmitterSingleFile(emitter, nsDecls, resolver).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        var headings = factory.GetWriter("", "api").Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("testlib", StringComparison.Ordinal));
    }

    /// <summary>Validates that class declarations are emitted as H3 sections in single-file output.</summary>
    [Fact]
    public void CppEmitterSingleFile_Emit_ClassData_ContainsClassSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildData();

        // Act
        new CppEmitterSingleFile(emitter, nsDecls, resolver).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        var headings = factory.GetWriter("", "api").Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Level == 3 && h.Text == "Widget");
    }

    /// <summary>Validates that free functions are emitted as H3 sections in single-file output.</summary>
    [Fact]
    public void CppEmitterSingleFile_Emit_FreeFunction_ContainsFreeFunctionSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildData();

        // Act
        new CppEmitterSingleFile(emitter, nsDecls, resolver).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        var headings = factory.GetWriter("", "api").Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Level == 3 && h.Text.StartsWith("MakeWidget(", StringComparison.Ordinal));
    }

    /// <summary>Validates that enums are emitted as H3 sections in single-file output.</summary>
    [Fact]
    public void CppEmitterSingleFile_Emit_Enum_ContainsEnumSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildData();

        // Act
        new CppEmitterSingleFile(emitter, nsDecls, resolver).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        var headings = factory.GetWriter("", "api").Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Level == 3 && h.Text == "Color");
    }

    /// <summary>Validates that namespace-level type aliases are emitted as H3 sections in single-file output.</summary>
    [Fact]
    public void CppEmitterSingleFile_Emit_TypeAlias_ContainsTypeAliasSection()
    {
        // Arrange: build a namespace with a single type alias
        var factory = new InMemoryMarkdownWriterFactory();
        var options = new CppGeneratorOptions
        {
            LibraryName = "TestLib",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
        };
        var nsDecls = new SortedDictionary<string, CppEmitter.NamespaceDeclarations>(StringComparer.Ordinal);
        var ns = new CppEmitter.NamespaceDeclarations("testlib", null);
        ns.TypeAliases.Add(new CppTypeAlias(
            "WidgetHandle",
            "unsigned int",
            false,
            null,
            new CppDocComment("Handle to a Widget.", null, [], null)));
        nsDecls["testlib"] = ns;
        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal));
        var emitter = new CppEmitter(options, nsDecls, resolver);

        // Act
        new CppEmitterSingleFile(emitter, nsDecls, resolver).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: alias name appears as an H3 heading and underlying type appears in the writer content
        var writer = factory.GetWriter("", "api");
        var headings = writer.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Level == 3 && h.Text == "WidgetHandle");
        var signatures = writer.Operations.OfType<SignatureOperation>().ToList();
        Assert.Contains(signatures, s => s.Code.Contains("WidgetHandle", StringComparison.Ordinal)
            && s.Code.Contains("unsigned int", StringComparison.Ordinal));
    }

    /// <summary>Validates that non-default heading depth offsets are applied consistently.</summary>
    [Fact]
    public void CppEmitterSingleFile_Emit_NonDefaultHeadingDepth_OffsetsHeadings()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildData();

        // Act
        new CppEmitterSingleFile(emitter, nsDecls, resolver).Emit(
            factory,
            new EmitConfig { Format = OutputFormat.SingleFile, HeadingDepth = 2 },
            new InMemoryContext());

        // Assert
        var headings = factory.GetWriter("", "api").Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Level == 2 && h.Text.Contains("TestLib", StringComparison.Ordinal));
        Assert.Contains(headings, h => h.Level == 3 && h.Text.Contains("testlib", StringComparison.Ordinal));
        Assert.Contains(headings, h => h.Level == 4 && h.Text == "Widget");
        Assert.Contains(headings, h => h.Level == 5 && h.Text.StartsWith("GetValue(", StringComparison.Ordinal));
    }
}
