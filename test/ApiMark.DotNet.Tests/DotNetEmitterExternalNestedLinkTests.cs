// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>
///     End-to-end tests for links from XML doc <c>cref</c> values and signatures to nested types of an
///     external assembly mapped through <see cref="DotNetGeneratorOptions.ExternalLinks"/>.
/// </summary>
public partial class DotNetEmitterExternalNestedLinkTests
{
    /// <summary>Matches a link into the other library's API pages.</summary>
    [GeneratedRegex(@"\]\(((?:\.\./)+OtherLib/api/[^)\s#]+)\.md", RegexOptions.Compiled)]
    private static partial Regex LinkPattern();

    /// <summary>The page key of the consumer type.</summary>
    private const string ConsumerKey = "ApiMark.DotNet.Fixtures/NestedExternalConsumer";

    /// <summary>The external API root as seen from a page one folder deep.</summary>
    private const string Root = "../../OtherLib/api/ApiMark.DotNet.Fixtures.External/Nested";

    /// <summary>Emits the fixture assembly with the external fixture assembly mapped.</summary>
    /// <returns>The populated factory.</returns>
    private static InMemoryMarkdownWriterFactory Emit()
    {
        var options = new DotNetGeneratorOptions
        {
            AssemblyPath = FixturePaths.GetFixtureDll(),
            XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
            Visibility = ApiVisibility.Public,
            ReferencePaths = [FixturePaths.GetExternalFixtureDll()],
            ExternalLinks = new Dictionary<string, string> { ["ApiMark.DotNet.Fixtures.External"] = "../OtherLib/api" },
        };
        var factory = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(options).Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());
        return factory;
    }

    /// <summary>Validates nested-type crefs link to the nested type's own page, including generic hosts.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ExternalNestedTypeCrefs_LinkToNestedPages()
    {
        // Arrange / Act
        var factory = Emit();

        // Assert
        var summary = factory.Writers[ConsumerKey].Operations.OfType<ParagraphOperation>().Select(p => p.Text)
            .Single(t => t.Contains("Refers to nested", StringComparison.Ordinal)).Replace('\n', ' ');
        Assert.Contains($"]({Root}/NestedHost/Inner.md)", summary, StringComparison.Ordinal);
        Assert.Contains($"]({Root}/NestedHost/Deep/Leaf.md)", summary, StringComparison.Ordinal);
        Assert.Contains($"]({Root}/NestedGenericHost{{1}}/Inner{{1}}.md)", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("`1", summary, StringComparison.Ordinal);
    }

    /// <summary>Validates property types that are external nested types link to the nested page (Cecil path).</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ExternalNestedPropertyTypes_LinkToNestedPages()
    {
        // Arrange / Act
        var factory = Emit();

        // Assert
        var cells = factory.Writers[ConsumerKey].Operations.OfType<TableOperation>().SelectMany(t => t.Rows.SelectMany(r => r)).ToList();
        Assert.Contains(cells, c => c.Contains($"]({Root}/NestedHost/Inner.md)", StringComparison.Ordinal));
        Assert.Contains(cells, c => c.Contains($"]({Root}/NestedGenericHost{{1}}/Inner{{1}}.md)", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates every link into the mapped assembly names a page that the mapped assembly's own run
    ///     writes, so the predictable name equals the name its registry approves.
    /// </summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ExternalLinks_MatchTargetAssemblyRegistryNames()
    {
        // Arrange
        var consumer = Emit();
        var target = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(new DotNetGeneratorOptions
        {
            AssemblyPath = FixturePaths.GetExternalFixtureDll(),
            XmlDocPath = FixturePaths.GetExternalFixtureXmlDoc(),
            Visibility = ApiVisibility.Public,
        }).Parse(new InMemoryContext()).Emit(target, new EmitConfig(), new InMemoryContext());
        const string marker = "OtherLib/api/";
        var pattern = LinkPattern();

        // Act
        var linked = consumer.Writers.Values
            .SelectMany(w => w.Operations.OfType<ParagraphOperation>().Select(p => p.Text)
                .Concat(w.Operations.OfType<TableOperation>().SelectMany(t => t.Rows.SelectMany(r => r))))
            .SelectMany(t => pattern.Matches(t).Select(m => m.Groups[1].Value))
            .Select(v => v[(v.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..])
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.Contains("ApiMark.DotNet.Fixtures.External/Nested/NestedGenericHost{1}/Inner{1}", linked);
        Assert.True(linked.Count >= 4, string.Join(", ", linked));
        var missing = linked.Where(l => !target.Writers.ContainsKey(l)).ToList();
        Assert.True(missing.Count == 0, "Links to pages the target run does not write: " + string.Join(", ", missing));
    }
}
