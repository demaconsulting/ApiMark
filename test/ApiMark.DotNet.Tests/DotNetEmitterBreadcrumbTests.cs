// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>
///     Tests that every page of a gradual-disclosure emit (except the root <c>api.md</c>) starts with a
///     breadcrumb whose first crumb is a relative link back to the API reference root, and that every
///     breadcrumb link resolves to an emitted page.
/// </summary>
public partial class DotNetEmitterBreadcrumbTests
{
    /// <summary>Matches a Markdown link and captures its target.</summary>
    [GeneratedRegex(@"\]\(([^)\s]+)\)", RegexOptions.Compiled)]
    private static partial Regex LinkPattern();

    /// <summary>Emits an assembly in gradual-disclosure format.</summary>
    /// <param name="dll">The assembly path.</param>
    /// <param name="xml">The XML documentation path.</param>
    /// <returns>The populated factory.</returns>
    private static InMemoryMarkdownWriterFactory Emit(string dll, string xml)
    {
        var factory = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(new DotNetGeneratorOptions
        {
            AssemblyPath = dll,
            XmlDocPath = xml,
            Visibility = ApiVisibility.Public,
        }).Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());
        return factory;
    }

    /// <summary>Resolves a relative link from a page key to the target page key.</summary>
    /// <param name="key">The key of the page containing the link.</param>
    /// <param name="link">The relative link target.</param>
    /// <returns>The target page key without the <c>.md</c> extension.</returns>
    private static string Resolve(string key, string link)
    {
        var folder = key.Contains('/') ? key[..key.LastIndexOf('/')] : string.Empty;
        var path = new Uri(new Uri("http://x/" + (folder.Length > 0 ? folder + "/" : string.Empty)), link).AbsolutePath.TrimStart('/');
        path = Uri.UnescapeDataString(path);
        return path[..^3];
    }

    /// <summary>Asserts every non-root page of an emit starts with a resolvable API Reference breadcrumb.</summary>
    /// <param name="factory">The populated factory.</param>
    private static void AssertBreadcrumbs(InMemoryMarkdownWriterFactory factory)
    {
        Assert.True(factory.Writers.ContainsKey("api"));
        var pages = factory.Writers.Where(w => w.Key != "api").ToList();
        Assert.NotEmpty(pages);

        foreach (var (key, writer) in pages)
        {
            var ops = writer.Operations.ToList();
            var headingIndex = ops.FindIndex(o => o is HeadingOperation { Level: 1 });
            Assert.True(headingIndex >= 0, $"{key} has no H1");
            var crumb = Assert.IsType<ParagraphOperation>(ops[headingIndex + 1]).Text;
            Assert.StartsWith("[API Reference](", crumb, StringComparison.Ordinal);
            Assert.Contains(" › ", crumb, StringComparison.Ordinal);

            var links = LinkPattern().Matches(crumb).Select(m => m.Groups[1].Value).ToList();
            Assert.Equal("api", Resolve(key, links[0]));
            foreach (var target in links.Select(link => Resolve(key, link)))
            {
                Assert.True(factory.Writers.ContainsKey(target), $"{key} breadcrumb links to missing page {target}");
            }
        }
    }

    /// <summary>Validates every page of the fixtures assembly has a resolvable API Reference breadcrumb.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_AllPages_HaveApiReferenceBreadcrumb()
    {
        // Arrange / Act
        var factory = Emit(FixturePaths.GetFixtureDll(), FixturePaths.GetFixtureXmlDoc());

        // Assert
        AssertBreadcrumbs(factory);
    }

    /// <summary>Validates global-namespace pages also have a resolvable API Reference breadcrumb.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_GlobalNamespacePages_HaveApiReferenceBreadcrumb()
    {
        // Arrange / Act
        var factory = Emit(FixturePaths.GetGlobalFixtureDll(), FixturePaths.GetGlobalFixtureXmlDoc());

        // Assert
        AssertBreadcrumbs(factory);
    }
}
