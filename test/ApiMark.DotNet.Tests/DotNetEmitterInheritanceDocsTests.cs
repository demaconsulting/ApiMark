// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>
///     Exact-output regression tests for inherited-member listings, unresolved
///     <c>&lt;inheritdoc/&gt;</c> placeholders, record primary-constructor summaries and external
///     <c>&lt;see cref&gt;</c> linking in the gradual-disclosure and single-file emitters.
/// </summary>
public class DotNetEmitterInheritanceDocsTests
{
    private const string Ns = "ApiMark.DotNet.Fixtures";

    /// <summary>Builds generator options for the fixture assembly.</summary>
    /// <param name="withReferences">Whether the external fixture assembly is supplied as a reference.</param>
    /// <param name="externalLinks">Optional external link mappings.</param>
    /// <returns>The options.</returns>
    private static DotNetGeneratorOptions Options(bool withReferences = false, IReadOnlyDictionary<string, string>? externalLinks = null)
    {
        var options = new DotNetGeneratorOptions
        {
            AssemblyPath = FixturePaths.GetFixtureDll(),
            XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
            Visibility = ApiVisibility.Public,
        };
        if (withReferences)
        {
            options.ReferencePaths = [FixturePaths.GetExternalFixtureDll()];
        }

        if (externalLinks != null)
        {
            options.ExternalLinks = externalLinks;
        }

        return options;
    }

    /// <summary>Emits the fixture assembly in gradual-disclosure format.</summary>
    /// <param name="options">The generator options.</param>
    /// <returns>The populated factory.</returns>
    private static InMemoryMarkdownWriterFactory EmitGradual(DotNetGeneratorOptions options)
    {
        var factory = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(options).Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());
        return factory;
    }

    /// <summary>Emits the fixture assembly in single-file format.</summary>
    /// <param name="options">The generator options.</param>
    /// <returns>The single output writer.</returns>
    private static InMemoryMarkdownWriter EmitSingle(DotNetGeneratorOptions options)
    {
        var factory = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(options).Parse(new InMemoryContext()).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());
        return factory.GetWriter(string.Empty, "api");
    }

    /// <summary>Returns the rows of every table in a writer.</summary>
    /// <param name="writer">The writer.</param>
    /// <returns>All table rows.</returns>
    private static List<string[]> Rows(InMemoryMarkdownWriter writer) =>
        writer.Operations.OfType<TableOperation>().SelectMany(t => t.Rows).ToList();

    /// <summary>Returns all paragraph and table-cell text of a writer.</summary>
    /// <param name="writer">The writer.</param>
    /// <returns>The concatenated text.</returns>
    private static string Text(InMemoryMarkdownWriter writer) =>
        string.Join("\n", Paragraphs(writer).Concat(Rows(writer).Select(r => string.Join(" | ", r))));

    /// <summary>Returns the heading texts with their markdown hashes.</summary>
    /// <param name="writer">The writer.</param>
    /// <returns>The headings.</returns>
    private static List<string> Headings(InMemoryMarkdownWriter writer) =>
        writer.Operations.OfType<HeadingOperation>().Select(h => new string('#', h.Level) + " " + h.Text).ToList();

    /// <summary>Returns the paragraph texts.</summary>
    /// <param name="writer">The writer.</param>
    /// <returns>The paragraphs.</returns>
    private static List<string> Paragraphs(InMemoryMarkdownWriter writer) =>
        writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();

    /// <summary>Returns the inherited-member tables (Member/Kind headers) of a writer.</summary>
    /// <param name="writer">The writer.</param>
    /// <returns>The table operations.</returns>
    private static List<TableOperation> InheritedTables(InMemoryMarkdownWriter writer) =>
        writer.Operations.OfType<TableOperation>().Where(t => t.Headers.SequenceEqual(new[] { "Member", "Kind" })).ToList();

    /// <summary>Validates inherited members are listed per declaring type, minus overridden and Object members.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_InheritedMembers_ListedPerDeclaringTypeWithoutOverridesOrObjectMembers()
    {
        // Arrange / Act
        var page = EmitGradual(Options()).Writers[$"{Ns}/InheritedLeaf"];

        // Assert
        Assert.Contains("## Inherited Members", Headings(page));
        var inherited = Headings(page).Where(h => h.StartsWith("### Inherited from", StringComparison.Ordinal)).ToList();
        Assert.Equal(
            ["### Inherited from [InheritedMiddle](InheritedMiddle.md)", "### Inherited from [InheritedGrandBase](InheritedGrandBase.md)"],
            inherited);
        var tables = InheritedTables(page);
        Assert.Equal(2, tables.Count);
        Assert.Equal(new[] { "Shared", "MiddleMethod" }.Order(), tables[0].Rows.Select(r => r[0]).Order());
        Assert.Equal(new[] { "GrandValue", "GrandMethod" }, tables[1].Rows.Select(r => r[0]));
        Assert.Equal(new[] { "Property", "Method" }, tables[1].Rows.Select(r => r[1]));
    }

    /// <summary>Validates inherited members do not become members of the leaf type's own pages.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_InheritedMembers_DoNotCreateMemberPages()
    {
        // Arrange / Act
        var factory = EmitGradual(Options());

        // Assert
        Assert.Contains($"{Ns}/InheritedLeaf/LeafMethod", factory.Writers.Keys);
        Assert.DoesNotContain($"{Ns}/InheritedLeaf/GrandMethod", factory.Writers.Keys);
        Assert.DoesNotContain($"{Ns}/InheritedLeaf/MiddleMethod", factory.Writers.Keys);
    }

    /// <summary>Validates the single-file emitter lists inherited members too.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_InheritedMembers_ListedPerDeclaringType()
    {
        // Arrange / Act
        var writer = EmitSingle(Options());

        // Assert
        Assert.Contains(Paragraphs(writer), p => p.StartsWith("**Inherited from", StringComparison.Ordinal) && p.Contains("InheritedMiddle", StringComparison.Ordinal));
        Assert.Contains(Rows(writer), r => r[0] == "GrandMethod" && r[1] == "Method");
        Assert.Contains(Rows(writer), r => r[0] == "MiddleMethod" && r[1] == "Method");
    }

    /// <summary>Validates an unresolvable base assembly produces a note instead of an error.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_UnresolvableBase_RendersNoteAndDoesNotThrow()
    {
        // Arrange: synthesize an assembly whose only type derives from a base in a missing assembly
        var directory = Path.Combine(Path.GetTempPath(), $"unresolved-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var dll = Path.Combine(directory, "Orphans.dll");
            var module = Mono.Cecil.AssemblyDefinition.CreateAssembly(new Mono.Cecil.AssemblyNameDefinition("Orphans", new Version(1, 0)), "Orphans", Mono.Cecil.ModuleKind.Dll);
            var missing = new Mono.Cecil.AssemblyNameReference("Missing.Lib", new Version(1, 0));
            var baseRef = new Mono.Cecil.TypeReference("Missing", "MissingBase", module.MainModule, missing);
            var orphan = new Mono.Cecil.TypeDefinition("Orphans", "Orphan", Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Class, baseRef);
            module.MainModule.Types.Add(orphan);
            module.Write(dll);
            var xml = Path.Combine(directory, "Orphans.xml");
            File.WriteAllText(xml, "<?xml version=\"1.0\"?><doc><assembly><name>Orphans</name></assembly><members /></doc>");
            var options = new DotNetGeneratorOptions { AssemblyPath = dll, XmlDocPath = xml, Visibility = ApiVisibility.Public };
            // Act
            var page = EmitGradual(options).Writers["Orphans/Orphan"];

            // Assert
            Assert.Contains("## Inherited Members", Headings(page));
            Assert.Contains(
                Paragraphs(page),
                p => p.Contains("could not be resolved", StringComparison.Ordinal) && p.Contains("--reference-paths", StringComparison.Ordinal));
            Assert.Empty(InheritedTables(page));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>Validates a resolvable external base lists its public members.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ExternalBaseWithReferences_ListsExternalMembers()
    {
        // Arrange / Act
        var page = EmitGradual(Options(withReferences: true)).Writers[$"{Ns}/InheritedFromExternal"];

        // Assert (Public visibility: the protected BaseCounter field is not listed)
        var table = Assert.Single(InheritedTables(page));
        Assert.Equal(new[] { "DescribeBase" }, table.Rows.Select(r => r[0]));
        Assert.Equal(new[] { "Method" }, table.Rows.Select(r => r[1]));
        Assert.Contains("### Inherited from ExternalBaseClass", Headings(page));
    }

    /// <summary>Validates inherited members are not counted by documentation coverage.</summary>
    [Fact]
    public void DocumentationCoverageChecker_Check_InheritedMembers_AreNotChecked()
    {
        // Arrange
        var emitter = (DotNetEmitter)new DotNetGenerator(Options()).Parse(new InMemoryContext());

        // Assert: the member page index holds only declared members of the leaf
        Assert.Contains(emitter.Model.MemberPageIndex.Keys, k => k.Contains("InheritedLeaf.LeafMethod", StringComparison.Ordinal));
        Assert.DoesNotContain(emitter.Model.MemberPageIndex.Keys, k => k.Contains("InheritedLeaf.GrandMethod", StringComparison.Ordinal));
    }

    /// <summary>Validates an unresolved inheritdoc reads as a placeholder only through the effective-summary API.</summary>
    [Fact]
    public void XmlDocReader_GetEffectiveSummary_UnresolvedInheritDoc_ReturnsPlaceholder()
    {
        // Arrange
        const string id = "M:ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass.Compute(System.Int32)";
        var xmlPath = Path.Combine(Path.GetTempPath(), $"inheritdoc-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(
                xmlPath,
                $"<?xml version=\"1.0\"?><doc><assembly><name>X</name></assembly><members><member name=\"{id}\"><inheritdoc/></member></members></doc>");
            var xmlDocs = new XmlDocReader(xmlPath);

            // Act / Assert
            Assert.True(xmlDocs.HasInheritDoc(id));
            Assert.Null(xmlDocs.GetSummary(id));
            Assert.Equal("Inherits documentation from base member.", xmlDocs.GetEffectiveSummary(id));
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }
    /// <summary>Validates an unresolved inheritdoc renders the placeholder; a resolved one renders real text.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_InheritDoc_UnresolvedRendersPlaceholderResolvedRendersText()
    {
        // Arrange / Act
        var unresolved = EmitGradual(Options()).Writers[$"{Ns}/ExternalInheritDocClass"];
        var resolved = EmitGradual(Options(withReferences: true)).Writers[$"{Ns}/ExternalInheritDocClass"];

        // Assert
        var unresolvedRow = Rows(unresolved).Single(r => r[0].Contains("ExternalInterfaceMethod", StringComparison.Ordinal));
        Assert.Contains("Inherits documentation from base member.", unresolvedRow);
        Assert.DoesNotContain("No description provided", string.Join(' ', unresolvedRow), StringComparison.Ordinal);
        var resolvedRow = Rows(resolved).Single(r => r[0].Contains("ExternalInterfaceMethod", StringComparison.Ordinal));
        Assert.Contains("Performs the external interface method's action.", resolvedRow);
    }

    /// <summary>Validates external inheritdoc (one and two hops) resolves from the real reference XML in single-file output.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_ExternalInheritDoc_ResolvesOneAndTwoHopsFromReferenceXml()
    {
        // Arrange / Act
        var resolved = Text(EmitSingle(Options(withReferences: true)));
        var unresolved = Text(EmitSingle(Options()));

        // Assert
        Assert.Contains("Describes the base implementation.", resolved);
        Assert.Contains("Describes the grandparent implementation.", resolved);
        Assert.DoesNotContain("Describes the grandparent implementation.", unresolved);
    }

    /// <summary>Validates the positional record primary constructor gets the standard summary.</summary>
    /// <param name="typeName">The record type.</param>
    /// <param name="expected">The expected constructor summary.</param>
    [Theory]
    [InlineData("PositionalRecord", "Initializes a new instance of the `PositionalRecord` record.")]
    [InlineData("PositionalRecordStruct", "Initializes a new instance of the `PositionalRecordStruct` record struct.")]
    public void DotNetEmitterGradualDisclosure_Emit_RecordPrimaryConstructor_UsesStandardSummary(string typeName, string expected)
    {
        // Arrange / Act
        var page = EmitGradual(Options()).Writers[$"{Ns}/{typeName}"];

        // Assert
        var rows = Rows(page).Where(r => r[0].Contains(typeName, StringComparison.Ordinal) && r.Length > 1).ToList();
        Assert.Contains(rows, r => r.Any(c => c == expected));
    }

    /// <summary>Validates explicit differing constructor documentation is preserved.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_RecordExplicitConstructor_PreservesSummary()
    {
        // Arrange / Act
        var factory = EmitGradual(Options());

        // Assert
        var page = factory.Writers[$"{Ns}/RecordWithExplicitConstructor"];
        Assert.Contains(Rows(page), r => r.Any(c => c == "Creates the record with an explicit, differing constructor summary."));
        var extra = factory.Writers[$"{Ns}/RecordWithExtraConstructor/RecordWithExtraConstructor"];
        Assert.Contains(Paragraphs(extra), p => p == "Initializes a new instance of the `RecordWithExtraConstructor` record.");
        extra = factory.Writers[$"{Ns}/RecordWithExtraConstructor"];
        Assert.Contains(Rows(extra), r => r.Any(c => c == "Overloaded; see the member page."));
    }

    /// <summary>Validates the single-file emitter uses the record primary-constructor summary.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_RecordPrimaryConstructor_UsesStandardSummary()
    {
        // Arrange / Act
        var writer = EmitSingle(Options());

        // Assert
        Assert.Contains(Paragraphs(writer).Concat(Rows(writer).SelectMany(r => r)), t => t == "Initializes a new instance of the `PositionalRecord` record.");
    }

    /// <summary>Validates an external cref is linked to the mapped documentation using a relative path.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ExternalCref_MappedAssemblyLinksType()
    {
        // Arrange
        var links = new Dictionary<string, string> { ["ApiMark.DotNet.Fixtures.External"] = "../OtherLib/api" };

        // Act
        var page = EmitGradual(Options(withReferences: true, externalLinks: links)).Writers[$"{Ns}/ExternalCrefClass"];

        // Assert
        var text = string.Join('\n', Rows(page).SelectMany(r => r).Concat(Paragraphs(page)));
        Assert.Contains("[`ExternalBaseClass`](../../OtherLib/api/ApiMark.DotNet.Fixtures.External/ExternalBaseClass.md)", text, StringComparison.Ordinal);
        Assert.Contains("[`ExternalBaseClass.DescribeBase`](../../OtherLib/api/ApiMark.DotNet.Fixtures.External/ExternalBaseClass.md)", text, StringComparison.Ordinal);
        Assert.Contains("`string`", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[`string`]", text, StringComparison.Ordinal);
    }

    /// <summary>Validates an absolute mapping yields an absolute external cref link.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ExternalCref_AbsoluteMappingLinksType()
    {
        // Arrange
        var links = new Dictionary<string, string> { ["ApiMark.DotNet.Fixtures.External"] = "https://example.com/other/api" };

        // Act
        var page = EmitGradual(Options(withReferences: true, externalLinks: links)).Writers[$"{Ns}/ExternalCrefClass"];

        // Assert
        var text = string.Join('\n', Rows(page).SelectMany(r => r).Concat(Paragraphs(page)));
        Assert.Contains("[`ExternalBaseClass`](https://example.com/other/api/ApiMark.DotNet.Fixtures.External/ExternalBaseClass.md)", text, StringComparison.Ordinal);
    }

    /// <summary>Validates an unmapped external cref stays a plain code span.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ExternalCref_UnmappedStaysPlainCodeSpan()
    {
        // Arrange / Act
        var page = EmitGradual(Options(withReferences: true)).Writers[$"{Ns}/ExternalCrefClass"];

        // Assert
        var text = string.Join('\n', Rows(page).SelectMany(r => r).Concat(Paragraphs(page)));
        Assert.Contains("`ExternalBaseClass`", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[`ExternalBaseClass`]", text, StringComparison.Ordinal);
    }
}
