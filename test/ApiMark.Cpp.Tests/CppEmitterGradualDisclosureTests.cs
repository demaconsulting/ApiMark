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
    /// <summary>Builds a representative synthetic API model without invoking clang.</summary>
    private static (CppEmitter emitter, SortedDictionary<string, CppEmitter.NamespaceDeclarations> nsDecls, CppTypeLinkResolver resolver) BuildRichData()
    {
        var options = new CppGeneratorOptions
        {
            LibraryName = "TestLib",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
        };

        var widget = new CppClass(
            "Widget",
            [],
            [],
            [
                new CppFunction("GetValue", "int", [], CppAccessibility.Public, false, false, false, false, false, false, null, new CppDocComment("Gets the current value.", null, [], null)),
                new CppFunction("operator+", "Widget", [new CppParameter("other", "Widget")], CppAccessibility.Public, false, false, false, false, false, false, null, new CppDocComment("Adds widgets.", null, [], "Combined widget.")),
            ],
            [],
            [
                new CppClass("Nested", [], [], [], [], [], [], false, false, null, new CppDocComment("Nested widget type.", null, [], null)),
            ],
            [
                new CppTypeAlias("size_type", "std::size_t", false, null, new CppDocComment("Widget size alias.", null, [], null)),
            ],
            false,
            false,
            null,
            new CppDocComment("A widget.", null, [], null));

        var collisionClass = new CppClass(
            "CaseCollisionClass",
            [],
            [],
            [
                new CppFunction("Name", "int", [], CppAccessibility.Public, false, false, false, false, false, false, null, new CppDocComment("Gets the name.", null, [], null)),
            ],
            [
                new CppField("name", "int", CppAccessibility.Public, false, false, null, new CppDocComment("The stored name.", null, [], null)),
            ],
            [],
            [],
            false,
            false,
            null,
            new CppDocComment("Collision fixture.", null, [], null));

        var nsDecls = new SortedDictionary<string, CppEmitter.NamespaceDeclarations>(StringComparer.Ordinal);
        var ns = new CppEmitter.NamespaceDeclarations("testlib", new CppDocComment("A test library.", null, [], null));
        ns.Classes.Add(widget);
        ns.Classes.Add(collisionClass);
        ns.Enums.Add(new CppEnum("Color", [new CppEnumValue("Red", new CppDocComment("Red.", null, [], null))], false, null, new CppDocComment("Color options.", null, [], null)));
        ns.TypeAliases.Add(new CppTypeAlias("widget_id_t", "int", false, null, new CppDocComment("Widget identifier.", null, [], null)));
        ns.FreeFunctions.Add(new CppFunction("MakeWidget", "Widget", [], CppAccessibility.Public, false, false, false, false, false, false, null, new CppDocComment("Creates a widget.", null, [], "A widget.")));
        ns.FreeFunctions.Add(new CppFunction("operator<<", "std::ostream &", [new CppParameter("stream", "std::ostream &"), new CppParameter("widget", "Widget")], CppAccessibility.Public, false, false, false, false, false, false, null, new CppDocComment("Streams a widget.", null, [], "The stream.")));
        nsDecls["testlib"] = ns;

        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "testlib::Widget", "testlib/Widget" },
            { "testlib::Color", "testlib/Color" },
            { "testlib::widget_id_t", "testlib/widget_id_t" },
            { "testlib::Widget::Nested", "testlib/Widget/Nested" },
            { "testlib::Widget::size_type", "testlib/Widget/size_type" },
        });

        return (new CppEmitter(options, nsDecls, resolver), nsDecls, resolver);
    }

    /// <summary>Builds an emitter with no namespaces so the api page fallback path can be verified.</summary>
    private static (CppEmitter emitter, SortedDictionary<string, CppEmitter.NamespaceDeclarations> nsDecls, CppTypeLinkResolver resolver) BuildEmptyData()
    {
        var options = new CppGeneratorOptions
        {
            LibraryName = "EmptyLib",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
        };
        var nsDecls = new SortedDictionary<string, CppEmitter.NamespaceDeclarations>(StringComparer.Ordinal);
        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal));
        return (new CppEmitter(options, nsDecls, resolver), nsDecls, resolver);
    }

    /// <summary>Validates that the gradual-disclosure emitter creates the api index page.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_MinimalData_CreatesApiIndexPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildRichData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("", "api"));
    }

    /// <summary>Validates that the gradual-disclosure emitter creates a namespace page.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_MinimalData_CreatesNamespacePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildRichData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("", "testlib"));
    }

    /// <summary>Validates that the gradual-disclosure emitter creates a type page for Widget.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_MinimalData_CreatesTypePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildRichData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("testlib", "Widget"));
    }

    /// <summary>Validates that the api index page heading contains the library name.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_MinimalData_ApiIndexContainsLibraryNameHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildRichData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var headings = factory.GetWriter("", "api").Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("TestLib", StringComparison.Ordinal));
    }

    /// <summary>Validates that a method detail page is written for a regular class member.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_MethodMember_CreatesMemberDetailPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildRichData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("testlib/Widget", "GetValue"));
    }

    /// <summary>Validates that a free-function detail page is written under the namespace folder.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_FreeFunction_CreatesFreeFunctionPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildRichData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("testlib", "MakeWidget"));
    }

    /// <summary>Validates that enum declarations receive their own detail pages.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_Enum_CreatesEnumPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildRichData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("testlib", "Color"));
    }

    /// <summary>Validates that type aliases receive their own detail pages.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_TypeAlias_CreatesTypeAliasPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildRichData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("testlib", "widget_id_t"));
        Assert.True(factory.HasWriter("testlib/Widget", "size_type"));
    }

    /// <summary>Validates that nested classes receive pages under their parent class folder.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_NestedClass_CreatesNestedClassPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildRichData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("testlib/Widget", "Nested"));
    }

    /// <summary>Validates that colliding member names are merged onto a single lowercase page.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_CaseInsensitiveCollision_CreatesCombinedPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildRichData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("testlib/CaseCollisionClass", "name"));
        Assert.False(factory.HasWriter("testlib/CaseCollisionClass", "Name"));
    }

    /// <summary>Validates that class-scoped operators are grouped onto a shared operators page.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_ClassOperators_CreatesOperatorsPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildRichData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("testlib/Widget", "operators"));
    }

    /// <summary>Validates that api.md emits a fallback paragraph when no namespaces are present.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_EmptyNamespaces_ApiPageContainsFallbackParagraph()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildEmptyData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var paragraphs = factory.GetWriter("", "api").Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains("No public API declarations found.", paragraphs);
    }

    /// <summary>Validates that namespace-level operators are grouped onto a shared namespace operators page.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_NamespaceOperators_CreatesOperatorsPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildRichData();

        // Act: BuildRichData() includes a namespace-level operator<< in the testlib namespace
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: namespace operators are written to {namespace}/operators.md
        Assert.True(factory.HasWriter("testlib", "operators"));
    }
}
