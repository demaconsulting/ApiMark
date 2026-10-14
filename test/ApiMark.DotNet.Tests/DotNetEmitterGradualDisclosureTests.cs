// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Unit tests for <see cref="DotNetEmitterGradualDisclosure"/>.</summary>
public class DotNetEmitterGradualDisclosureTests
{
    /// <summary>Builds DotNetGeneratorOptions pointing at the fixture assembly.</summary>
    private static DotNetGeneratorOptions BuildOptions() => new()
    {
        AssemblyPath = FixturePaths.GetFixtureDll(),
        XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
        Visibility = ApiVisibility.Public,
    };

    /// <summary>
    ///     Validates that every page path computed by <c>BuildMemberPageIndex</c> (surfaced on
    ///     <c>DotNetAstModel.MemberPageIndex</c>) corresponds to an actual page written by the
    ///     gradual-disclosure emitter — mitigating the risk that the index's independently
    ///     reimplemented grouping logic silently drifts from the real page-writing logic.
    /// </summary>
    [Fact]
    public void BuildMemberPageIndex_AllEntries_MatchActualWrittenPages()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: every computed member page path is an actual page the emitter wrote
        Assert.NotEmpty(emitter.Model.MemberPageIndex);
        foreach (var (memberId, pagePath) in emitter.Model.MemberPageIndex)
        {
            Assert.True(
                factory.Writers.ContainsKey(pagePath),
                $"MemberPageIndex entry for '{memberId}' points at '{pagePath}', but no such page was written.");
        }
    }

    /// <summary>Validates that the gradual-disclosure emitter creates the api index page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesApiIndexPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("", "api"), "Expected api index page to be created");
    }

    /// <summary>Validates that an AssemblyDescriptionAttribute value is emitted as a paragraph after the assembly-level heading on the api index page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_AssemblyWithDescription_EmitsDescriptionParagraph()
    {
        // Arrange: the fixture assembly carries a Description property in its csproj
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: a paragraph containing the assembly description appears on the api index page
        var apiWriter = factory.GetWriter("", "api");
        var paragraphs = apiWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p => p.Text.Contains("fixture", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Validates that an explicitly-supplied <see cref="DotNetGeneratorOptions.LibraryDescription"/>
    ///     takes precedence over the assembly's compiled AssemblyDescriptionAttribute on the api index page.
    /// </summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_LibraryDescriptionSupplied_OverridesAssemblyDescription()
    {
        // Arrange: the fixture assembly carries a Description property in its csproj, but an
        // explicit LibraryDescription option should win
        var options = BuildOptions();
        options.LibraryDescription = "TEST OVERRIDE";
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(options).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the overriding paragraph is emitted instead of the compiled attribute value
        var apiWriter = factory.GetWriter("", "api");
        var paragraphs = apiWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p => p.Text.Contains("TEST OVERRIDE", StringComparison.Ordinal));
        Assert.DoesNotContain(paragraphs, p => p.Text.Contains("Test fixture assemblies", StringComparison.Ordinal));
    }

    /// <summary>Validates that the gradual-disclosure emitter creates a namespace page for the fixture namespace.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesNamespacePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: root namespace page exists as a root-level writer
        Assert.True(
            factory.Writers.Keys.Any(k => k.EndsWith("ApiMark.DotNet.Fixtures", StringComparison.Ordinal)),
            "Expected a namespace page containing 'ApiMark.DotNet.Fixtures'");
    }

    /// <summary>Validates that the NamespaceDoc XML remarks are emitted as a paragraph on the namespace page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_NamespaceWithDoc_EmitsNamespaceRemarks()
    {
        // Arrange: the fixture namespace NamespaceDoc carrier declares <remarks>
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the NamespaceDoc remarks appear as a paragraph on the namespace page
        var nsWriter = factory.Writers["ApiMark.DotNet.Fixtures"];
        var paragraphs = nsWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("Namespace-level remarks for verification", StringComparison.Ordinal));
    }

    /// <summary>Validates that the NamespaceDoc XML example is emitted as a code block on the namespace page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_NamespaceWithDoc_EmitsNamespaceExampleCodeBlock()
    {
        // Arrange: the fixture namespace NamespaceDoc carrier declares <example><code>
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the NamespaceDoc example code appears as a code block on the namespace page
        var nsWriter = factory.Writers["ApiMark.DotNet.Fixtures"];
        var codeBlocks = nsWriter.Operations.OfType<CodeBlockOperation>().Select(c => c.Code).ToList();
        Assert.Contains(codeBlocks, c => c.Contains("var x = 1", StringComparison.Ordinal));
    }

    /// <summary>Validates that a namespace whose NamespaceDoc has only <c>&lt;remarks&gt;</c> (no <c>&lt;summary&gt;</c>) shows the single-line remarks text in the all-namespaces index table instead of the no-description placeholder.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_NamespaceWithRemarksOnly_UsesRemarksInIndexTable()
    {
        // Arrange: the RemarksOnly fixture namespace declares only <remarks> on its NamespaceDoc
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the all-namespaces table on the api page shows the collapsed remarks text
        var apiWriter = factory.GetWriter("", "api");
        var nsTable = apiWriter.Operations.OfType<TableOperation>()
            .Single(t => t.Headers.Contains("Types", StringComparer.Ordinal));
        var row = nsTable.Rows.Single(r => r[0].Contains(
            "ApiMark.DotNet.Fixtures.Inner.RemarksOnly", StringComparison.Ordinal));
        Assert.Equal(
            "Remarks-only namespace fallback line one. Remarks-only namespace fallback line two.",
            row[^1]);
        Assert.DoesNotContain("No description provided", row[^1], StringComparison.Ordinal);
    }

    /// <summary>Validates that a namespace whose NamespaceDoc has only <c>&lt;remarks&gt;</c> (no <c>&lt;summary&gt;</c>) shows the single-line remarks text in the parent's child-namespace table instead of the no-description placeholder.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_NamespaceWithRemarksOnly_UsesRemarksInChildTable()
    {
        // Arrange: RemarksOnly is a child of ApiMark.DotNet.Fixtures.Inner, so it appears in that
        // namespace page's child-namespace table
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the child-namespace table on the Inner page shows the collapsed remarks text
        var innerWriter = factory.Writers["ApiMark.DotNet.Fixtures/Inner"];
        var childTable = innerWriter.Operations.OfType<TableOperation>()
            .Single(t => string.Equals(t.Headers[0], "Namespace", StringComparison.Ordinal));
        var row = childTable.Rows.Single(r => r[0].Contains("RemarksOnly", StringComparison.Ordinal));
        Assert.Equal(
            "Remarks-only namespace fallback line one. Remarks-only namespace fallback line two.",
            row[^1]);
        Assert.DoesNotContain("No description provided", row[^1], StringComparison.Ordinal);
    }

    /// <summary>Validates that a type's <c>&lt;remarks&gt;</c> numbered list is rendered as ordered Markdown items on the type page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_TypeWithListRemarks_RendersNumberedListInMarkdown()
    {
        // Arrange: NumberListDocClass declares a <list type="number"> in its <remarks>
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the NumberListDocClass type page contains the rendered ordered list items
        var typeWriter = factory.Writers["ApiMark.DotNet.Fixtures/NumberListDocClass"];
        var paragraphs = typeWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(
            paragraphs,
            p => p.Contains("1. Restore dependencies.", StringComparison.Ordinal) &&
                 p.Contains("1. Run the tests.", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a type's <c>&lt;summary&gt;</c> numbered list is rendered as real
    ///     multi-line ordered Markdown items on the type page body (Issue 2: previously the
    ///     summary was always collapsed to a single line, destroying list structure).
    /// </summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_TypeWithListSummary_RendersNumberedListInMarkdown()
    {
        // Arrange: SummaryNumberListDocClass declares a <list type="number"> in its <summary>
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the type page body renders the summary's list as real ordered Markdown items,
        // with the trailing prose rendered as its own paragraph
        var typeWriter = factory.Writers["ApiMark.DotNet.Fixtures/SummaryNumberListDocClass"];
        var paragraphs = typeWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(
            paragraphs,
            p => p.Contains("1. First summary numbered item.", StringComparison.Ordinal) &&
                 p.Contains("1. Third summary numbered item.", StringComparison.Ordinal));
        Assert.Contains(paragraphs, p => p.Contains("Trailing summary prose after the list.", StringComparison.Ordinal));
    }

    /// <summary>Validates that the gradual-disclosure emitter creates a type page for SampleClass.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesTypePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(
            factory.Writers.Keys.Any(k => k.Contains("SampleClass", StringComparison.Ordinal)),
            "Expected a type page containing 'SampleClass'");
    }

    /// <summary>Validates that the gradual-disclosure emitter creates a dedicated detail page for at least one visible member.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesMemberDetailPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: SampleClass has a Reset method — its detail page must exist
        Assert.True(
            factory.Writers.Keys.Any(k =>
                k.Contains("SampleClass", StringComparison.Ordinal) &&
                k.Contains("Reset", StringComparison.Ordinal)),
            "Expected a member detail page for SampleClass.Reset");
    }

    /// <summary>Validates that the gradual-disclosure emitter creates a combined page for case-colliding members.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_CaseCollision_CreatesCombinedPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: CaseCollisionClass has 'name' (field) and 'Name' (property) that collide on
        // case-insensitive filesystems; the combined page is keyed using the lower-invariant "name"
        Assert.True(
            factory.Writers.Keys.Any(k =>
                k.Contains("CaseCollisionClass", StringComparison.Ordinal) &&
                k.EndsWith("/name", StringComparison.OrdinalIgnoreCase)),
            "Expected a combined collision page for CaseCollisionClass members 'name' and 'Name'");
    }

    /// <summary>Validates that the api index page heading contains the assembly name.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_ApiIndexContainsAssemblyNameHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: api page heading contains assembly name text
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("ApiMark.DotNet.Fixtures API Reference", StringComparison.Ordinal));
    }

    /// <summary>Validates that a type with overloaded methods produces a consolidated overload page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesMethodOverloadPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: IntVsIntArrayClass has two overloads of Process() — they should share one page
        Assert.True(
            factory.Writers.Keys.Any(k =>
                k.Contains("IntVsIntArrayClass", StringComparison.Ordinal) &&
                k.EndsWith("/Process", StringComparison.OrdinalIgnoreCase)),
            "Expected a consolidated overload page for IntVsIntArrayClass.Process");
    }

    /// <summary>Validates that a type with operator overloads produces an operators.md page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesOperatorsPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: OperatorsStruct declares operator overloads — an operators.md page must be created
        Assert.True(
            factory.Writers.Keys.Any(k =>
                k.Contains("OperatorsStruct", StringComparison.Ordinal) &&
                k.EndsWith("/operators", StringComparison.OrdinalIgnoreCase)),
            "Expected an operators page for OperatorsStruct");
    }

    /// <summary>Validates that a type with a nested type produces a dedicated page for the nested type.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesNestedTypePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: OuterClass.Inner should produce a dedicated page under the OuterClass folder
        Assert.True(
            factory.Writers.Keys.Any(k =>
                k.Contains("OuterClass", StringComparison.Ordinal) &&
                k.Contains("Inner", StringComparison.Ordinal)),
            "Expected a dedicated page for OuterClass.Inner nested type");
    }

    /// <summary>Validates that a child namespace also produces a dedicated Markdown page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesChildNamespacePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the child namespace ApiMark.DotNet.Fixtures.Inner must produce its own page
        Assert.True(
            factory.Writers.Keys.Any(k => k.Contains("Inner", StringComparison.Ordinal) &&
                                          !k.Contains("OuterClass", StringComparison.Ordinal)),
            "Expected a dedicated page for the ApiMark.DotNet.Fixtures.Inner child namespace");
    }

    /// <summary>
    ///     Writes a minimal XML doc file containing <paramref name="membersXml"/> and returns the
    ///     path so the caller can clean it up after use.
    /// </summary>
    /// <param name="membersXml">Raw XML to embed inside the &lt;members&gt; element.</param>
    /// <returns>Path to the temporary XML documentation file.</returns>
    private static string WriteXmlDoc(string membersXml)
    {
        var path = Path.GetTempFileName();
        var xml = $"""
            <?xml version="1.0"?>
            <doc>
              <assembly><name>TestAssembly</name></assembly>
              <members>
                {membersXml}
              </members>
            </doc>
            """;
        File.WriteAllText(path, xml);
        return path;
    }

    /// <summary>
    ///     Validates that a member with no <c>&lt;summary&gt;</c> but with <c>&lt;remarks&gt;</c>
    ///     content does NOT show the "No description provided." placeholder on its detail page,
    ///     and that the remarks content is still shown. The compiled fixture XML doc for
    ///     <c>GeneratedRegexClass.DigitsRegex</c> naturally has only &lt;remarks&gt; (the regex
    ///     source generator replaces the hand-written &lt;summary&gt; at compile time).
    /// </summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_MemberWithRemarksOnly_SuppressesPlaceholderAndShowsRemarks()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: DigitsRegex's detail page shows its remarks text and not the placeholder
        var memberKey = factory.Writers.Keys.Single(k =>
            k.Contains("GeneratedRegexClass", StringComparison.Ordinal) &&
            k.Contains("DigitsRegex", StringComparison.Ordinal));
        var memberWriter = factory.Writers[memberKey];
        var paragraphs = memberWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("Pattern:", StringComparison.Ordinal));
        Assert.DoesNotContain(paragraphs, p => p.Contains("No description provided", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Regression guard: validates that a member with NEITHER <c>&lt;summary&gt;</c> nor
    ///     <c>&lt;remarks&gt;</c> still shows the "No description provided." placeholder on its
    ///     detail page.
    /// </summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_MemberWithNeitherSummaryNorRemarks_StillShowsPlaceholder()
    {
        // Arrange: a synthetic XML doc with no entry at all for GeneratedRegexClass.DigitsRegex
        var docPath = WriteXmlDoc("""
            <member name="T:ApiMark.DotNet.Fixtures.GeneratedRegexClass">
              <summary>A class.</summary>
            </member>
            """);
        try
        {
            var options = BuildOptions();
            options.XmlDocPath = docPath;
            var factory = new InMemoryMarkdownWriterFactory();
            var emitter = (DotNetEmitter)new DotNetGenerator(options).Parse(new InMemoryContext());

            // Act
            new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

            // Assert: DigitsRegex's detail page still shows the placeholder (regression guard)
            var memberKey = factory.Writers.Keys.Single(k =>
                k.Contains("GeneratedRegexClass", StringComparison.Ordinal) &&
                k.Contains("DigitsRegex", StringComparison.Ordinal));
            var memberWriter = factory.Writers[memberKey];
            var paragraphs = memberWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
            Assert.Contains(paragraphs, p => p.Contains("No description provided", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    /// <summary>
    ///     Validates that a TYPE with no <c>&lt;summary&gt;</c> but with <c>&lt;remarks&gt;</c>
    ///     content does NOT show the "No description provided." placeholder on its type page,
    ///     and that the remarks content is still shown. Exercises <c>WriteTypeHeaderSections</c>'
    ///     type-level placeholder-suppression branch directly, which the member-level
    ///     <see cref="DotNetEmitterGradualDisclosure_Emit_MemberWithRemarksOnly_SuppressesPlaceholderAndShowsRemarks"/>
    ///     test does not cover.
    /// </summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_TypeWithRemarksOnly_SuppressesPlaceholderAndShowsRemarksOnTypePage()
    {
        // Arrange: override SampleClass's compiled XML doc entry with a remarks-only type doc
        var docPath = WriteXmlDoc("""
            <member name="T:ApiMark.DotNet.Fixtures.SampleClass">
              <remarks>This type's remarks explain its internal behavior.</remarks>
            </member>
            """);
        try
        {
            var options = BuildOptions();
            options.XmlDocPath = docPath;
            var factory = new InMemoryMarkdownWriterFactory();
            var emitter = (DotNetEmitter)new DotNetGenerator(options).Parse(new InMemoryContext());

            // Act
            new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

            // Assert: SampleClass's own type page (not its constructor member sub-page, which
            // happens to share the same sanitized file name) shows the remarks text and not the
            // placeholder. The type page key is exactly "<namespace-folder>/SampleClass"; a
            // member sub-page key has one more path segment (e.g. ".../SampleClass/SampleClass"
            // for the constructor).
            var typeKey = factory.Writers.Keys.Single(k => k.EndsWith("/SampleClass", StringComparison.Ordinal) &&
                !k.EndsWith("/SampleClass/SampleClass", StringComparison.Ordinal));
            var typeWriter = factory.Writers[typeKey];
            var paragraphs = typeWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
            Assert.Contains(paragraphs, p => p.Contains("This type's remarks explain its internal behavior.", StringComparison.Ordinal));
            Assert.DoesNotContain(paragraphs, p => p.Contains("No description provided", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    /// <summary>
    ///     Validates end-to-end that a <c>&lt;see cref&gt;</c> to another in-assembly, visible
    ///     type renders as a real relative Markdown link (still code-span-wrapped), not the
    ///     plain code-span fallback.
    /// </summary>
    [Fact]
    public void CrefLinking_SeeCrefToVisibleType_RendersAsMarkdownLink()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the method summary links directly to the SampleClass type page
        var memberWriter = factory.Writers["ApiMark.DotNet.Fixtures/CrefLinkingClass/ReferencesVisibleType"];
        var paragraphs = memberWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(
            paragraphs,
            p => p.Contains("[`SampleClass`](../SampleClass.md)", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates end-to-end that a <c>&lt;see cref&gt;</c> to another in-assembly, visible
    ///     member renders as a real relative Markdown link to that member's own page.
    /// </summary>
    [Fact]
    public void CrefLinking_SeeCrefToVisibleMember_RendersAsMarkdownLink()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the method summary links directly to the SampleClass.Reset member page
        var memberWriter = factory.Writers["ApiMark.DotNet.Fixtures/CrefLinkingClass/ReferencesVisibleMember"];
        var paragraphs = memberWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(
            paragraphs,
            p => p.Contains("[`SampleClass.Reset`](../SampleClass/Reset.md)", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates end-to-end that a <c>&lt;see cref&gt;</c> to a member filtered out of the
    ///     generated documentation by the active visibility setting keeps today's plain
    ///     code-span fallback — no link is generated for symbols that won't be emitted.
    /// </summary>
    [Fact]
    public void CrefLinking_SeeCrefToFilteredMember_RendersFallbackWithNoLink()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the private method is not emitted under Public visibility, so the cref falls
        // back to plain code-span text with no Markdown link
        var memberWriter = factory.Writers["ApiMark.DotNet.Fixtures/CrefLinkingClass/ReferencesFilteredMember"];
        var paragraphs = memberWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("`ProtectedMembersClass.PrivateMethod()`", StringComparison.Ordinal));
        // The first paragraph is the breadcrumb navigation (which legitimately contains links); only the body must be link-free
        Assert.DoesNotContain(paragraphs.Skip(1), p => p.Contains("](", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates end-to-end that a <c>&lt;see cref&gt;</c> to an external framework type
    ///     keeps today's plain code-span fallback — external crefs are never linked.
    /// </summary>
    [Fact]
    public void CrefLinking_SeeCrefToExternalType_RendersFallbackWithNoLink()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var memberWriter = factory.Writers["ApiMark.DotNet.Fixtures/CrefLinkingClass/ReferencesExternalType"];
        var paragraphs = memberWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("`ArgumentNullException`", StringComparison.Ordinal));
        // The first paragraph is the breadcrumb navigation (which legitimately contains links); only the body must be link-free
        Assert.DoesNotContain(paragraphs.Skip(1), p => p.Contains("](", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates end-to-end that a malformed, unresolvable <c>&lt;see cref&gt;</c> string
    ///     keeps today's fallback rendering — no link is ever generated for it.
    /// </summary>
    [Fact]
    public void CrefLinking_SeeCrefMalformed_RendersFallbackWithNoLink()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var memberWriter = factory.Writers["ApiMark.DotNet.Fixtures/CrefLinkingClass/ReferencesMalformedCref"];
        var paragraphs = memberWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("NotAWellFormedCrefString", StringComparison.Ordinal));
        // The first paragraph is the breadcrumb navigation (which legitimately contains links); only the body must be link-free
        Assert.DoesNotContain(paragraphs.Skip(1), p => p.Contains("](", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates end-to-end that a <c>&lt;seealso cref&gt;</c> to another
    ///     in-assembly, visible type also renders as a real relative Markdown link, proving
    ///     both <c>&lt;see&gt;</c> and <c>&lt;seealso&gt;</c> share the same linking path.
    /// </summary>
    [Fact]
    public void CrefLinking_SeeAlsoCrefToVisibleType_RendersAsMarkdownLink()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the type-level <remarks>See also <seealso cref="SampleClass"/>.</remarks>
        // renders as a real Markdown link on the CrefLinkingClass type page
        var typeWriter = factory.Writers["ApiMark.DotNet.Fixtures/CrefLinkingClass"];
        var paragraphs = typeWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("[`SampleClass`](SampleClass.md)", StringComparison.Ordinal));
    }

    /// <summary>Validates that a member page's parameter table shows default values in the Parameter column.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_DefaultValueParameter_ShowsDefaultInParameterTable()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var memberWriter = factory.Writers["ApiMark.DotNet.Fixtures/DefaultValueClass/ToJson"];
        var table = memberWriter.Operations.OfType<TableOperation>().Single();
        Assert.Equal("Parameter", table.Headers[0]);
        Assert.Equal(3, table.Headers.Length);
        Assert.Equal("indented = true", table.Rows[0][0]);
        Assert.Equal("bool", table.Rows[0][1]);
    }

    /// <summary>Validates breadcrumb paragraphs on type, member, nested-type, and operators pages.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_Pages_HaveRelativeBreadcrumbs()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        string FirstParagraph(string key) => factory.Writers[key].Operations.OfType<ParagraphOperation>().First().Text;
        Assert.Equal("[ApiMark.DotNet.Fixtures](../ApiMark.DotNet.Fixtures.md)", FirstParagraph("ApiMark.DotNet.Fixtures/DefaultValueClass"));
        Assert.Equal(
            "[ApiMark.DotNet.Fixtures](../../ApiMark.DotNet.Fixtures.md) › [DefaultValueClass](../DefaultValueClass.md)",
            FirstParagraph("ApiMark.DotNet.Fixtures/DefaultValueClass/ToJson"));
        Assert.Equal(
            "[ApiMark.DotNet.Fixtures](../../ApiMark.DotNet.Fixtures.md) › [OperatorsStruct](../OperatorsStruct.md)",
            FirstParagraph("ApiMark.DotNet.Fixtures/OperatorsStruct/operators"));
        Assert.Equal(
            "[ApiMark.DotNet.Fixtures](../../ApiMark.DotNet.Fixtures.md) › [OperatorsStruct](../OperatorsStruct.md)",
            FirstParagraph("ApiMark.DotNet.Fixtures/OperatorsStruct/Wrapped"));
    }

    /// <summary>Validates that init-only properties (including positional record properties) render <c>init;</c> on gradual-disclosure pages.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_InitOnlyProperties_RenderInitAccessor()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var allSignatures = factory.Writers
            .Where(w => w.Key.Contains("InitPropertyClass", StringComparison.Ordinal) || w.Key.Contains("SealedRecordClass", StringComparison.Ordinal))
            .SelectMany(w => w.Value.Operations.OfType<SignatureOperation>().Select(s => s.Code))
            .ToList();
        Assert.Contains("public string InitOnlyProperty { get; init; }", allSignatures);
        Assert.Contains("public string Name { get; init; }", allSignatures);
        Assert.DoesNotContain(allSignatures, s => s.Contains("{ get; }", StringComparison.Ordinal) && s.Contains("InitOnlyProperty", StringComparison.Ordinal));
    }

    /// <summary>Validates that nullable value-type parameters and function-typed returns render per-type annotations.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_NullableParameterAndFuncReturn_RendersPerTypeFlags()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var paste = factory.Writers["ApiMark.DotNet.Fixtures/NullabilityDiagramElement/PasteAsync"];
        var pasteTable = paste.Operations.OfType<TableOperation>().Single();
        Assert.Contains("NullPoint", pasteTable.Rows[0][1], StringComparison.Ordinal);
        Assert.EndsWith("?", pasteTable.Rows[0][1].TrimEnd('`'), StringComparison.Ordinal);
        var style = factory.Writers["ApiMark.DotNet.Fixtures/NullabilityDiagramElement/Style"];
        var signature = style.Operations.OfType<SignatureOperation>().First();
        Assert.Contains("Func<NullWire, NullWireStyle?>?", signature.Code, StringComparison.Ordinal);
    }

    /// <summary>Emits the fixture assembly in gradual-disclosure format into a fresh in-memory factory.</summary>
    /// <returns>The populated factory.</returns>
    private static InMemoryMarkdownWriterFactory EmitFixture()
    {
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());
        return factory;
    }

    /// <summary>Returns the first paragraph written to the page with the given key.</summary>
    /// <param name="factory">The factory holding the emitted pages.</param>
    /// <param name="key">The page key.</param>
    /// <returns>The first paragraph text.</returns>
    private static string FirstParagraph(InMemoryMarkdownWriterFactory factory, string key) =>
        factory.Writers[key].Operations.OfType<ParagraphOperation>().First().Text;

    /// <summary>Validates that every default-value fixture variant renders its default in the Parameter column.</summary>
    [Theory]
    [InlineData("ToJson", "indented = true")]
    [InlineData("Format", "format = \"N0\"")]
    [InlineData("Name", "name = null")]
    [InlineData("Origin", "point = default")]
    [InlineData("Mode", "mode = DefaultValueMode.Slow")]
    [InlineData("Optional", "value = default")]
    [InlineData("Explicit", "value = 7")]
    [InlineData("Rate", "rate = 1.5")]
    [InlineData("Count", "count = null")]
    public void DotNetEmitterGradualDisclosure_Emit_DefaultValueVariants_RenderDefaults(string method, string expected)
    {
        // Arrange / Act
        var factory = EmitFixture();

        // Assert
        var table = factory.Writers[$"ApiMark.DotNet.Fixtures/DefaultValueClass/{method}"].Operations.OfType<TableOperation>().Single();
        Assert.Equal(expected, table.Rows[0][0]);
    }

    /// <summary>Validates the character and long default-value variants (two parameters on one method).</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_CharAndLongDefaults_RenderDefaults()
    {
        // Arrange / Act
        var factory = EmitFixture();

        // Assert
        var table = factory.Writers["ApiMark.DotNet.Fixtures/DefaultValueClass/Separate"].Operations.OfType<TableOperation>().Single();
        Assert.Equal("separator = ','", table.Rows[0][0]);
        Assert.Equal("limit = 42", table.Rows[1][0]);
    }

    /// <summary>Validates breadcrumbs on a method-overload page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_OverloadPage_HasBreadcrumb()
    {
        // Arrange / Act
        var factory = EmitFixture();

        // Assert
        var key = factory.Writers.Keys.Single(k => k.StartsWith("ApiMark.DotNet.Fixtures/GenericMethodClass/", StringComparison.Ordinal) && k.EndsWith("/Over", StringComparison.Ordinal));
        Assert.Equal(
            "[ApiMark.DotNet.Fixtures](../../ApiMark.DotNet.Fixtures.md) › [GenericMethodClass](../GenericMethodClass.md)",
            FirstParagraph(factory, key));
    }

    /// <summary>Validates breadcrumbs on a combined (case-collision) page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_CombinedPage_HasBreadcrumb()
    {
        // Arrange / Act
        var factory = EmitFixture();

        // Assert
        var key = "ApiMark.DotNet.Fixtures/CaseCollisionClass/name";
        Assert.Equal(
            "[ApiMark.DotNet.Fixtures](../../ApiMark.DotNet.Fixtures.md) › [CaseCollisionClass](../CaseCollisionClass.md)",
            FirstParagraph(factory, key));
    }

    /// <summary>Validates breadcrumbs on a nested type's page and on a member page of the nested type.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_NestedType_BreadcrumbsIncludeOuterType()
    {
        // Arrange / Act
        var factory = EmitFixture();

        // Assert
        Assert.Equal(
            "[ApiMark.DotNet.Fixtures](../../ApiMark.DotNet.Fixtures.md) › [OuterClass](../OuterClass.md)",
            FirstParagraph(factory, "ApiMark.DotNet.Fixtures/OuterClass/Inner"));
        Assert.Equal(
            "[ApiMark.DotNet.Fixtures](../../../ApiMark.DotNet.Fixtures.md) › [OuterClass](../../OuterClass.md) › [Inner](../Inner.md)",
            FirstParagraph(factory, "ApiMark.DotNet.Fixtures/OuterClass/Inner/InnerValue"));
    }

    /// <summary>Validates that generic method pages list the type parameters and constraints in the signature.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_GenericMethod_SignatureHasTypeParametersAndConstraints()
    {
        // Arrange / Act
        var factory = EmitFixture();

        // Assert
        var signature = factory.Writers["ApiMark.DotNet.Fixtures/GenericMethodClass/Map"].Operations.OfType<SignatureOperation>().First().Code;
        Assert.Contains("Map<TIn, TOut>(TIn value)", signature, StringComparison.Ordinal);
        Assert.Contains("where TIn : class, IDisposable", signature, StringComparison.Ordinal);
        Assert.Contains("where TOut : struct", signature, StringComparison.Ordinal);
    }

    /// <summary>Validates record and struct kind keywords on type pages.</summary>
    [Theory]
    [InlineData("RecordStructSample", "public record struct RecordStructSample")]
    [InlineData("ReadOnlyRecordStructSample", "public readonly record struct ReadOnlyRecordStructSample")]
    [InlineData("ReadOnlyStructSample", "public readonly struct ReadOnlyStructSample")]
    [InlineData("GenericRecordSample1", "public record GenericRecordSample<T>")]
    public void DotNetEmitterGradualDisclosure_Emit_RecordAndStructKinds_ShowKindKeywords(string typeName, string expectedPrefix)
    {
        // Arrange / Act
        var factory = EmitFixture();

        // Assert
        var signature = factory.Writers[$"ApiMark.DotNet.Fixtures/{typeName}"].Operations.OfType<SignatureOperation>().First().Code;
        Assert.StartsWith(expectedPrefix, signature, StringComparison.Ordinal);
    }

    /// <summary>Validates that <c>lang</c> attributes select the fence language of each example code block.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_LangExample_TagsEachCodeBlockWithItsLanguage()
    {
        // Arrange / Act
        var factory = EmitFixture();

        // Assert
        var blocks = factory.Writers["ApiMark.DotNet.Fixtures/LangExampleClass/Configure"].Operations.OfType<CodeBlockOperation>().ToList();
        Assert.Equal(["xml", "powershell", "csharp", "csharp"], blocks.Select(b => b.Language).ToArray());
    }

    /// <summary>Validates that a <c>lang</c> code block nested in a paragraph is honored and prose is not fenced.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_LangExampleInPara_TagsCodeBlocksAndKeepsProseAsParagraphs()
    {
        // Arrange / Act
        var factory = EmitFixture();

        // Assert
        var operations = factory.Writers["ApiMark.DotNet.Fixtures/LangParaExampleClass"].Operations;
        Assert.Equal(["xml", "powershell"], operations.OfType<CodeBlockOperation>().Select(b => b.Language).ToArray());
        Assert.Contains(operations.OfType<ParagraphOperation>(), p => p.Text.Contains("Then run it from a shell.", StringComparison.Ordinal));
    }

    /// <summary>Validates that the cref-weakening exemption does not hide links: only the first paragraph of a member page is a breadcrumb.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_MemberPage_FirstParagraphIsBreadcrumb()
    {
        // Arrange / Act
        var factory = EmitFixture();

        // Assert
        var paragraphs = factory.Writers["ApiMark.DotNet.Fixtures/CrefLinkingClass/ReferencesFilteredMember"].Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(" › ", paragraphs[0].Text, StringComparison.Ordinal);
    }

    /// <summary>Parses and emits the global-namespace fixture assembly.</summary>
    private static InMemoryMarkdownWriterFactory EmitGlobalFixture()
    {
        var options = new DotNetGeneratorOptions
        {
            AssemblyPath = FixturePaths.GetGlobalFixtureDll(),
            XmlDocPath = FixturePaths.GetGlobalFixtureXmlDoc(),
            Visibility = ApiVisibility.Public,
        };
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(options).Parse(new InMemoryContext());
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());
        return factory;
    }

    /// <summary>Validates that emitting types declared without a namespace does not throw and writes the synthetic global-namespace page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_GlobalNamespaceTypes_DoesNotThrowAndCreatesPages()
    {
        // Arrange / Act
        var factory = EmitGlobalFixture();

        // Assert
        Assert.True(factory.HasWriter("", "global-namespace"), "Expected the global-namespace page");
        Assert.True(factory.Writers.ContainsKey("global-namespace/GlobalNamespaceClass"), "Expected the global type page");
        var heading = factory.Writers["global-namespace"].Operations.OfType<HeadingOperation>().First();
        Assert.Equal(1, heading.Level);
        Assert.Equal("(global namespace)", heading.Text);
    }

    /// <summary>Validates that the api index lists the global namespace with its type count.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_GlobalNamespaceTypes_ApiIndexListsGlobalNamespace()
    {
        // Arrange / Act
        var factory = EmitGlobalFixture();

        // Assert
        var table = factory.GetWriter("", "api").Operations.OfType<TableOperation>().First();
        var row = Assert.Single(table.Rows, r => r[0] == "[(global namespace)](global-namespace.md)");
        Assert.Equal("2", row[1]);
    }

    /// <summary>Validates that the global-namespace page links to the type pages it contains.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_GlobalNamespaceTypes_NamespacePageLinksToTypePages()
    {
        // Arrange / Act
        var factory = EmitGlobalFixture();

        // Assert
        var tables = factory.Writers["global-namespace"].Operations.OfType<TableOperation>().ToList();
        Assert.Contains(tables, t => t.Rows.Any(r => r[0].Contains("(global-namespace/GlobalNamespaceClass.md)", StringComparison.Ordinal)));
    }

    /// <summary>Validates that type, member, and nested-type pages carry a breadcrumb to the global-namespace page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_GlobalNamespaceTypes_BreadcrumbLinksToGlobalNamespacePage()
    {
        // Arrange / Act
        var factory = EmitGlobalFixture();

        // Assert
        string FirstParagraph(string key) => factory.Writers[key].Operations.OfType<ParagraphOperation>().First().Text;
        Assert.Equal("[(global namespace)](../global-namespace.md)", FirstParagraph("global-namespace/GlobalNamespaceClass"));
        Assert.Equal(
            "[(global namespace)](../../global-namespace.md) › [GlobalNamespaceClass](../GlobalNamespaceClass.md)",
            FirstParagraph("global-namespace/GlobalNamespaceClass/Echo"));
        Assert.Equal(
            "[(global namespace)](../../global-namespace.md) › [GlobalNamespaceClass](../GlobalNamespaceClass.md)",
            FirstParagraph("global-namespace/GlobalNamespaceClass/NestedGlobalClass"));
    }

    /// <summary>Validates that a cref to a global-namespace type resolves to a relative link to its page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_GlobalNamespaceTypes_CrefResolvesToTypePage()
    {
        // Arrange / Act
        var factory = EmitGlobalFixture();

        // Assert: the consumer's property summary references GlobalNamespaceClass by cref
        var all = factory.Writers["global-namespace/GlobalNamespaceConsumer/Create"].Operations
            .OfType<ParagraphOperation>().Select(p => p.Text);
        Assert.Contains(all, t => t.Contains("(../GlobalNamespaceClass.md)", StringComparison.Ordinal));
    }

    /// <summary>Validates that a type used in another global type's signature is linked to its page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_GlobalNamespaceTypes_SignatureTypeLinksToTypePage()
    {
        // Arrange / Act
        var factory = EmitGlobalFixture();

        // Assert
        var tables = factory.Writers["global-namespace/GlobalNamespaceConsumer"].Operations.OfType<TableOperation>();
        Assert.Contains(tables, t => t.Rows.Any(r => r.Any(c => c.Contains("(GlobalNamespaceClass.md)", StringComparison.Ordinal))));
    }

    /// <summary>Validates that an assembly with no global types produces no global-namespace output.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_NoGlobalNamespaceTypes_NoGlobalNamespaceOutput()
    {
        // Arrange / Act
        var factory = EmitFixture();

        // Assert
        Assert.DoesNotContain(factory.Writers.Keys, k => k.Contains("global-namespace", StringComparison.Ordinal));
        var api = factory.GetWriter("", "api").Operations.OfType<TableOperation>().First();
        Assert.DoesNotContain(api.Rows, r => r[0].Contains("(global namespace)", StringComparison.Ordinal));
    }
}
