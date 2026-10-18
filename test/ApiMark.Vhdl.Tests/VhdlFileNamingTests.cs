// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.Vhdl.VhdlAst;
using Xunit;

namespace ApiMark.Vhdl.Tests;

/// <summary>
///     Tests for the symbol-name registry applied to VHDL output: exact-name rules, every collision scenario,
///     and whole-output properties (unique, legal names whose links all resolve).
/// </summary>
public partial class VhdlFileNamingTests
{
    /// <summary>Matches a Markdown link destination ending in <c>.md</c>.</summary>
    [GeneratedRegex(@"\]\(([^)\s#]+\.md)(?:#[^)\s]*)?\)")]
    private static partial Regex LinkPattern();

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

    /// <summary>Builds an entity without documentation.</summary>
    private static VhdlEntityDecl Entity(string name) => new(name, [], [], null);

    /// <summary>Builds a function subprogram without documentation.</summary>
    private static VhdlSubprogramDecl Func(string name) =>
        new(name, VhdlSubprogramKind.Function, $"function {name} return integer", [], "integer", null);

    /// <summary>Builds a package without documentation.</summary>
    private static VhdlPackageDecl Pkg(string name, params VhdlSubprogramDecl[] subprograms) =>
        new(name, null, [], [], [], subprograms);

    /// <summary>Emits the models into a recording factory.</summary>
    private static RecordingFactory Emit(params VhdlFileModel[] models)
    {
        var emitter = new VhdlEmitter(new VhdlGeneratorOptions { LibraryName = "TestLib" }, models);
        var factory = new RecordingFactory();
        emitter.Emit(factory, new EmitConfig(), new InMemoryContext());
        return factory;
    }

    /// <summary>Asserts that every page is unique case-insensitively and legal, and that every link resolves.</summary>
    private static int AssertUniqueLegalAndLinked(RecordingFactory factory)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var request in factory.Requests)
        {
            Assert.True(seen.Add(request), $"Page '{request}' was requested more than once (case-insensitively)");
            Assert.All(request.Split('/'), s => Assert.True(FileNameRules.IsLegalSegment(s), $"Illegal segment '{s}' in '{request}'"));
        }

        var checkedLinks = 0;
        foreach (var (key, writer) in factory.Writers)
        {
            var slash = key.LastIndexOf('/');
            var folder = slash < 0 ? string.Empty : key[..slash];
            var texts = writer.Operations.OfType<TableOperation>().SelectMany(t => t.Rows).SelectMany(r => r)
                .Concat(writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text));
            foreach (var text in texts)
            {
                foreach (Match match in LinkPattern().Matches(text))
                {
                    var target = Path.GetRelativePath(".", Path.Join(folder, match.Groups[1].Value)).Replace('\\', '/');
                    target = target[..^".md".Length];
                    Assert.True(factory.Writers.ContainsKey(target), $"Link '{match.Groups[1].Value}' on '{key}' resolves to missing page '{target}'");
                    checkedLinks++;
                }
            }
        }

        return checkedLinks;
    }

    /// <summary>Plain names are used unchanged.</summary>
    [Fact]
    public void VhdlNames_PlainNames_AreUnchanged()
    {
        // Arrange
        var entity = Entity("Counter");
        var package = Pkg("Types", Func("Do_It"));

        // Act
        var names = VhdlNames.Build([new VhdlFileModel("a.vhd", [entity], [], [package])]);

        // Assert
        Assert.Equal("api", names.ApiPath());
        Assert.Equal("Counter", names.Path(entity));
        Assert.Equal("Types", names.Path(package));
        Assert.Equal("Types/Do_It", names.Path(package.Subprograms[0]));
        Assert.Equal("Types", names.Directory(package.Subprograms[0]));
    }

    /// <summary>An entity keeps the plain name; a package of the same name is displaced with a suffix and carries its subprograms.</summary>
    [Fact]
    public void VhdlNames_EntityAndPackageSameName_EntityWins()
    {
        // Arrange
        var entity = Entity("Shared");
        var package = Pkg("Shared", Func("Run"));

        // Act
        var factory = Emit(new VhdlFileModel("a.vhd", [entity], [], [package]));

        // Assert
        Assert.Contains("Shared", factory.Requests);
        Assert.Contains("Shared-2", factory.Requests);
        Assert.Contains("Shared-2/Run", factory.Requests);
        Assert.True(AssertUniqueLegalAndLinked(factory) >= 3);
    }

    /// <summary>An entity and a package that differ only by case are the same VHDL name and must not overwrite each other.</summary>
    [Fact]
    public void VhdlNames_EntityAndPackageCaseTwins_AreSeparated()
    {
        // Arrange
        var entity = Entity("Mix");
        var package = Pkg("MIX");

        // Act
        var factory = Emit(new VhdlFileModel("a.vhd", [entity], [], [package]));

        // Assert
        Assert.Contains("Mix", factory.Requests);
        Assert.Contains("MIX-2", factory.Requests);
        AssertUniqueLegalAndLinked(factory);
    }

    /// <summary>Entities that differ only by case are separated; the first in ordinal order keeps the plain name.</summary>
    [Fact]
    public void VhdlNames_CaseTwinEntities_SecondGetsSuffix()
    {
        // Arrange and Act
        var factory = Emit(new VhdlFileModel("a.vhd", [Entity("foo"), Entity("Foo")], [], []));

        // Assert
        Assert.Contains("Foo", factory.Requests);
        Assert.Contains("foo-2", factory.Requests);
        AssertUniqueLegalAndLinked(factory);
    }

    /// <summary>The same entity name declared in two files yields two distinct pages.</summary>
    [Fact]
    public void VhdlNames_DuplicateEntityAcrossFiles_GetsDistinctPages()
    {
        // Arrange and Act
        var factory = Emit(
            new VhdlFileModel("a.vhd", [Entity("Dup")], [], []),
            new VhdlFileModel("b.vhd", [Entity("Dup")], [], []));

        // Assert
        Assert.Contains("Dup", factory.Requests);
        Assert.Contains("Dup-2", factory.Requests);
        AssertUniqueLegalAndLinked(factory);
    }

    /// <summary>Entities and packages named like the reserved index page never overwrite it.</summary>
    [Fact]
    public void VhdlNames_ReservedApiName_KeepsIndexPage()
    {
        // Arrange and Act
        var factory = Emit(new VhdlFileModel("a.vhd", [Entity("API")], [], [Pkg("api")]));

        // Assert
        Assert.Equal(1, factory.Requests.Count(r => string.Equals(r, "api", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("api", factory.Requests);
        Assert.Contains("API-2", factory.Requests);
        Assert.Contains("api-3", factory.Requests);
        AssertUniqueLegalAndLinked(factory);
    }

    /// <summary>Overloaded and case-twin subprograms each keep a page in the package folder.</summary>
    [Fact]
    public void VhdlNames_OverloadedAndCaseTwinSubprograms_GetDistinctPages()
    {
        // Arrange and Act
        var factory = Emit(new VhdlFileModel("a.vhd", [], [], [Pkg("P", Func("Add"), Func("Add"), Func("add"))]));

        // Assert
        Assert.Contains("P/Add", factory.Requests);
        Assert.Contains("P/Add-2", factory.Requests);
        Assert.Contains("P/add-3", factory.Requests);
        Assert.True(AssertUniqueLegalAndLinked(factory) >= 3);
    }

    /// <summary>Names with illegal characters are escaped exactly once.</summary>
    [Fact]
    public void VhdlNames_IllegalAndDeviceNames_AreEscaped()
    {
        // Arrange and Act
        var factory = Emit(new VhdlFileModel("a.vhd", [Entity("CON"), Entity("a/b")], [], [Pkg("Pk?", Func("op\"+\""))]));

        // Assert
        Assert.Contains("CON_", factory.Requests);
        Assert.Contains("a_b", factory.Requests);
        Assert.Contains("Pk_", factory.Requests);
        Assert.Contains("Pk_/op_+_", factory.Requests);
        AssertUniqueLegalAndLinked(factory);
    }

    /// <summary>
    ///     An entity <c>Api</c>, a package <c>API</c> and a package <c>Aux</c> never overwrite the index page; the
    ///     package page, its folder and every subprogram link all use the same resolved name.
    /// </summary>
    [Fact]
    public void VhdlNames_EntityApiPackageApiAndAux_ShareOneResolvedNamePerSymbol()
    {
        // Arrange and Act
        var factory = Emit(new VhdlFileModel("a.vhd", [Entity("Api")], [], [Pkg("API", Func("Run"), Func("Aux")), Pkg("Aux", Func("Aux"))]));

        // Assert: the reserved index keeps api; the entity then beats the package of the same name
        Assert.Equal(
            ["API-3", "API-3/Aux_", "API-3/Run", "Api-2", "Aux_", "Aux_/Aux_", "api"],
            factory.Requests.OrderBy(r => r, StringComparer.Ordinal).ToList());

        // The index links every symbol by its resolved name
        var index = factory.Writers["api"].Operations.OfType<TableOperation>().SelectMany(t => t.Rows).Select(r => r[0]).ToList();
        Assert.Contains("[Api](Api-2.md)", index);
        Assert.Contains("[API](API-3.md)", index);
        Assert.Contains("[Aux](Aux_.md)", index);

        // The package page links its subprograms into the folder that carries the same resolved name
        var paragraphs = factory.Writers["API-3"].Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains("**[Run](API-3/Run.md)** — *Function*", paragraphs);
        Assert.Contains("**[Aux](API-3/Aux_.md)** — *Function*", paragraphs);
        Assert.True(AssertUniqueLegalAndLinked(factory) >= 5);
    }

    /// <summary>Names are the same for every run.</summary>
    [Fact]
    public void VhdlNames_SameModel_IsDeterministic()
    {
        // Arrange
        var model = new VhdlFileModel("a.vhd", [Entity("B"), Entity("b")], [], [Pkg("B", Func("x"), Func("X"))]);

        // Act
        var first = Emit(model).Requests;
        var second = Emit(model).Requests;

        // Assert
        Assert.Equal(first, second);
    }

    /// <summary>Every page generated from the VHDL fixtures is unique, legal, and every link resolves.</summary>
    [Fact]
    public void VhdlFileNaming_Fixtures_AllPagesUniqueLegalAndLinked()
    {
        // Arrange
        var options = new VhdlGeneratorOptions
        {
            LibraryName = "Fixtures",
            Sources = [FixturePaths.MuxVhd, FixturePaths.CounterVhd, FixturePaths.CommonTypesVhd],
        };
        var context = new InMemoryContext();
        var emitter = new VhdlGenerator(options).Parse(context);
        var factory = new RecordingFactory();

        // Act
        emitter.Emit(factory, new EmitConfig(), context);

        // Assert
        Assert.Empty(context.Errors);
        Assert.True(AssertUniqueLegalAndLinked(factory) > 0);
    }
}
