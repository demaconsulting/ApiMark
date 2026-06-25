// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.Cpp;
using ApiMark.Cpp.CppAst;
using Xunit;

namespace ApiMark.Cpp.Tests;

/// <summary>Unit tests for <see cref="CppEmitter"/>.</summary>
public class CppEmitterTests
{
    /// <summary>Builds a minimal <see cref="CppEmitter"/> with one namespace and one class for testing.</summary>
    private static (CppEmitter emitter, SortedDictionary<string, CppEmitter.NamespaceDeclarations> nsDecls, CppTypeLinkResolver resolver) BuildMinimalEmitter()
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

    /// <summary>Validates that passing null to <see cref="CppEmitter.Emit"/> throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void CppEmitter_Emit_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var (emitter, _, _) = BuildMinimalEmitter();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => emitter.Emit(null!, new EmitConfig(), new InMemoryContext()));
    }

    /// <summary>Validates that <see cref="OutputFormat.GradualDisclosure"/> produces more than one writer.</summary>
    [Fact]
    public void CppEmitter_Emit_GradualDisclosureFormat_ProducesMultipleFiles()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, _, _) = BuildMinimalEmitter();

        // Act
        emitter.Emit(factory, new EmitConfig { Format = OutputFormat.GradualDisclosure }, new InMemoryContext());

        // Assert
        Assert.True(factory.Writers.Count > 1, "GradualDisclosure format must produce more than one writer");
    }

    /// <summary>Validates that <see cref="OutputFormat.SingleFile"/> produces exactly one writer keyed as "api".</summary>
    [Fact]
    public void CppEmitter_Emit_SingleFileFormat_ProducesSingleApiFile()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, _, _) = BuildMinimalEmitter();

        // Act
        emitter.Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("", "api"), "SingleFile format must produce an api writer");
        Assert.Single(factory.Writers);
    }

    /// <summary>Validates that <see cref="CppEmitter.SanitizeFileName"/> leaves regular names unchanged.</summary>
    [Fact]
    public void CppEmitter_SanitizeFileName_RegularName_IsUnchanged()
    {
        // Arrange / Act
        var result = CppEmitter.SanitizeFileName("MyClass");

        // Assert
        Assert.Equal("MyClass", result);
    }


    /// <summary>Validates that <see cref="CppEmitter.BuildClassDeclaration"/> returns the class name for a non-final class with no bases.</summary>
    [Fact]
    public void CppEmitter_BuildClassDeclaration_NonFinalNoBase_ReturnsJustClassName()
    {
        // Arrange
        var cls = new CppClass("Circle", [], [], [], [], [], [], false, false, null, null);

        // Act
        var result = CppEmitter.BuildClassDeclaration(cls);

        // Assert
        Assert.Equal("class Circle", result);
    }

    /// <summary>Validates that <see cref="CppEmitter.BuildClassDeclaration"/> appends "final" for a final class.</summary>
    [Fact]
    public void CppEmitter_BuildClassDeclaration_FinalClass_AppendsFinalKeyword()
    {
        // Arrange
        var cls = new CppClass("FinalWidget", [], [], [], [], [], [], false, true, null, null);

        // Act
        var result = CppEmitter.BuildClassDeclaration(cls);

        // Assert: the exact declaration string for a final class with no bases
        Assert.Equal("class FinalWidget final", result);
    }

    /// <summary>Validates that <see cref="CppEmitter.BuildClassDeclaration"/> appends both final and inheritance list correctly.</summary>
    [Fact]
    public void CppEmitter_BuildClassDeclaration_FinalClassWithBaseTypes_AppendsFinalAndInheritance()
    {
        // Arrange
        var cls = new CppClass(
            "FinalWidget",
            [new CppBaseType("Base")],
            [],
            [],
            [],
            [],
            [],
            false,
            true,
            null,
            null);

        // Act
        var result = CppEmitter.BuildClassDeclaration(cls);

        // Assert: final keyword precedes the inheritance list
        Assert.Equal("class FinalWidget final : public Base", result);
    }

    /// <summary>Validates that colliding member names are merged onto a single combined page keyed by the lowercase name.</summary>
    [Fact]
    public void CppEmitter_WriteCombinedMemberPage_CaseInsensitiveCollision_ProducesSingleCombinedPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal));
        var cls = new CppClass(
            "CaseCollisionClass",
            [],
            [],
            [
                new CppFunction("Name", "int", [], CppAccessibility.Public, false, false, false, false, false, false, null, null),
            ],
            [
                new CppField("name", "int", CppAccessibility.Public, false, false, null, null),
            ],
            [],
            [],
            false,
            false,
            null,
            null);

        // Act
        CppEmitter.WriteCombinedMemberPage(
            factory,
            "fixtures",
            "fixtures",
            cls,
            "name",
            [cls.Members[0], cls.Fields[0]],
            resolver);

        // Assert
        Assert.True(factory.HasWriter("fixtures/CaseCollisionClass", "name"));
        Assert.False(factory.HasWriter("fixtures/CaseCollisionClass", "Name"));
        var writer = factory.GetWriter("fixtures/CaseCollisionClass", "name");
        var headings = writer.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Level == 2 && h.Text.StartsWith("Name", StringComparison.Ordinal));
        Assert.Contains(headings, h => h.Level == 2 && h.Text.StartsWith("name", StringComparison.Ordinal));
    }

    /// <summary>Validates that invalid file-name characters are replaced with underscores.</summary>
    [Fact]
    public void CppEmitter_SanitizeFileName_InvalidCharacters_AreReplacedWithUnderscore()
    {
        // Arrange / Act
        var result = CppEmitter.SanitizeFileName("operator/");

        // Assert
        Assert.Equal("operator_", result);
    }

    /// <summary>Validates that base-class names are appended to the class declaration line.</summary>
    [Fact]
    public void CppEmitter_BuildClassDeclaration_WithBaseTypes_AppendsInheritanceList()
    {
        // Arrange
        var cls = new CppClass(
            "Circle",
            [new CppBaseType("Shape"), new CppBaseType("Renderable")],
            [],
            [],
            [],
            [],
            [],
            false,
            false,
            null,
            null);

        // Act
        var result = CppEmitter.BuildClassDeclaration(cls);

        // Assert
        Assert.Equal("class Circle : public Shape, public Renderable", result);
    }

    /// <summary>
    ///     Validates that <see cref="CppEmitter.GetIncludePath"/> returns a root-relative path
    ///     when the source file resides under a configured public include root.
    /// </summary>
    [Fact]
    public void CppEmitter_GetIncludePath_MatchingRoot_ReturnsRelativePath()
    {
        // Arrange: emitter whose include root is the fixture include directory
        var (emitter, _, _) = BuildMinimalEmitter();
        var root = FixturePaths.GetFixtureIncludeDir();
        var headerFile = Path.Combine(root, "mylib", "widget.h");

        // Act
        var result = emitter.GetIncludePath(headerFile);

        // Assert: path relative to the matching root, forward-slash separated
        Assert.Equal("mylib/widget.h", result);
    }

    /// <summary>
    ///     Validates that <see cref="CppEmitter.GetIncludePath"/> returns the full normalized
    ///     forward-slash path when the source file does not reside under any configured root.
    /// </summary>
    [Fact]
    public void CppEmitter_GetIncludePath_NoMatchingRoot_ReturnsFileName()
    {
        // Arrange: header path completely outside any configured include root
        var (emitter, _, _) = BuildMinimalEmitter();
        var headerFile = Path.Combine(Path.GetTempPath(), "standalone.h");

        // Act
        var result = emitter.GetIncludePath(headerFile);

        // Assert: when no root matches, the result contains the file name
        Assert.EndsWith("standalone.h", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Validates that passing null to <see cref="CppEmitter.GetIncludePath"/> throws
    ///     <see cref="ArgumentNullException"/> before any path processing is attempted.
    /// </summary>
    [Fact]
    public void CppEmitter_GetIncludePath_NullSourceFile_ThrowsArgumentNullException()
    {
        // Arrange
        var (emitter, _, _) = BuildMinimalEmitter();

        // Act / Assert: null source file must be rejected immediately
        Assert.Throws<ArgumentNullException>(() => emitter.GetIncludePath(null!));
    }

    /// <summary>
    ///     Validates that passing a <paramref name="members"/> list with fewer than two elements to
    ///     <see cref="CppEmitter.WriteCombinedMemberPage"/> throws <see cref="ArgumentException"/>,
    ///     because a combined page by definition requires at least two members.
    /// </summary>
    [Fact]
    public void CppEmitter_WriteCombinedMemberPage_TooFewMembers_ThrowsArgumentException()
    {
        // Arrange: a factory, resolver, class, and a list with only one member
        var factory = new InMemoryMarkdownWriterFactory();
        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal));
        var cls = new CppClass("TestClass", [], [], [], [], [], [], false, false, null, null);
        var singleMember = new CppFunction(
            "GetCount", "int", [], CppAccessibility.Public,
            false, false, false, false, false, false, null, null);

        // Act / Assert: one-element list must be rejected — combined page requires ≥ 2
        Assert.Throws<ArgumentException>(() =>
            CppEmitter.WriteCombinedMemberPage(
                factory, "ns", "ns", cls, "getcount",
                [singleMember], resolver));
    }

    /// <summary>
    ///     Validates that <see cref="CppEmitter.WriteExternalTypesSection"/> emits an
    ///     "External Types" heading and a table when the external-types set is non-empty.
    /// </summary>
    [Fact]
    public void CppEmitter_WriteExternalTypesSection_WithEntries_WritesExternalTypesSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        using var writer = factory.CreateMarkdown("", "test");
        var externalTypes = new SortedSet<CppExternalTypeInfo>
        {
            new CppExternalTypeInfo("Logger", "acme"),
        };

        // Act
        CppEmitter.WriteExternalTypesSection(writer, externalTypes);

        // Assert: an "External Types" H2 heading must appear in the written output
        var headings = factory.GetWriter("", "test").Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("External Types", StringComparison.Ordinal));
        Assert.Contains(headings, h => h.Level == 2 && h.Text.Contains("External Types", StringComparison.Ordinal));

        // Assert: the table must include a row for Logger in the acme namespace
        var tables = factory.GetWriter("", "test").Operations.OfType<TableOperation>().ToList();
        var allCells = tables.SelectMany(t => t.Rows).SelectMany(r => r).ToList();
        Assert.Contains(allCells, c => c.Contains("Logger", StringComparison.Ordinal));
        Assert.Contains(allCells, c => c.Contains("acme", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that <see cref="CppEmitter.WriteExternalTypesSection"/> writes no output
    ///     when the external-types set is empty.
    /// </summary>
    [Fact]
    public void CppEmitter_WriteExternalTypesSection_EmptySet_WritesNothing()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        using var writer = factory.CreateMarkdown("", "empty");
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Act
        CppEmitter.WriteExternalTypesSection(writer, externalTypes);

        // Assert: no operations were written to the writer — empty set must produce no output
        var headings = factory.GetWriter("", "empty").Operations.OfType<HeadingOperation>().ToList();
        Assert.Empty(headings);
        var tables = factory.GetWriter("", "empty").Operations.OfType<TableOperation>().ToList();
        Assert.Empty(tables);
    }
}
