// Copyright (c) DEMA Consulting
// SPDX-License-Identifier: MIT

using System.Text.RegularExpressions;
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Mono.Cecil;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>
///     Tests for the symbol-name registry applied to .NET output: exact-name rules, every collision scenario,
///     and whole-output properties (unique, legal, visibility-independent names whose links all resolve).
/// </summary>
public class DotNetFileNamingTests
{
    /// <summary>Matches a Markdown link destination ending in <c>.md</c>, with an optional anchor.</summary>
    private static readonly Regex LinkPattern = new(@"\]\(([^)\s#]+\.md)(?:#[^)\s]*)?\)", RegexOptions.Compiled);

    /// <summary>Creates an in-memory module whose types are created by <paramref name="build"/>.</summary>
    /// <param name="build">Callback that adds types to the module.</param>
    /// <returns>The resolved names of the module.</returns>
    private static DotNetNames NamesOf(Action<ModuleDefinition> build)
    {
        var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("Synthetic", new Version(1, 0)), "Synthetic", ModuleKind.Dll);
        build(assembly.MainModule);
        return DotNetNames.For(assembly.MainModule);
    }

    /// <summary>Adds a public class to <paramref name="module"/>.</summary>
    /// <param name="module">The module.</param>
    /// <param name="ns">The namespace.</param>
    /// <param name="name">The Cecil type name.</param>
    /// <returns>The new type.</returns>
    private static TypeDefinition AddType(ModuleDefinition module, string ns, string name)
    {
        var type = new TypeDefinition(ns, name, TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        module.Types.Add(type);
        return type;
    }

    /// <summary>Adds a public field to <paramref name="type"/>.</summary>
    /// <param name="module">The module.</param>
    /// <param name="type">The declaring type.</param>
    /// <param name="name">The field name.</param>
    /// <returns>The field.</returns>
    private static FieldDefinition AddField(ModuleDefinition module, TypeDefinition type, string name)
    {
        var field = new FieldDefinition(name, FieldAttributes.Public, module.TypeSystem.Int32);
        type.Fields.Add(field);
        return field;
    }

    /// <summary>NR-namespace: a namespace keeps the plain name and the same-named type is displaced.</summary>
    [Fact]
    public void DotNetNames_NamespaceAndTypeShareAPath_NamespaceWinsAndTypeIsSuffixed()
    {
        var names = NamesOf(m =>
        {
            AddType(m, "A", "B");
            AddType(m, "A.B", "C");
        });

        Assert.Equal("A/B", names.TryGetNamespacePath("A.B"));
        Assert.Equal("A", names.TryGetNamespacePath("A"));
        var typeB = names.Registry.GetPath("T:A.B");
        Assert.Equal("A/B-2", typeB);
        Assert.Equal("A/B/C", names.Registry.GetPath("T:A.B.C"));
    }

    /// <summary>Case-twin namespaces get separate paths; the first in ordinal order keeps the plain name.</summary>
    [Fact]
    public void DotNetNames_CaseTwinNamespaces_SecondGetsSuffix()
    {
        var names = NamesOf(m =>
        {
            AddType(m, "R", "X");
            AddType(m, "R.Twin", "Y");
            AddType(m, "R.twin", "Z");
        });

        Assert.Equal("R/Twin", names.TryGetNamespacePath("R.Twin"));
        Assert.Equal("R/twin-2", names.TryGetNamespacePath("R.twin"));
        Assert.Equal("R/twin-2/Z", names.Registry.GetPath("T:R.twin.Z"));
    }

    /// <summary>A root namespace named like the reserved index page is displaced; api.md is never overwritten.</summary>
    [Fact]
    public void DotNetNames_RootNamespaceNamedApi_IsDisplacedFromReservedPage()
    {
        var names = NamesOf(m =>
        {
            AddType(m, "api", "X");
            AddType(m, "Api", "Y");
        });

        Assert.Equal("Api-2", names.TryGetNamespacePath("Api"));
        Assert.Equal("api-3", names.TryGetNamespacePath("api"));
        Assert.Equal("api", names.Registry.GetPath("R:api"));
    }

    /// <summary>Generic types use brace arity and never collide with a type of a different arity.</summary>
    [Fact]
    public void DotNetNames_GenericTypes_UseBraceArity()
    {
        var names = NamesOf(m =>
        {
            AddType(m, "N", "Foo");
            AddType(m, "N", "Foo`1");
            AddType(m, "N", "Foo`2");
            AddType(m, "N", "Foo1");
        });

        Assert.Equal("Foo", names.Registry.GetName("T:N.Foo"));
        Assert.Equal("Foo{1}", names.Registry.GetName("T:N.Foo`1"));
        Assert.Equal("Foo{2}", names.Registry.GetName("T:N.Foo`2"));
        Assert.Equal("Foo1", names.Registry.GetName("T:N.Foo1"));
    }

    /// <summary>Members named like the reserved operators page, in any case, are displaced.</summary>
    [Fact]
    public void DotNetNames_MembersNamedOperators_AreDisplaced()
    {
        TypeDefinition? type = null;
        FieldDefinition? lower = null;
        FieldDefinition? upper = null;
        var names = NamesOf(m =>
        {
            type = AddType(m, "N", "T");
            lower = AddField(m, type, "operators");
            upper = AddField(m, type, "Operators");
        });

        Assert.Equal("operators", names.Registry.GetName("O:N.T"));
        Assert.Equal("Operators-2", names.TryGetMemberName(upper!, type!));
        Assert.Equal("operators-3", names.TryGetMemberName(lower!, type!));
    }

    /// <summary>Case-twin members get separate pages: the plain name, then the last-resort suffix.</summary>
    [Fact]
    public void DotNetNames_CaseTwinMembers_GetSeparatePages()
    {
        TypeDefinition? type = null;
        FieldDefinition? lower = null;
        FieldDefinition? upper = null;
        var names = NamesOf(m =>
        {
            type = AddType(m, "N", "T");
            lower = AddField(m, type, "value");
            upper = AddField(m, type, "Value");
        });

        Assert.Equal("Value", names.TryGetMemberName(upper!, type!));
        Assert.Equal("value-2", names.TryGetMemberName(lower!, type!));
    }

    /// <summary>A nested type and a member of the declaring type with the same name: the type keeps the name.</summary>
    [Fact]
    public void DotNetNames_NestedTypeAndMemberSameName_TypeWins()
    {
        TypeDefinition? outer = null;
        FieldDefinition? field = null;
        var names = NamesOf(m =>
        {
            outer = AddType(m, "N", "Outer");
            var nested = new TypeDefinition(string.Empty, "Item", TypeAttributes.NestedPublic | TypeAttributes.Class, m.TypeSystem.Object);
            outer.NestedTypes.Add(nested);
            field = AddField(m, outer, "Item");
        });

        Assert.Equal("Item", names.Registry.GetName("T:N.Outer/Item"));
        Assert.Equal("Item-2", names.TryGetMemberName(field!, outer!));
    }

    /// <summary>Every path in the registry is legal and unique case-insensitively, whatever the type names.</summary>
    [Fact]
    public void DotNetNames_HostileNames_ProduceUniqueLegalPaths()
    {
        var names = NamesOf(m =>
        {
            foreach (var name in new[] { "CON", "con", "Aux.txt", "a<b", "a>b", "A_B", "a:b", "x.", "COM1" })
            {
                AddType(m, "Hostile", name);
            }
        });

        var paths = names.Registry.GetAllPaths();
        Assert.Equal(paths.Count, paths.Select(p => p.ToLowerInvariant()).Distinct().Count());
        Assert.All(paths, p => Assert.All(p.Split('/'), s => Assert.True(FileNameRules.IsLegalSegment(s), s)));
    }

    /// <summary>Explicit interface implementations keep their type arguments in the page name.</summary>
    [Fact]
    public void DotNetGenerator_Emit_ExplicitGenericInterfaceMembers_KeepTypeArguments()
    {
        using var module = ModuleDefinition.ReadModule(FixturePaths.GetFixtureDll());
        var type = module.GetType("ApiMark.DotNet.Fixtures.LegalNames.ExplicitGenericImpl");
        var names = DotNetNames.For(module);
        var keys = DotNetEmitter.GetCandidateMembers(type)
            .Select(m => names.TryGetMemberName(m, type))
            .ToList();

        Assert.Contains("IGenericSource{int}.GetItem", keys);
        Assert.Contains("IGenericSource{string}.GetItem", keys);
        Assert.Contains("IGenericSource{Array{int}}.GetItem", keys);
        Assert.Contains("IGenericSource{List{Dictionary{string-int}}}.Current", keys);
    }

    /// <summary>Generic types use the brace arity in their page names.</summary>
    [Fact]
    public void DotNetGenerator_Emit_GenericType_UsesBraceArity()
    {
        var factory = Emit(FixturePaths.GetFixtureDll(), FixturePaths.GetFixtureXmlDoc(), ApiVisibility.Public);

        Assert.Contains("ApiMark.DotNet.Fixtures/PropertyEditorRegistry{1}", factory.Writers.Keys);
        Assert.Contains("ApiMark.DotNet.Fixtures/KeyedPair{2}", factory.Writers.Keys);
    }

    /// <summary>Every generated path is legal and unique case-insensitively, and every link resolves.</summary>
    /// <param name="fixture">The fixture assembly selector.</param>
    /// <param name="visibility">The visibility filter.</param>
    [Theory]
    [InlineData("main", ApiVisibility.Public)]
    [InlineData("main", ApiVisibility.PublicAndProtected)]
    [InlineData("main", ApiVisibility.All)]
    [InlineData("global", ApiVisibility.Public)]
    [InlineData("global", ApiVisibility.All)]
    [InlineData("external", ApiVisibility.All)]
    public void DotNetGenerator_Emit_Fixtures_PathsAreUniqueLegalAndLinksResolve(string fixture, ApiVisibility visibility)
    {
        var factory = EmitFixture(fixture, visibility);

        var keys = factory.Writers.Keys.ToList();
        Assert.NotEmpty(keys);
        Assert.Equal(keys.Count, keys.Select(k => k.ToLowerInvariant()).Distinct().Count());

        // A page and a folder of the same name may coexist, but no folder may shadow a file of another case.
        Assert.All(keys, k => Assert.All(k.Split('/'), s => Assert.True(FileNameRules.IsLegalSegment(s), k)));

        var broken = new List<string>();
        foreach (var (key, writer) in factory.Writers)
        {
            var texts = writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text)
                .Concat(writer.Operations.OfType<TableOperation>().SelectMany(t => t.Rows.SelectMany(r => r)));
            foreach (var link in texts.SelectMany(t => LinkPattern.Matches(t).Select(m => m.Groups[1].Value)))
            {
                if (link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var target = Resolve(key, link);
                if (!factory.Writers.ContainsKey(target))
                {
                    broken.Add($"{key} -> {link} ({target})");
                }
            }
        }

        Assert.True(broken.Count == 0, string.Join(Environment.NewLine, broken.Take(20)));
    }

    /// <summary>A symbol has the same file name whatever the visibility filter.</summary>
    [Fact]
    public void DotNetGenerator_Emit_Fixtures_NamesAreIndependentOfVisibility()
    {
        var publicKeys = EmitFixture("main", ApiVisibility.Public).Writers.Keys.ToHashSet(StringComparer.Ordinal);
        var allKeys = EmitFixture("main", ApiVisibility.All).Writers.Keys.ToHashSet(StringComparer.Ordinal);

        var missing = publicKeys.Where(k => !allKeys.Contains(k)).ToList();
        Assert.True(missing.Count == 0, string.Join(", ", missing.Take(10)));
    }

    /// <summary>Emits one of the fixture assemblies.</summary>
    /// <param name="fixture">The fixture selector.</param>
    /// <param name="visibility">The visibility filter.</param>
    /// <returns>The populated factory.</returns>
    private static InMemoryMarkdownWriterFactory EmitFixture(string fixture, ApiVisibility visibility) => fixture switch
    {
        "global" => Emit(FixturePaths.GetGlobalFixtureDll(), FixturePaths.GetGlobalFixtureXmlDoc(), visibility),
        "external" => Emit(FixturePaths.GetExternalFixtureDll(), FixturePaths.GetExternalFixtureXmlDoc(), visibility),
        _ => Emit(FixturePaths.GetFixtureDll(), FixturePaths.GetFixtureXmlDoc(), visibility),
    };

    /// <summary>Generates gradual-disclosure output for an assembly.</summary>
    /// <param name="dll">The assembly path.</param>
    /// <param name="xml">The XML documentation path.</param>
    /// <param name="visibility">The visibility filter.</param>
    /// <returns>The populated factory.</returns>
    private static InMemoryMarkdownWriterFactory Emit(string dll, string xml, ApiVisibility visibility)
    {
        var factory = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(new DotNetGeneratorOptions
        {
            AssemblyPath = dll,
            XmlDocPath = xml,
            Visibility = visibility,
        }).Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());
        return factory;
    }

    /// <summary>Resolves a relative link against the page that contains it.</summary>
    /// <param name="key">The page key of the containing page.</param>
    /// <param name="link">The link destination.</param>
    /// <returns>The target page key without extension.</returns>
    private static string Resolve(string key, string link)
    {
        var folder = key.Contains('/') ? key[..key.LastIndexOf('/')] : string.Empty;
        var path = new Uri(new Uri("http://x/" + (folder.Length > 0 ? folder + "/" : string.Empty)), link).AbsolutePath.TrimStart('/');
        return Uri.UnescapeDataString(path)[..^3];
    }
}
