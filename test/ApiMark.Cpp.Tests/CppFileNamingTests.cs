// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.Cpp.CppAst;
using Xunit;

namespace ApiMark.Cpp.Tests;

/// <summary>
///     Tests for the symbol-name registry applied to C++ output: exact-name rules, every collision scenario,
///     and whole-output properties (unique, legal names whose links all resolve).
/// </summary>
public class CppFileNamingTests
{
    /// <summary>Matches a Markdown link destination ending in <c>.md</c>, with an optional anchor.</summary>
    private static readonly Regex LinkPattern = new(@"\]\(([^)\s#]+\.md)(?:#[^)\s]*)?\)", RegexOptions.Compiled);

    /// <summary>A factory that records every page request so that overwritten pages are detected.</summary>
    private sealed class RecordingFactory : IMarkdownWriterFactory
    {
        /// <summary>The wrapped factory.</summary>
        private readonly InMemoryMarkdownWriterFactory _inner = new();

        /// <summary>Gets every requested page key, in request order.</summary>
        public List<string> Requests { get; } = [];

        /// <summary>Gets the pages written so far.</summary>
        public IReadOnlyDictionary<string, InMemoryMarkdownWriter> Writers => _inner.Writers;

        /// <inheritdoc />
        public IMarkdownWriter CreateMarkdown(string subFolder, string name)
        {
            Requests.Add(subFolder.Length == 0 ? name : $"{subFolder}/{name}");
            return _inner.CreateMarkdown(subFolder, name);
        }
    }

    /// <summary>Builds a function without documentation.</summary>
    private static CppFunction Fn(string name, string returnType, params CppParameter[] parameters) =>
        new(name, returnType, parameters, CppAccessibility.Public, false, false, false, false, false, false, null, null);

    /// <summary>Builds a class without documentation.</summary>
    private static CppClass Cls(string name, CppFunction[]? methods = null, CppField[]? fields = null, CppClass[]? nested = null) =>
        new(name, [], [], methods ?? [], fields ?? [], nested ?? [], [], false, false, null, null);

    /// <summary>Builds an enum without documentation.</summary>
    private static CppEnum Enm(string name) => new(name, [], false, null, null);

    /// <summary>Builds a type alias without documentation.</summary>
    private static CppTypeAlias Alias(string name) => new(name, "int", false, null, null);

    /// <summary>Builds a field without documentation.</summary>
    private static CppField Field(string name) => new(name, "int", CppAccessibility.Public, false, false, null, null);

    /// <summary>Creates declarations for one namespace.</summary>
    private static CppEmitter.NamespaceDeclarations Ns(string display) => new(display, null);

    /// <summary>Emits the declarations into a recording factory.</summary>
    private static RecordingFactory Emit(SortedDictionary<string, CppEmitter.NamespaceDeclarations> nsDecls)
    {
        var options = new CppGeneratorOptions
        {
            LibraryName = "TestLib",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
        };
        var names = CppNames.Build(nsDecls);
        var resolver = new CppTypeLinkResolver(names.BuildKnownTypes(nsDecls));
        var emitter = new CppEmitter(options, nsDecls, resolver, names);
        var factory = new RecordingFactory();
        emitter.Emit(factory, new EmitConfig(), new InMemoryContext());
        return factory;
    }

    /// <summary>Creates a sorted namespace map from the supplied pairs.</summary>
    private static SortedDictionary<string, CppEmitter.NamespaceDeclarations> Decls(params (string Key, CppEmitter.NamespaceDeclarations Decl)[] items)
    {
        var map = new SortedDictionary<string, CppEmitter.NamespaceDeclarations>(StringComparer.Ordinal);
        foreach (var (key, decl) in items)
        {
            map[key] = decl;
        }

        return map;
    }

    /// <summary>Builds a model that exercises every collision scenario at once.</summary>
    private static SortedDictionary<string, CppEmitter.NamespaceDeclarations> BuildCollisionModel()
    {
        var lib = Ns("lib");
        lib.Classes.Add(Cls("Foo", [Fn("Get", "int"), Fn("get", "int"), Fn("Get", "int", new CppParameter("a", "int")), Fn("operators", "int"), Fn("operator+", "int", new CppParameter("o", "int"))], [Field("Value"), Field("value")], [Cls("Inner"), Cls("inner")]));
        lib.Classes.Add(Cls("Bar"));
        lib.Classes.Add(Cls("bar"));
        lib.Classes.Add(Cls("operators"));
        lib.Enums.Add(Enm("Foo"));
        lib.Enums.Add(Enm("BAR"));
        lib.TypeAliases.Add(Alias("Foo"));
        lib.TypeAliases.Add(Alias("Baz"));
        lib.FreeFunctions.Add(Fn("Foo", "int"));
        lib.FreeFunctions.Add(Fn("baz", "int"));
        lib.FreeFunctions.Add(Fn("Run", "int"));
        lib.FreeFunctions.Add(Fn("Run", "int", new CppParameter("a", "int")));
        lib.FreeFunctions.Add(Fn("operator<<", "int", new CppParameter("w", "int")));
        lib.FreeFunctions.Add(Fn("api", "int"));

        var api = Ns("api");
        api.Classes.Add(Cls("Foo<int>", [Fn("Get", "Foo<int>")]));
        var upper = Ns("API");
        upper.Classes.Add(Cls("Aux"));
        upper.Enums.Add(Enm("CON"));
        var odd = Ns("a::b");
        odd.Classes.Add(Cls("X"));

        return Decls(("lib", lib), ("api", api), ("API", upper), ("a::b", odd), ("operators", Ns("operators")));
    }

    /// <summary>Asserts that every page is unique (case-insensitively, as requested) and legal.</summary>
    internal static void AssertUniqueAndLegal(IEnumerable<string> requests)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var request in requests)
        {
            Assert.True(seen.Add(request), $"Page '{request}' was requested more than once (case-insensitively)");
            foreach (var segment in request.Split('/'))
            {
                Assert.True(FileNameRules.IsLegalSegment(segment), $"Illegal segment '{segment}' in '{request}'");
            }
        }
    }

    /// <summary>Asserts that every relative link resolves to an emitted page and returns the number of links checked.</summary>
    internal static int AssertAllLinksResolve(IReadOnlyDictionary<string, InMemoryMarkdownWriter> writers)
    {
        var checkedLinks = 0;
        foreach (var (key, writer) in writers)
        {
            var slash = key.LastIndexOf('/');
            var folder = slash < 0 ? string.Empty : key[..slash];
            var texts = writer.Operations.OfType<TableOperation>().SelectMany(t => t.Rows).SelectMany(r => r)
                .Concat(writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text));
            foreach (var text in texts)
            {
                foreach (Match match in LinkPattern.Matches(text))
                {
                    var target = Path.GetRelativePath(".", Path.Combine(folder, match.Groups[1].Value)).Replace('\\', '/');
                    target = target[..^".md".Length];
                    Assert.True(writers.ContainsKey(target), $"Link '{match.Groups[1].Value}' on '{key}' resolves to missing page '{target}'");
                    checkedLinks++;
                }
            }
        }

        return checkedLinks;
    }

    /// <summary>A class, enum, alias and function of the same name each get their own page; the class keeps the plain name.</summary>
    [Fact]
    public void CppNames_ClassEnumAliasFunctionSameName_AreSeparatedInKindOrder()
    {
        // Arrange
        var lib = Ns("lib");
        lib.Classes.Add(Cls("Foo"));
        lib.Enums.Add(Enm("Foo"));
        lib.TypeAliases.Add(Alias("Foo"));
        lib.FreeFunctions.Add(Fn("Foo", "int"));

        // Act
        var names = CppNames.Build(Decls(("lib", lib)));
        var ns = CppNames.NamespaceKey("lib");

        // Assert
        Assert.Equal("lib/Foo", names.Path(CppNames.ClassKey(ns, "Foo")));
        Assert.Equal("lib/Foo-2", names.Path(CppNames.EnumKey(ns, "Foo")));
        Assert.Equal("lib/Foo-3", names.Path(CppNames.AliasKey(ns, "Foo")));
        Assert.Equal("lib/Foo-4", names.Path(CppNames.FunctionKey(ns, "Foo")));
    }

    /// <summary>Declarations that differ only by case are separate pages; the ordinally first keeps the plain name.</summary>
    [Fact]
    public void CppNames_CaseTwinClasses_SecondGetsSuffix()
    {
        // Arrange
        var lib = Ns("lib");
        lib.Classes.Add(Cls("Bar", [Fn("Get", "int"), Fn("get", "int")]));
        lib.Classes.Add(Cls("bar"));

        // Act
        var names = CppNames.Build(Decls(("lib", lib)));
        var ns = CppNames.NamespaceKey("lib");
        var bar = CppNames.ClassKey(ns, "Bar");

        // Assert
        Assert.Equal("lib/Bar", names.Path(bar));
        Assert.Equal("lib/bar-2", names.Path(CppNames.ClassKey(ns, "bar")));
        Assert.Equal("lib/Bar/Get", names.Path(CppNames.MemberKey(bar, "Get")));
        Assert.Equal("lib/Bar/get-2", names.Path(CppNames.MemberKey(bar, "get")));
    }

    /// <summary>The reserved api and operators pages keep their names; same-named symbols are displaced with a numeric suffix.</summary>
    [Fact]
    public void CppNames_ReservedNames_AreNeverTaken()
    {
        // Arrange
        var api = Ns("api");
        api.Classes.Add(Cls("Holder", [Fn("operators", "int")]));
        api.Classes.Add(Cls("operators"));
        api.FreeFunctions.Add(Fn("Operators", "int"));

        // Act
        var names = CppNames.Build(Decls(("api", api)));
        var ns = CppNames.NamespaceKey("api");

        // Assert
        Assert.Equal("api", names.Path(CppNames.ApiKey));
        Assert.Equal("api-2", names.Path(ns));
        Assert.Equal("api-2/operators", names.Path(CppNames.OperatorsKey(ns)));
        Assert.Equal("api-2/operators-2", names.Path(CppNames.ClassKey(ns, "operators")));
        Assert.Equal("api-2/Operators-3", names.Path(CppNames.FunctionKey(ns, "Operators")));
        Assert.Equal("api-2/Holder/operators", names.Path(CppNames.OperatorsKey(CppNames.ClassKey(ns, "Holder"))));
        Assert.Equal("api-2/Holder/operators-2", names.Path(CppNames.MemberKey(CppNames.ClassKey(ns, "Holder"), "operators")));
    }

    /// <summary>Template angle brackets become braces and reserved device names are suffixed.</summary>
    [Fact]
    public void CppNames_TemplateAndDeviceNames_AreEscapedOnce()
    {
        // Arrange
        var lib = Ns("lib");
        lib.Classes.Add(Cls("Foo<int>"));
        lib.Enums.Add(Enm("CON"));

        // Act
        var names = CppNames.Build(Decls(("lib", lib)));
        var ns = CppNames.NamespaceKey("lib");

        // Assert
        Assert.Equal("lib/Foo{int}", names.Path(CppNames.ClassKey(ns, "Foo<int>")));
        Assert.Equal("lib/CON_", names.Path(CppNames.EnumKey(ns, "CON")));
    }

    /// <summary>A namespace key with qualification separators is one legal segment.</summary>
    [Fact]
    public void CppNames_QualifiedNamespaceKey_IsOneEscapedSegment()
    {
        // Arrange
        var odd = Ns("a::b");
        odd.Classes.Add(Cls("X"));

        // Act
        var names = CppNames.Build(Decls(("a::b", odd)));

        // Assert
        Assert.Equal("a__b", names.Path(CppNames.NamespaceKey("a::b")));
        Assert.Equal("a__b/X", names.Path(CppNames.ClassKey(CppNames.NamespaceKey("a::b"), "X")));
    }

    /// <summary>Overloaded free functions share one page that lists every overload; no overload overwrites another.</summary>
    [Fact]
    public void CppEmitter_OverloadedFreeFunctions_ShareOnePageWithEveryOverload()
    {
        // Arrange
        var lib = Ns("lib");
        lib.FreeFunctions.Add(Fn("Run", "int"));
        lib.FreeFunctions.Add(Fn("Run", "int", new CppParameter("a", "int")));

        // Act
        var factory = Emit(Decls(("lib", lib)));

        // Assert
        Assert.Single(factory.Requests, r => r == "lib/Run");
        var page = factory.Writers["lib/Run"];
        var headings = page.Operations.OfType<HeadingOperation>().Select(h => h.Text).ToList();
        Assert.Contains("Run()", headings);
        Assert.Contains("Run(int)", headings);
        var rows = factory.Writers["lib"].Operations.OfType<TableOperation>().SelectMany(t => t.Rows).ToList();
        Assert.Contains(rows, r => r[0] == "[Run()](lib/Run.md)");
        Assert.Contains(rows, r => r[0] == "[Run(int)](lib/Run.md)");
    }

    /// <summary>A class and a function of the same name are both written, and each namespace row links to its own page.</summary>
    [Fact]
    public void CppEmitter_ClassAndFunctionSameName_BothPagesWrittenAndLinked()
    {
        // Arrange
        var lib = Ns("lib");
        lib.Classes.Add(Cls("Foo"));
        lib.FreeFunctions.Add(Fn("Foo", "int"));

        // Act
        var factory = Emit(Decls(("lib", lib)));

        // Assert
        Assert.True(factory.Writers.ContainsKey("lib/Foo"));
        Assert.True(factory.Writers.ContainsKey("lib/Foo-2"));
        var rows = factory.Writers["lib"].Operations.OfType<TableOperation>().SelectMany(t => t.Rows).ToList();
        Assert.Contains(rows, r => r[0] == "[Foo](lib/Foo.md)");
        Assert.Contains(rows, r => r[0] == "[Foo](lib/Foo-2.md)");
    }

    /// <summary>Every page of a model that exercises all collision scenarios is unique, legal, and linked.</summary>
    [Fact]
    public void CppEmitter_CollisionModel_AllPagesUniqueLegalAndLinksResolve()
    {
        // Arrange / Act
        var factory = Emit(BuildCollisionModel());

        // Assert
        AssertUniqueAndLegal(factory.Requests);
        Assert.Equal(factory.Requests.Count, factory.Writers.Count);
        Assert.True(AssertAllLinksResolve(factory.Writers) > 20);
        Assert.Equal(1, factory.Requests.Count(r => r == "api"));
    }

    /// <summary>Emitting the same model twice yields identical page sets.</summary>
    [Fact]
    public void CppEmitter_CollisionModel_IsDeterministic()
    {
        // Arrange / Act
        var first = Emit(BuildCollisionModel()).Requests.OrderBy(r => r, StringComparer.Ordinal).ToList();
        var second = Emit(BuildCollisionModel()).Requests.OrderBy(r => r, StringComparer.Ordinal).ToList();

        // Assert
        Assert.Equal(first, second);
    }
}
