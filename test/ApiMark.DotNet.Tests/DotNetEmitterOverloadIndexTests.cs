// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>
///     Exact-output tests for the overload index (aggregated summary and return column), overload-specific
///     cross-reference links and anchors, method type-parameter tables, inline code spans for
///     <c>paramref</c>/<c>typeparamref</c>/<c>langword</c>, and example placement on overload pages, in both
///     gradual-disclosure and single-file modes.
/// </summary>
public class DotNetEmitterOverloadIndexTests
{
    /// <summary>The page key of the <c>UndoHistory</c> type page.</summary>
    private const string HistoryKey = "ApiMark.DotNet.Fixtures/UndoHistory";

    /// <summary>The summary text the overload index uses when the overload summaries differ.</summary>
    private const string OverloadedSummary = "Overloaded; see the member page.";

    /// <summary>The escaped heading of the generic <c>Transact</c> overload.</summary>
    private const string GenericHeading = "Transact\\<T\\>(string, Func\\<T\\>)";

    /// <summary>Emits the fixture assembly in gradual-disclosure format.</summary>
    /// <returns>The populated factory.</returns>
    private static InMemoryMarkdownWriterFactory EmitGradual()
    {
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = BuildEmitter();
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());
        return factory;
    }

    /// <summary>Emits the fixture assembly in single-file format.</summary>
    /// <returns>The recorded operations of the single output file.</returns>
    private static List<MarkdownOperation> EmitSingle()
    {
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = BuildEmitter();
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());
        return factory.GetWriter("", "api").Operations.ToList();
    }

    /// <summary>Parses the fixture assembly.</summary>
    /// <returns>The emitter.</returns>
    private static DotNetEmitter BuildEmitter() =>
        (DotNetEmitter)new DotNetGenerator(new DotNetGeneratorOptions
        {
            AssemblyPath = FixturePaths.GetFixtureDll(),
            XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
            Visibility = ApiVisibility.Public,
        }).Parse(new InMemoryContext());

    /// <summary>Returns the first row of any table on a page whose first cell starts with the given text.</summary>
    /// <param name="factory">The factory.</param>
    /// <param name="key">The page key.</param>
    /// <param name="prefix">The expected start of the first cell.</param>
    /// <returns>The matching row.</returns>
    private static string[] Row(InMemoryMarkdownWriterFactory factory, string key, string prefix) =>
        factory.Writers[key].Operations.OfType<TableOperation>().SelectMany(t => t.Rows)
            .Single(r => r[0].StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>Returns the paragraph texts of a page.</summary>
    /// <param name="factory">The factory.</param>
    /// <param name="key">The page key.</param>
    /// <returns>The paragraph texts.</returns>
    private static List<string> Paragraphs(InMemoryMarkdownWriterFactory factory, string key) =>
        factory.Writers[key].Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();

    /// <summary>Computes the GitHub heading slug: lowercase, punctuation dropped, spaces become hyphens.</summary>
    /// <param name="heading">The heading text as written (backslash escapes allowed).</param>
    /// <returns>The anchor slug.</returns>
    private static string Slug(string heading) =>
        Regex.Replace(heading.ToLowerInvariant(), @"[^\p{L}\p{N} _-]", string.Empty).Replace(' ', '-');

    /// <summary>Returns the operations that follow the heading starting with <paramref name="headingStart"/> up to the next heading of the same or higher level.</summary>
    /// <param name="ops">All operations.</param>
    /// <param name="headingStart">The start of the heading text.</param>
    /// <returns>The section body operations.</returns>
    private static List<MarkdownOperation> Section(IReadOnlyList<MarkdownOperation> ops, string headingStart)
    {
        var start = ops.ToList().FindIndex(o => o is HeadingOperation h && h.Text.StartsWith(headingStart, StringComparison.Ordinal));
        Assert.True(start >= 0, $"Heading '{headingStart}' not found");
        var level = ((HeadingOperation)ops[start]).Level;
        return ops.Skip(start + 1).TakeWhile(o => o is not HeadingOperation h || h.Level > level).ToList();
    }

    /// <summary>Validates the index summary of a group with different overload summaries is neutral, not the first overload's text.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_OverloadIndex_DifferingSummaries_UsesNeutralText()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert
        Assert.Equal(OverloadedSummary, Row(factory, "ApiMark.DotNet.Fixtures/ConversionExtensions", "[ToAvalonia (4 overloads)]")[2]);
        Assert.Equal(OverloadedSummary, Row(factory, HistoryKey, "[Transact (2 overloads)]")[2]);
    }

    /// <summary>Validates the index summary of a group whose overloads share one summary is that summary.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_OverloadIndex_IdenticalSummaries_UsesSharedSummary()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert
        Assert.Equal("Clears the history.", Row(factory, HistoryKey, "[Clear (2 overloads)]")[2]);
    }

    /// <summary>Emits the fixture assembly using an XML doc file with the summaries of selected members removed.</summary>
    /// <param name="memberPrefix">The XML doc member id prefix whose summaries are stripped.</param>
    /// <param name="onlyFirst">When true, only the first matching member loses its summary.</param>
    /// <returns>The populated factory.</returns>
    private static InMemoryMarkdownWriterFactory EmitWithoutSummaries(string memberPrefix, bool onlyFirst)
    {
        var doc = System.Xml.Linq.XDocument.Load(FixturePaths.GetFixtureXmlDoc());
        var members = doc.Descendants("member")
            .Where(m => ((string?)m.Attribute("name"))?.StartsWith(memberPrefix, StringComparison.Ordinal) == true)
            .ToList();
        Assert.Equal(2, members.Count);
        foreach (var member in onlyFirst ? members.Take(1) : members)
        {
            member.Element("summary")!.Remove();
        }

        var path = Path.Combine(Path.GetTempPath(), $"apimark-{Guid.NewGuid():N}.xml");
        try
        {
            doc.Save(path);
            var factory = new InMemoryMarkdownWriterFactory();
            var emitter = (DotNetEmitter)new DotNetGenerator(new DotNetGeneratorOptions
            {
                AssemblyPath = FixturePaths.GetFixtureDll(),
                XmlDocPath = path,
                Visibility = ApiVisibility.Public,
            }).Parse(new InMemoryContext());
            new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());
            return factory;
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Validates an overload group where no overload has a summary shows neutral text, not the no-description placeholder.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_OverloadIndex_AllSummariesMissing_UsesNeutralText()
    {
        // Arrange / Act
        var factory = EmitWithoutSummaries("M:ApiMark.DotNet.Fixtures.UndoHistory.Clear", onlyFirst: false);

        // Assert
        Assert.Equal(OverloadedSummary, Row(factory, HistoryKey, "[Clear (2 overloads)]")[2]);
    }

    /// <summary>Validates an overload group where only some overloads lack a summary shows neutral text.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_OverloadIndex_SomeSummariesMissing_UsesNeutralText()
    {
        // Arrange / Act
        var factory = EmitWithoutSummaries("M:ApiMark.DotNet.Fixtures.UndoHistory.Clear", onlyFirst: true);

        // Assert
        Assert.Equal(OverloadedSummary, Row(factory, HistoryKey, "[Clear (2 overloads)]")[2]);
    }

    /// <summary>Validates the return column lists every distinct return type, including <c>void</c>, in overload order.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_OverloadIndex_ReturnColumn_ListsDistinctTypesIncludingVoid()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert: overloads are ordered by parameter type name (Byte, Int32, Int64, Single)
        Assert.Equal(
            "[Color](Color.md), [Point](Point.md), [Size](Size.md), [Rect](Rect.md)",
            Row(factory, "ApiMark.DotNet.Fixtures/ConversionExtensions", "[ToAvalonia (4 overloads)]")[1]);
        Assert.Equal("void, T", Row(factory, HistoryKey, "[Transact (2 overloads)]")[1]);
        Assert.Equal("void", Row(factory, HistoryKey, "[Clear (2 overloads)]")[1]);
    }

    /// <summary>Validates the exact headings of the Transact overload page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_OverloadPage_HasExactHeadings()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert
        Assert.Equal(
            new[] { "Transact", "Transact(string, Action)", GenericHeading },
            factory.Writers[HistoryKey + "/Transact"].Operations.OfType<HeadingOperation>().Select(h => h.Text));
    }

    /// <summary>Validates the example sits under the generic overload's own heading and nowhere else (gradual disclosure).</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_OverloadPage_ExampleOnlyUnderGenericHeading()
    {
        // Arrange / Act
        var factory = EmitGradual();
        var ops = factory.Writers[HistoryKey + "/Transact"].Operations;

        // Assert
        Assert.DoesNotContain(Section(ops, "Transact(string, Action)"), o => o is CodeBlockOperation);
        var block = Assert.Single(Section(ops, "Transact\\<T\\>"), o => o is CodeBlockOperation);
        Assert.Equal(new CodeBlockOperation("csharp", "var count = history.Transact(\"count\", () => 3);"), block);
    }

    /// <summary>Validates the example sits under the generic overload's own heading and nowhere else (single file).</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_Overloads_ExampleOnlyUnderGenericSection()
    {
        // Arrange / Act
        var ops = EmitSingle();

        // Assert
        var generic = Section(ops, "Transact\\<T\\>(string");
        var block = Assert.Single(generic, o => o is CodeBlockOperation { Language: "csharp" });
        Assert.Equal("var count = history.Transact(\"count\", () => 3);", ((CodeBlockOperation)block).Code);
        Assert.DoesNotContain(Section(ops, "Transact(string, Action)"), o => o is CodeBlockOperation { Language: "csharp" });
    }

    /// <summary>Validates the generic overload documents its type parameter in a table on the overload page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_OverloadPage_GenericOverloadHasTypeParameterTable()
    {
        // Arrange / Act
        var factory = EmitGradual();
        var section = Section(factory.Writers[HistoryKey + "/Transact"].Operations, "Transact\\<T\\>");

        // Assert
        var table = Assert.Single(section.OfType<TableOperation>(), t => t.Headers[0] == "Type Parameter");
        Assert.Equal(new[] { "Type Parameter", "Description" }, table.Headers);
        var row = Assert.Single(table.Rows);
        Assert.Equal(new[] { "T", "The result type; `T` may be a reference type." }, row);
        Assert.DoesNotContain(Section(factory.Writers[HistoryKey + "/Transact"].Operations, "Transact(string, Action)"), o => o is TableOperation t && t.Headers[0] == "Type Parameter");
    }

    /// <summary>Validates the generic overload documents its type parameter in a table in single-file output.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_GenericOverload_HasTypeParameterTable()
    {
        // Arrange / Act
        var section = Section(EmitSingle(), "Transact\\<T\\>(string");

        // Assert
        var table = Assert.Single(section.OfType<TableOperation>(), t => t.Headers[0] == "Type Parameter");
        Assert.Equal(new[] { "T", "The result type; `T` may be a reference type." }, Assert.Single(table.Rows));
    }

    /// <summary>Validates overload-specific crefs keep the signature in the link text and link to the overload's heading anchor.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_OverloadCref_LinksToOverloadHeadingAnchor()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert: same-page-group link from the generic overload to the void overload
        var transact = Section(factory.Writers[HistoryKey + "/Transact"].Operations, "Transact\\<T\\>").OfType<ParagraphOperation>().Select(Flatten).ToList();
        Assert.Contains(
            "Runs `action` as one undoable step and returns its result; use [`UndoHistory.Transact(string, Action)`](Transact.md#transactstring-action) when no result is needed.",
            transact);

        // Assert: cross-type link to an overload, and a link to the generic overload
        Assert.Contains(
            "Converts using [`ConversionExtensions.ToAvalonia(int)`](../ConversionExtensions/ToAvalonia.md#toavaloniaint) and then calls [`UndoHistory.Transact<T>(string, Func<T>)`](Transact.md#transacttstring-funct).",
            Paragraphs(factory, HistoryKey + "/Convert").Select(p => p.Replace('\n', ' ')));
    }

    /// <summary>Flattens the source line breaks of a paragraph into spaces.</summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <returns>The single-line text.</returns>
    private static string Flatten(ParagraphOperation paragraph) => paragraph.Text.Replace('\n', ' ');

    /// <summary>Validates every anchor in a generated link equals the slug of a real heading on the linked page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_AllLinkAnchors_MatchRealHeadings()
    {
        // Arrange / Act
        var factory = EmitGradual();
        var linkPattern = new Regex(@"\]\(([^)#\s]+\.md)#([^)\s]+)\)");
        var checkedAnchors = 0;

        // Assert
        foreach (var (key, writer) in factory.Writers)
        {
            var texts = writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text)
                .Concat(writer.Operations.OfType<TableOperation>().SelectMany(t => t.Rows).SelectMany(r => r));
            foreach (var match in texts.SelectMany(t => linkPattern.Matches(t).Cast<Match>()))
            {
                var folder = key.Contains('/') ? key[..key.LastIndexOf('/')] : string.Empty;
                var target = new Uri(new Uri("http://x/" + folder + "/"), match.Groups[1].Value).AbsolutePath.TrimStart('/');
                target = target[..^3];
                Assert.True(factory.Writers.TryGetValue(target, out var targetWriter), $"{key} links to missing page {target}");
                var slugs = targetWriter!.Operations.OfType<HeadingOperation>().Select(h => Slug(h.Text)).ToList();
                Assert.Contains(match.Groups[2].Value, slugs);
                checkedAnchors++;
            }
        }

        Assert.True(checkedAnchors >= 3, "Expected at least the three anchors of the UndoHistory fixtures");
    }

    /// <summary>Validates parameter, type-parameter, and keyword references render as code spans in every prose location (gradual disclosure).</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_InlineReferences_RenderAsCodeSpansEverywhere()
    {
        // Arrange / Act
        var factory = EmitGradual();
        const string key = "ApiMark.DotNet.Fixtures/InlineCodeDocs/Format";
        var ops = factory.Writers[key].Operations;

        // Assert
        var paragraphs = Paragraphs(factory, key);
        Assert.Contains("Formats `value` of type `T`, or `null`.", paragraphs);
        Assert.Contains("**Returns:** The text, or `null` when `value` is empty.", paragraphs);
        Assert.Contains("Passing `null` for `name` throws.", paragraphs);
        Assert.Contains("Call with `name` set to `null` to see the exception.", paragraphs);
        var tables = ops.OfType<TableOperation>().ToList();
        Assert.Contains(new[] { "T", "The value type; `value` has type `T`." }, tables.SelectMany(t => t.Rows));
        Assert.Contains(tables.SelectMany(t => t.Rows), r => r[0] == "name" && r[^1] == "The label; `null` is not allowed.");
        Assert.Contains(tables.SelectMany(t => t.Rows), r => r[0] == "value" && r[^1] == "The value of type `T` for `name`.");
        Assert.Contains(new[] { "ArgumentNullException", "Thrown when `name` is `null`." }, tables.SelectMany(t => t.Rows));
    }

    /// <summary>Validates parameter, type-parameter, and keyword references render as code spans in every prose location (single file).</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_InlineReferences_RenderAsCodeSpansEverywhere()
    {
        // Arrange / Act
        var ops = EmitSingle();
        var section = Section(ops, "Format\\<T\\>");

        // Assert
        var paragraphs = section.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains("Formats `value` of type `T`, or `null`.", paragraphs);
        Assert.Contains("**Returns:** The text, or `null` when `value` is empty.", paragraphs);
        var rows = section.OfType<TableOperation>().SelectMany(t => t.Rows).ToList();
        Assert.Contains(new[] { "T", "The value type; `value` has type `T`." }, rows);
        Assert.Contains(new[] { "ArgumentNullException", "Thrown when `name` is `null`." }, rows);
    }
}
