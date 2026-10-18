// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.Cpp;
using ApiMark.Cpp.CppAst;
using Xunit;

namespace ApiMark.Cpp.Tests;

/// <summary>Unit tests for <see cref="CppEmitterGradualDisclosure"/>.</summary>
public partial class CppEmitterGradualDisclosureTests
{
    /// <summary>Matches a Markdown link destination ending in <c>.md</c>.</summary>
    [GeneratedRegex(@"\]\(([^)]+\.md)\)")]
    private static partial Regex LinkPattern();

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

    /// <summary>Validates that class-scoped type aliases receive their own detail pages under the owning class folder.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_ClassScopedTypeAlias_CreatesAliasPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildRichData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: class-scoped alias page at {namespace}/{TypeName}/{AliasName}
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

    /// <summary>Validates that members differing only by case get distinct pages; the first in ordinal order keeps the plain name.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_CaseTwinMembers_GetDistinctPages()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildRichData();

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("testlib/CaseCollisionClass", "Name"));
        Assert.True(factory.HasWriter("testlib/CaseCollisionClass", "name-2"));
        Assert.False(factory.HasWriter("testlib/CaseCollisionClass", "name"));
    }

    /// <summary>
    ///     Validates that a field with a non-null <see cref="CppField.Value"/> (a simple-literal
    ///     constexpr initializer) renders <c>= value</c> in its signature block.
    /// </summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_FieldWithLiteralValue_SignatureContainsValue()
    {
        // Arrange: a class with a single constexpr field carrying a resolved literal value
        var factory = new InMemoryMarkdownWriterFactory();
        var options = new CppGeneratorOptions
        {
            LibraryName = "TestLib",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
        };
        var nsDecls = new SortedDictionary<string, CppEmitter.NamespaceDeclarations>(StringComparer.Ordinal);
        var ns = new CppEmitter.NamespaceDeclarations("testlib", null);
        ns.Classes.Add(new CppClass(
            "Limits",
            [],
            [],
            [],
            [new CppField("MaxCount", "int", CppAccessibility.Public, true, false, null, new CppDocComment("Maximum count.", null, [], null), "42")],
            [],
            [],
            false,
            false,
            null,
            new CppDocComment("Limit constants.", null, [], null)));
        nsDecls["testlib"] = ns;
        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal));
        var emitter = new CppEmitter(options, nsDecls, resolver);

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the field's signature block includes the literal value
        var writer = factory.GetWriter("testlib/Limits", "MaxCount");
        var signatures = writer.Operations.OfType<SignatureOperation>().ToList();
        Assert.Contains(signatures, s => s.Code.Contains("MaxCount = 42;", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a field with a <see langword="null"/> <see cref="CppField.Value"/> (e.g. a
    ///     computed expression that was deliberately not folded) renders no <c>=</c> in its signature.
    /// </summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_FieldWithoutValue_SignatureContainsNoEquals()
    {
        // Arrange: a class with a single field carrying no resolved literal value
        var factory = new InMemoryMarkdownWriterFactory();
        var options = new CppGeneratorOptions
        {
            LibraryName = "TestLib",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
        };
        var nsDecls = new SortedDictionary<string, CppEmitter.NamespaceDeclarations>(StringComparer.Ordinal);
        var ns = new CppEmitter.NamespaceDeclarations("testlib", null);
        ns.Classes.Add(new CppClass(
            "Limits",
            [],
            [],
            [],
            [new CppField("ComputedLimit", "int", CppAccessibility.Public, true, false, null, new CppDocComment("Computed limit.", null, [], null))],
            [],
            [],
            false,
            false,
            null,
            new CppDocComment("Limit constants.", null, [], null)));
        nsDecls["testlib"] = ns;
        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal));
        var emitter = new CppEmitter(options, nsDecls, resolver);

        // Act
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the field's signature block has no "=" since no literal value was resolved
        var writer = factory.GetWriter("testlib/Limits", "ComputedLimit");
        var signatures = writer.Operations.OfType<SignatureOperation>().ToList();
        Assert.Contains(signatures, s => s.Code.Contains("ComputedLimit;", StringComparison.Ordinal));
        Assert.DoesNotContain(signatures, s => s.Code.Contains('='));
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

    /// <summary>Builds synthetic data whose names are illegal or reserved on Windows (no clang required).</summary>
    private static (CppEmitter emitter, SortedDictionary<string, CppEmitter.NamespaceDeclarations> nsDecls, CppTypeLinkResolver resolver) BuildIllegalNameData()
    {
        var options = new CppGeneratorOptions
        {
            LibraryName = "TestLib",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
        };

        var fooInt = new CppClass(
            "Foo<int>",
            [],
            [],
            [
                new CppFunction("Get", "Aux", [], CppAccessibility.Public, false, false, false, false, false, false, null, new CppDocComment("Gets.", null, [], null)),
                new CppFunction("Name", "int", [], CppAccessibility.Public, false, false, false, false, false, false, null, new CppDocComment("Gets the name.", null, [], null)),
                new CppFunction("operator+", "int", [new CppParameter("o", "int")], CppAccessibility.Public, false, false, false, false, false, false, null, new CppDocComment("Adds.", null, [], null)),
            ],
            [
                new CppField("name", "int", CppAccessibility.Public, false, false, null, new CppDocComment("Stored.", null, [], null)),
            ],
            [
                new CppClass("Aux", [], [], [], [], [], [], false, false, null, new CppDocComment("Nested aux.", null, [], null)),
            ],
            [
                new CppTypeAlias("a:b", "int", false, null, new CppDocComment("Odd alias.", null, [], null)),
            ],
            false,
            false,
            null,
            new CppDocComment("A template instance.", null, [], null));

        var nsDecls = new SortedDictionary<string, CppEmitter.NamespaceDeclarations>(StringComparer.Ordinal);
        var ns = new CppEmitter.NamespaceDeclarations("testlib", new CppDocComment("A test library.", null, [], null));
        ns.Classes.Add(fooInt);
        ns.Classes.Add(new CppClass("Aux", [], [], [], [], [], [], false, false, null, new CppDocComment("Reserved name.", null, [], null)));
        ns.Enums.Add(new CppEnum("CON", [new CppEnumValue("Red", new CppDocComment("Red.", null, [], null))], false, null, new CppDocComment("Reserved enum.", null, [], null)));
        ns.TypeAliases.Add(new CppTypeAlias("a:b", "Aux", false, null, new CppDocComment("Odd alias.", null, [], null)));
        ns.FreeFunctions.Add(new CppFunction("operator<<", "int", [new CppParameter("w", "Aux")], CppAccessibility.Public, false, false, false, false, false, false, null, new CppDocComment("Streams.", null, [], null)));
        nsDecls["testlib"] = ns;

        // A second namespace whose key is a reserved device name
        var auxNs = new CppEmitter.NamespaceDeclarations("aux", new CppDocComment("Reserved namespace.", null, [], null));
        auxNs.Classes.Add(new CppClass("Plain", [], [], [], [], [], [], false, false, null, new CppDocComment("Plain.", null, [], null)));
        nsDecls["aux"] = auxNs;

        var resolver = new CppTypeLinkResolver(CppNames.Build(nsDecls).BuildKnownTypes(nsDecls));

        return (new CppEmitter(options, nsDecls, resolver), nsDecls, resolver);
    }

    /// <summary>Emits the illegal-name data set; the in-memory factory does not validate, so tests assert legality.</summary>
    private static InMemoryMarkdownWriterFactory EmitIllegalNames()
    {
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildIllegalNameData();
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());
        return factory;
    }

    /// <summary>Validates that every folder and file segment written for illegal C++ names is legal on Windows and Linux.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_IllegalAndReservedNames_AllWrittenSegmentsAreLegal()
    {
        // Arrange / Act
        var factory = EmitIllegalNames();

        // Assert: the in-memory factory does not validate, so check each recorded key explicitly
        Assert.NotEmpty(factory.Writers);
        foreach (var key in factory.Writers.Keys)
        {
            foreach (var segment in key.Split('/'))
            {
                Assert.True(FileNameRules.IsLegalSegment(segment), $"Illegal segment '{segment}' in '{key}'");
            }
        }
    }

    /// <summary>Validates that template, reserved, and namespace pages are created at their approved paths.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_IllegalAndReservedNames_PagesUseApprovedPaths()
    {
        // Arrange / Act
        var factory = EmitIllegalNames();

        // Assert
        Assert.True(factory.HasWriter("testlib", "Foo{int}"));
        Assert.True(factory.HasWriter("testlib", "Aux_"));
        Assert.True(factory.HasWriter("testlib", "CON_"));
        Assert.True(factory.HasWriter("testlib", "a_b"));
        Assert.True(factory.HasWriter("testlib/Foo{int}", "Get"));
        Assert.True(factory.HasWriter("testlib/Foo{int}", "Name"));
        Assert.True(factory.HasWriter("testlib/Foo{int}", "name-2"));
        Assert.False(factory.HasWriter("testlib/Foo{int}", "name"));
        Assert.True(factory.HasWriter("testlib/Foo{int}", "operators"));
        Assert.True(factory.HasWriter("testlib/Foo{int}", "Aux_"));
        Assert.True(factory.HasWriter("testlib/Foo{int}", "a_b"));
        Assert.True(factory.HasWriter("", "aux_"));
        Assert.True(factory.HasWriter("aux_", "Plain"));
    }

    /// <summary>Validates that every relative link emitted for illegal names resolves to a page that was written.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_IllegalAndReservedNames_AllLinksResolveToWrittenPages()
    {
        // Arrange / Act
        var factory = EmitIllegalNames();

        // Assert: parse every Markdown link in every table cell and resolve it against the page folder
        var linkPattern = LinkPattern();
        var checkedLinks = 0;
        foreach (var (key, writer) in factory.Writers)
        {
            var slash = key.LastIndexOf('/');
            var folder = slash < 0 ? string.Empty : key[..slash];
            foreach (var cell in writer.Operations.OfType<TableOperation>().SelectMany(t => t.Rows).SelectMany(r => r))
            {
                foreach (System.Text.RegularExpressions.Match match in linkPattern.Matches(cell))
                {
                    var target = Path.GetRelativePath(".", Path.Join(folder, match.Groups[1].Value)).Replace('\\', '/');
                    target = target[..^".md".Length];
                    Assert.True(factory.Writers.ContainsKey(target), $"Link '{match.Groups[1].Value}' on '{key}' resolves to missing page '{target}'");
                    checkedLinks++;
                }
            }
        }

        Assert.True(checkedLinks > 0);
    }

    /// <summary>Builds a function with no documentation for compact test data.</summary>
    private static CppFunction Fn(string name, string returnType, params CppParameter[] parameters) =>
        new(name, returnType, parameters, CppAccessibility.Public, false, false, false, false, false, false, null, null);

    /// <summary>Builds an empty class with the given members for compact test data.</summary>
    private static CppClass Cls(string name, CppFunction[]? methods = null, CppField[]? fields = null, CppClass[]? nested = null) =>
        new(name, [], [], methods ?? [], fields ?? [], nested ?? [], [], false, false, null, null);

    /// <summary>Builds data whose names collide with reserved generated pages and with generic/odd names that need linking.</summary>
    private static (CppEmitter emitter, SortedDictionary<string, CppEmitter.NamespaceDeclarations> nsDecls, CppTypeLinkResolver resolver) BuildReservedPageData()
    {
        var options = new CppGeneratorOptions
        {
            LibraryName = "TestLib",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
        };

        var nsDecls = new SortedDictionary<string, CppEmitter.NamespaceDeclarations>(StringComparer.Ordinal);

        // Namespace named like the api index page, holding a class named like the operators page
        var apiNs = new CppEmitter.NamespaceDeclarations("api", null);
        apiNs.Classes.Add(Cls("operators"));
        apiNs.Classes.Add(Cls("Holder", [Fn("operators", "int"), Fn("operator+", "int", new CppParameter("o", "int"))]));
        apiNs.Classes.Add(Cls("Cased", [Fn("Operators", "int")], [new CppField("operators", "int", CppAccessibility.Public, false, false, null, null)]));
        apiNs.Classes.Add(Cls("Outer", [Fn("operator-", "int", new CppParameter("o", "int"))], null, [Cls("operators")]));
        apiNs.FreeFunctions.Add(Fn("operator<<", "int", new CppParameter("w", "int")));
        nsDecls["api"] = apiNs;

        // A namespace whose free function is named like the namespace operators page
        var otherNs = new CppEmitter.NamespaceDeclarations("other", null);
        otherNs.FreeFunctions.Add(Fn("operators", "int"));
        otherNs.FreeFunctions.Add(Fn("operator<<", "int", new CppParameter("w", "int")));
        nsDecls["other"] = otherNs;

        // Generic, odd-namespace, and reserved-device types that link back to each other from member pages
        var generic = Cls(
            "Foo<int>",
            [Fn("Self", "a::b::Foo<int>"), Fn("MakeInner", "Inner"), Fn("Use", "int", new CppParameter("a", "a::b::Aux"), new CppParameter("c", "a::b::CON"))],
            null,
            [Cls("Inner")]);
        var oddNs = new CppEmitter.NamespaceDeclarations("a::b", null);
        oddNs.Classes.Add(generic);
        oddNs.Classes.Add(Cls("Aux"));
        oddNs.Enums.Add(new CppEnum("CON", [], false, null, null));
        oddNs.Classes.Add(Cls("Sibling", [Fn("Back", "a::b::Foo<int>"), Fn("Other", "a::b::Aux")]));
        nsDecls["a::b"] = oddNs;

        var resolver = new CppTypeLinkResolver(CppNames.Build(nsDecls).BuildKnownTypes(nsDecls));

        return (new CppEmitter(options, nsDecls, resolver), nsDecls, resolver);
    }

    /// <summary>Emits the reserved-page data set.</summary>
    private static InMemoryMarkdownWriterFactory EmitReservedPages()
    {
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, nsDecls, resolver) = BuildReservedPageData();
        new CppEmitterGradualDisclosure(emitter, nsDecls, resolver).Emit(factory, new EmitConfig(), new InMemoryContext());
        return factory;
    }

    /// <summary>Gets every link target found in the table cells and paragraphs of a page.</summary>
    private static IEnumerable<string> LinkTargets(InMemoryMarkdownWriter writer)
    {
        var linkPattern = LinkPattern();
        var texts = writer.Operations.OfType<TableOperation>().SelectMany(t => t.Rows).SelectMany(r => r)
            .Concat(writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text));
        return texts.SelectMany(t => linkPattern.Matches(t).Select(m => m.Groups[1].Value));
    }

    /// <summary>Validates that a member named like the reserved operators page is moved aside and never overwrites it.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_MemberNamedOperators_DoesNotOverwriteOperatorsPage()
    {
        // Arrange / Act
        var factory = EmitReservedPages();

        // Assert: the operators page keeps its operator content, and the member page is moved aside
        var operatorsPage = factory.GetWriter("api-2/Holder", "operators");
        Assert.Contains(operatorsPage.Operations.OfType<HeadingOperation>(), h => h.Level == 2 && h.Text.StartsWith("operator+", StringComparison.Ordinal));
        Assert.True(factory.HasWriter("api-2/Holder", "operators-2"));
        Assert.Contains(
            factory.GetWriter("api-2", "Holder").Operations.OfType<TableOperation>().SelectMany(t => t.Rows),
            r => r[0].Contains("(Holder/operators-2.md)", StringComparison.Ordinal));

        // A case twin of the reserved name is moved aside too
        Assert.True(factory.HasWriter("api-2/Cased", "Operators-2"));
        Assert.False(factory.HasWriter("api-2/Cased", "operators"));
        Assert.False(factory.HasWriter("api-2/Cased", "Operators"));
    }

    /// <summary>Validates that declarations and namespaces named like reserved pages are moved aside consistently.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_DeclarationsNamedLikeReservedPages_AreMovedAside()
    {
        // Arrange / Act
        var factory = EmitReservedPages();

        // Assert: class, nested class, and free function named operators; namespace named api
        Assert.True(factory.HasWriter("api-2", "operators-2"));
        Assert.True(factory.HasWriter("api-2", "operators"));
        Assert.Contains(
            factory.GetWriter("api-2", "operators").Operations.OfType<HeadingOperation>(),
            h => h.Level == 2 && h.Text.StartsWith("operator<<", StringComparison.Ordinal));
        Assert.True(factory.HasWriter("api-2/Outer", "operators-2"));
        Assert.True(factory.HasWriter("api-2/Outer", "operators"));
        Assert.True(factory.HasWriter("other", "operators-2"));
        Assert.True(factory.HasWriter("other", "operators"));
        Assert.True(factory.HasWriter("", "api-2"));
        Assert.True(factory.HasWriter("", "api"));
        Assert.Contains(
            factory.GetWriter("", "api").Operations.OfType<TableOperation>().SelectMany(t => t.Rows),
            r => r[0].Contains("(api-2.md)", StringComparison.Ordinal));
    }

    /// <summary>Validates that links from member pages of generic and odd-named types use plain relative paths to written pages.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_GenericAndOddNames_MemberPageLinksUseApprovedPaths()
    {
        // Arrange / Act
        var factory = EmitReservedPages();
        const string oddFolder = "a__b";
        const string genericFolder = "a__b/Foo{int}";

        // Assert: from the generic member page, the owning type and nested type resolve without detours
        var self = factory.GetWriter(genericFolder, "Self").Operations.OfType<ParagraphOperation>().Select(p => p.Text);
        Assert.Contains(self, t => t.Contains("(../Foo{int}.md)", StringComparison.Ordinal));
        var inner = factory.GetWriter(genericFolder, "MakeInner").Operations.OfType<ParagraphOperation>().Select(p => p.Text);
        Assert.Contains(inner, t => t.Contains("(Inner.md)", StringComparison.Ordinal));

        // Assert: from sibling and reserved-name pages, the sibling, reserved class, and enum resolve directly
        var sibling = factory.GetWriter(oddFolder, "Sibling").Operations.OfType<TableOperation>().SelectMany(t => t.Rows).Select(r => r.Length > 1 ? r[1] : string.Empty);
        Assert.Contains(sibling, c => c.Contains("(Foo{int}.md)", StringComparison.Ordinal));
        Assert.Contains(sibling, c => c.Contains("(Aux_.md)", StringComparison.Ordinal));
        var use = factory.GetWriter(genericFolder, "Use").Operations.OfType<TableOperation>().SelectMany(t => t.Rows).Select(r => r[1]);
        Assert.Contains(use, c => c.Contains("(../Aux_.md)", StringComparison.Ordinal));
        Assert.Contains(use, c => c.Contains("(../CON_.md)", StringComparison.Ordinal));
    }

    /// <summary>Validates that every link in every table cell and paragraph resolves to a page the emitter wrote.</summary>
    [Fact]
    public void CppEmitterGradualDisclosure_Emit_ReservedAndGenericNames_AllLinksResolveToWrittenPages()
    {
        // Arrange / Act
        var factory = EmitReservedPages();

        // Assert
        var checkedLinks = 0;
        foreach (var (key, writer) in factory.Writers)
        {
            var slash = key.LastIndexOf('/');
            var folder = slash < 0 ? string.Empty : key[..slash];
            foreach (var link in LinkTargets(writer))
            {
                var target = Path.GetRelativePath(".", Path.Join(folder, link)).Replace('\\', '/');
                target = target[..^".md".Length];
                Assert.True(factory.Writers.ContainsKey(target), $"Link '{link}' on '{key}' resolves to missing page '{target}'");
                checkedLinks++;
            }
        }

        Assert.True(checkedLinks > 10);
    }
}
