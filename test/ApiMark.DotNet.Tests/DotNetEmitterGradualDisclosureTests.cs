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
}
