// Copyright (c) DEMA Consulting
// SPDX-License-Identifier: MIT

using System.Text.RegularExpressions;
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>
///     End-to-end naming tests over a synthetic assembly whose hostile names cannot be written in C#:
///     literal braces and square brackets, Windows device names, names that look like reserved pages, a
///     namespace that collides with a type, and case twins. Every scenario asserts the exact file names
///     the symbol-name registry produces and that every link and breadcrumb resolves to a written page.
/// </summary>
public class DotNetSyntheticNamingTests : IDisposable
{
    /// <summary>Matches a Markdown link destination ending in <c>.md</c>, with an optional anchor.</summary>
    private static readonly Regex LinkPattern = new(@"\]\(([^)\s#]+\.md)(?:#[^)\s]*)?\)", RegexOptions.Compiled);

    /// <summary>The directory that holds the synthetic assembly.</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "apimark-synthetic-" + Guid.NewGuid().ToString("N"));

    /// <summary>Gets the page keys of the shared all-visibility emit.</summary>
    private HashSet<string> AllKeys => Emitted.Writers.Keys.ToHashSet(StringComparer.Ordinal);

    /// <summary>Gets the shared emit of the synthetic assembly.</summary>
    private InMemoryMarkdownWriterFactory Emitted => _emitted ??= Emit(ApiVisibility.All);

    /// <summary>Backing field of <see cref="Emitted"/>.</summary>
    private InMemoryMarkdownWriterFactory? _emitted;

    /// <summary>Initializes a new instance of the <see cref="DotNetSyntheticNamingTests"/> class.</summary>
    public DotNetSyntheticNamingTests()
    {
        Directory.CreateDirectory(_directory);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Adds a public class to <paramref name="module"/>.</summary>
    private static TypeDefinition AddType(ModuleDefinition module, string ns, string name, params string[] genericParameters)
    {
        var type = new TypeDefinition(ns, name, TypeAttributes.Public | TypeAttributes.Class, module.ImportReference(typeof(object)));
        foreach (var parameter in genericParameters)
        {
            type.GenericParameters.Add(new GenericParameter(parameter, type));
        }

        module.Types.Add(type);
        return type;
    }

    /// <summary>Adds a public method (with a trivial body) to <paramref name="type"/>.</summary>
    private static MethodDefinition AddMethod(TypeDefinition type, string name, TypeReference returnType, MethodAttributes extra, params TypeReference[] parameters)
    {
        var method = new MethodDefinition(name, MethodAttributes.Public | MethodAttributes.HideBySig | extra, returnType);
        for (var i = 0; i < parameters.Length; i++)
        {
            method.Parameters.Add(new ParameterDefinition("p" + i, ParameterAttributes.None, parameters[i]));
        }

        method.Body.GetILProcessor().Emit(OpCodes.Ret);
        type.Methods.Add(method);
        return method;
    }

    /// <summary>Adds a public int field to <paramref name="type"/>.</summary>
    private static void AddField(ModuleDefinition module, TypeDefinition type, string name) =>
        type.Fields.Add(new FieldDefinition(name, FieldAttributes.Public, module.ImportReference(typeof(int))));

    /// <summary>Builds the synthetic assembly and returns the (dll, xml) paths.</summary>
    private (string Dll, string Xml) Build()
    {
        var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("Synth", new Version(1, 0)), "Synth", ModuleKind.Dll);
        var m = assembly.MainModule;
        var voidType = m.ImportReference(typeof(void));
        var intType = m.ImportReference(typeof(int));

        // Namespace A.B and type A.B collide on the path A/B
        var typeB = AddType(m, "A", "B");
        var typeC = AddType(m, "A.B", "C");
        AddMethod(typeB, "Get", typeC, 0);
        AddMethod(typeC, "Make", typeB, 0, typeB);
        AddField(m, typeB, "Value");

        // Root namespace named like the reserved index page, with same-named type and member
        var api = AddType(m, "api", "api");
        AddMethod(api, "api", voidType, 0);
        AddType(m, "Other", "Api");
        AddType(m, "Other", "api");

        // Windows device names, with extensions, plus reserved-page names as members
        var con = AddType(m, "Dev", "CON");
        var nul = AddType(m, "Dev", "NUL");
        AddType(m, "Dev", "COM1");
        AddType(m, "Dev", "LPT2.log");
        AddMethod(con, "Use", nul, 0, nul);
        AddField(m, con, "operators");
        AddField(m, con, "Operators");
        AddMethod(con, "op_Addition", con, MethodAttributes.Static | MethodAttributes.SpecialName, con, con);
        AddMethod(con, "op_Custom", intType, 0);
        AddMethod(con, "operator+", intType, 0);
        AddField(m, con, "Nul");

        // Literal braces, square brackets, parentheses and underscores
        AddType(m, "Hostile", "A[B]");
        AddType(m, "Hostile", "A(B)");
        AddType(m, "Hostile", "A{B}");
        AddType(m, "Hostile", "A_B_");
        AddType(m, "Hostile", "a_b_");
        AddType(m, "Hostile", "x.");
        AddType(m, "Hostile", "Foo");
        AddType(m, "Hostile", "Foo1");
        var generic = AddType(m, "Hostile", "Foo`1", "T");
        var inner = new TypeDefinition(string.Empty, "Inner`1", TypeAttributes.NestedPublic | TypeAttributes.Class, m.ImportReference(typeof(object)));
        inner.GenericParameters.Add(new GenericParameter("U", inner));
        generic.NestedTypes.Add(inner);
        AddType(m, "Hostile", "Foo`2", "T1", "T2");

        // Case-twin namespaces, types and members
        AddType(m, "R", "X");
        AddType(m, "R.Twin", "Y");
        AddType(m, "R.twin", "Z");
        var twins = AddType(m, "R", "Members");
        AddType(m, "R", "members");
        AddField(m, twins, "value");
        AddField(m, twins, "Value");

        var dll = Path.Combine(_directory, "Synth.dll");
        assembly.Write(dll);
        var xml = Path.Combine(_directory, "Synth.xml");
        File.WriteAllText(xml, "<doc><assembly><name>Synth</name></assembly><members/></doc>");
        return (dll, xml);
    }

    /// <summary>Emits the synthetic assembly in gradual-disclosure format.</summary>
    private InMemoryMarkdownWriterFactory Emit(ApiVisibility visibility)
    {
        var (dll, xml) = Build();
        var factory = new InMemoryMarkdownWriterFactory();
        var context = new InMemoryContext();
        new DotNetGenerator(new DotNetGeneratorOptions { AssemblyPath = dll, XmlDocPath = xml, Visibility = visibility })
            .Parse(context).Emit(factory, new EmitConfig(), context);
        Assert.Empty(context.Errors);
        return factory;
    }

    /// <summary>Resolves a relative link against the page that contains it.</summary>
    private static string Resolve(string key, string link)
    {
        var folder = key.Contains('/') ? key[..key.LastIndexOf('/')] : string.Empty;
        var path = new Uri(new Uri("http://x/" + (folder.Length > 0 ? folder + "/" : string.Empty)), link).AbsolutePath.TrimStart('/');
        return Uri.UnescapeDataString(path)[..^3];
    }

    /// <summary>Gets every text of a page that may contain a link.</summary>
    private static IEnumerable<string> Texts(InMemoryMarkdownWriter writer) =>
        writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text)
            .Concat(writer.Operations.OfType<TableOperation>().SelectMany(t => t.Rows.SelectMany(r => r)));

    /// <summary>A namespace beats a same-named type: the type becomes B-2 and its children move with it.</summary>
    [Fact]
    public void DotNetSyntheticNaming_NamespaceAndTypeSamePath_NamespaceWinsTypeIsSuffixedAndLinksResolve()
    {
        var keys = AllKeys;

        Assert.Contains("A/B", keys);
        Assert.Contains("A/B/C", keys);
        Assert.Contains("A/B-2", keys);
        Assert.Contains("A/B-2/Get", keys);
        Assert.Contains("A/B-2/Value", keys);
        Assert.DoesNotContain("A/B/Get", keys);

        // The namespace page lists the type under its displaced name and the type page links the namespace's type
        var nsRows = Emitted.Writers["A/B"].Operations.OfType<TableOperation>().SelectMany(t => t.Rows).ToList();
        Assert.Contains(nsRows, r => r[0] == "[C](B/C.md)");
        var rootRows = Emitted.Writers["A"].Operations.OfType<TableOperation>().SelectMany(t => t.Rows).ToList();
        Assert.Contains(rootRows, r => r[0] == "[B](A/B-2.md)");
        Assert.Contains(rootRows, r => r[0] == "[A.B](A/B.md)");
    }

    /// <summary>Hostile type names get exactly the documented legal names, each used once.</summary>
    [Fact]
    public void DotNetSyntheticNaming_HostileTypeNames_GetExactNames()
    {
        var keys = AllKeys;

        // Device names, with and without extensions
        Assert.Contains("Dev/CON_", keys);
        Assert.Contains("Dev/NUL_", keys);
        Assert.Contains("Dev/COM1_", keys);
        Assert.Contains("Dev/LPT2_.log", keys);
        Assert.DoesNotContain("Dev/CON", keys);

        // Literal braces never collide with escaped square brackets or parentheses, nor with underscores that look like escapes
        Assert.Contains("Hostile/A{B}", keys);
        Assert.Contains("Hostile/A_B_", keys);
        Assert.Contains("Hostile/a_b_-2", keys);
        Assert.Contains("Hostile/A_B_-3", keys);
        Assert.Contains("Hostile/A_B_-4", keys);
        Assert.DoesNotContain("Hostile/A_B_-5", keys);
        Assert.Contains("Hostile/x_", keys);

        // Arity in braces never collides with a digit suffix
        Assert.Contains("Hostile/Foo", keys);
        Assert.Contains("Hostile/Foo1", keys);
        Assert.Contains("Hostile/Foo{1}", keys);
        Assert.Contains("Hostile/Foo{2}", keys);
        Assert.Contains("Hostile/Foo{1}/Inner{1}", keys);
    }

    /// <summary>A member named like the operators page is displaced; real operators keep operators.md.</summary>
    [Fact]
    public void DotNetSyntheticNaming_MemberNamedOperators_IsDisplacedAndOperatorCheckIsStrict()
    {
        var keys = AllKeys;

        Assert.Contains("Dev/CON_/operators", keys);
        Assert.Contains("Dev/CON_/Operators-2", keys);
        Assert.Contains("Dev/CON_/operators-3", keys);

        // A regular method named like an operator method is a member, not an operator
        Assert.Contains("Dev/CON_/op_Custom", keys);
        Assert.Contains("Dev/CON_/operator+", keys);

        // operators.md lists the real operator only
        var opRows = Emitted.Writers["Dev/CON_/operators"].Operations.OfType<TableOperation>().SelectMany(t => t.Rows).ToList();
        Assert.Equal(2, opRows.Count);
        Assert.All(opRows, r => Assert.Equal("[CON](../CON_.md)", r[1]));

        // The reserved-page names in the type page link to the displaced pages
        var typeCells = Emitted.Writers["Dev/CON_"].Operations.OfType<TableOperation>().SelectMany(t => t.Rows.SelectMany(r => r)).ToList();
        Assert.Contains(typeCells, c => c.Contains("(CON_/Operators-2.md)", StringComparison.Ordinal));
        Assert.Contains(typeCells, c => c.Contains("(CON_/operators-3.md)", StringComparison.Ordinal));
        Assert.Contains(typeCells, c => c.Contains("(CON_/operators.md)", StringComparison.Ordinal));
    }

    /// <summary>A namespace, type or member named api never overwrites the root api.md.</summary>
    [Fact]
    public void DotNetSyntheticNaming_NamesLikeApi_NeverOverwriteRootIndex()
    {
        var keys = AllKeys;

        Assert.Contains("api", keys);
        Assert.Contains("api-2", keys);
        Assert.Contains("api-2/api", keys);
        Assert.Contains("api-2/api/api", keys);
        Assert.Contains("Other/Api", keys);
        Assert.Contains("Other/api-2", keys);

        // The root index lists the displaced namespace under its real path
        var rows = Emitted.Writers["api"].Operations.OfType<TableOperation>().SelectMany(t => t.Rows).ToList();
        Assert.Contains(rows, r => r[0] == "[api](api-2.md)");
    }

    /// <summary>Case twins in namespaces, types and members each get their own legal path.</summary>
    [Fact]
    public void DotNetSyntheticNaming_CaseTwins_GetSeparatePaths()
    {
        var keys = AllKeys;

        Assert.Contains("R/Twin", keys);
        Assert.Contains("R/Twin/Y", keys);
        Assert.Contains("R/twin-2/Z", keys);
        Assert.Contains("R/Members", keys);
        Assert.Contains("R/members-2", keys);
        Assert.Contains("R/Members/Value", keys);
        Assert.Contains("R/Members/value-2", keys);
        Assert.Equal(keys.Count, keys.Select(k => k.ToLowerInvariant()).Distinct().Count());
    }

    /// <summary>Every path is legal, every link and breadcrumb resolves to a written page at every visibility.</summary>
    /// <param name="visibility">The visibility filter.</param>
    [Theory]
    [InlineData(ApiVisibility.Public)]
    [InlineData(ApiVisibility.PublicAndProtected)]
    [InlineData(ApiVisibility.All)]
    public void DotNetSyntheticNaming_AllVisibilities_PathsAreLegalAndLinksAndBreadcrumbsResolve(ApiVisibility visibility)
    {
        var factory = Emit(visibility);

        var keys = factory.Writers.Keys.ToList();
        Assert.Equal(keys.Count, keys.Select(k => k.ToLowerInvariant()).Distinct().Count());
        Assert.All(keys, k => Assert.All(k.Split('/'), s => Assert.True(FileNameRules.IsLegalSegment(s), k)));

        var checkedLinks = 0;
        var checkedCrumbs = 0;
        foreach (var (key, writer) in factory.Writers)
        {
            foreach (var text in Texts(writer))
            {
                foreach (Match match in LinkPattern.Matches(text))
                {
                    var target = Resolve(key, match.Groups[1].Value);
                    Assert.True(factory.Writers.ContainsKey(target), $"{key} -> {match.Groups[1].Value} resolves to missing {target}");
                    checkedLinks++;
                }
            }

            if (key == "api")
            {
                continue;
            }

            var ops = writer.Operations.ToList();
            var crumb = Assert.IsType<ParagraphOperation>(ops[ops.FindIndex(o => o is HeadingOperation { Level: 1 }) + 1]).Text;
            var first = LinkPattern.Matches(crumb).First().Groups[1].Value;
            Assert.Equal("api", Resolve(key, first));
            checkedCrumbs++;
        }

        Assert.True(checkedLinks > 20);
        Assert.Equal(factory.Writers.Count - 1, checkedCrumbs);
    }

    /// <summary>Links between types use the approved names: displaced type, escaped device name, once-escaped parameters.</summary>
    [Fact]
    public void DotNetSyntheticNaming_TypeLinks_UseApprovedNamesWithoutDoubleEscaping()
    {
        var made = Emitted.Writers["A/B/C/Make"].Operations.OfType<ParagraphOperation>().Select(p => p.Text)
            .Concat(Emitted.Writers["A/B/C/Make"].Operations.OfType<TableOperation>().SelectMany(t => t.Rows.SelectMany(r => r)))
            .Concat(Emitted.Writers["A/B/C/Make"].Operations.OfType<SignatureOperation>().Select(s => s.Code))
            .ToList();
        Assert.Contains(made, t => t.Contains("](../../B-2.md)", StringComparison.Ordinal));

        var use = Emitted.Writers["Dev/CON_/Use"].Operations.OfType<TableOperation>().SelectMany(t => t.Rows.SelectMany(r => r)).ToList();
        Assert.Contains(use, t => t.Contains("](../NUL_.md)", StringComparison.Ordinal));
        Assert.DoesNotContain(AllKeys, k => k.Contains("__", StringComparison.Ordinal));
    }
}
