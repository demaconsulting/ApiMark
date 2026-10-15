// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>
///     End-to-end tests for links from XML doc <c>cref</c> values and signatures to nested types of an
///     external assembly mapped through <see cref="DotNetGeneratorOptions.ExternalLinks"/>.
/// </summary>
public class DotNetEmitterExternalNestedLinkTests
{
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
}
