// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>
///     Exact-output tests for the "Inherited Members" section across visibility tiers, abstract bases,
///     external bases (resolved, unresolved and link-mapped), generic and nested bases, and the
///     "Inherited from" link targets.
/// </summary>
public class DotNetEmitterInheritedMembersTests
{
    /// <summary>The fixture namespace and root page folder.</summary>
    private const string Ns = "ApiMark.DotNet.Fixtures";

    /// <summary>Emits the fixture assembly in gradual-disclosure format.</summary>
    /// <param name="visibility">The visibility tier.</param>
    /// <param name="withReferences">Whether the external fixture assembly is a reference.</param>
    /// <param name="externalLinks">Optional external link mappings.</param>
    /// <returns>The populated factory.</returns>
    private static InMemoryMarkdownWriterFactory Emit(
        ApiVisibility visibility,
        bool withReferences = false,
        IReadOnlyDictionary<string, string>? externalLinks = null)
    {
        var factory = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(BuildOptions(visibility, withReferences, externalLinks)).Parse(new InMemoryContext())
            .Emit(factory, new EmitConfig(), new InMemoryContext());
        return factory;
    }

    /// <summary>Builds the generator options.</summary>
    /// <param name="visibility">The visibility tier.</param>
    /// <param name="withReferences">Whether the external fixture assembly is a reference.</param>
    /// <param name="externalLinks">Optional external link mappings.</param>
    /// <returns>The options.</returns>
    private static DotNetGeneratorOptions BuildOptions(
        ApiVisibility visibility,
        bool withReferences,
        IReadOnlyDictionary<string, string>? externalLinks)
    {
        var options = new DotNetGeneratorOptions
        {
            AssemblyPath = FixturePaths.GetFixtureDll(),
            XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
            Visibility = visibility,
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

    /// <summary>Renders the inherited-members section of a page as exact lines.</summary>
    /// <param name="factory">The factory.</param>
    /// <param name="typeName">The type page name.</param>
    /// <returns>Lines such as <c>### Inherited from X</c> followed by <c>Name | Kind</c> rows.</returns>
    private static List<string> InheritedLines(InMemoryMarkdownWriterFactory factory, string typeName)
    {
        var lines = new List<string>();
        var inSection = false;
        foreach (var op in factory.Writers[$"{Ns}/{typeName}"].Operations)
        {
            switch (op)
            {
                case HeadingOperation { Level: 2 } h:
                    inSection = h.Text == "Inherited Members";
                    break;
                case HeadingOperation { Level: 3 } h when inSection:
                    lines.Add("### " + h.Text);
                    break;
                case TableOperation t when inSection:
                    lines.AddRange(t.Rows.Select(r => $"{r[0]} | {r[1]}"));
                    break;
                case ParagraphOperation p when inSection:
                    lines.Add("P: " + p.Text);
                    break;
            }
        }

        return lines;
    }

    /// <summary>Validates the visibility tiers select exactly the inherited members the tier exposes.</summary>
    /// <param name="visibility">The tier.</param>
    /// <param name="expected">The expected member rows under the single base heading.</param>
    [Theory]
    [MemberData(nameof(TierData))]
    public void DotNetEmitterGradualDisclosure_Emit_InheritedMembers_HonorsVisibilityTier(ApiVisibility visibility, string[] expected)
    {
        // Arrange / Act
        var lines = InheritedLines(Emit(visibility), "InheritedTierDerived");

        // Assert
        Assert.Equal(new[] { "### Inherited from [InheritedTierBase](InheritedTierBase.md)" }.Concat(expected).ToArray(), lines);
    }

    /// <summary>Gets the tier theory data.</summary>
    /// <returns>The data rows.</returns>
    public static TheoryData<ApiVisibility, string[]> TierData() => new()
    {
        { ApiVisibility.Public, new[] { "PublicOp | Method" } },
        {
            ApiVisibility.PublicAndProtected,
            new[] { "ProtectedInternalOp | Method", "ProtectedOp | Method", "PublicOp | Method", "ProtectedField | Field" }
        },
        {
            ApiVisibility.All,
            new[]
            {
                "InternalOp | Method",
                "PrivateProtectedOp | Method",
                "ProtectedInternalOp | Method",
                "ProtectedOp | Method",
                "PublicOp | Method",
                "ProtectedField | Field",
            }
        },
    };

    /// <summary>Validates abstract members implemented lower in the chain are listed once, at the declaring type that still owns them.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_AbstractBase_ListsEachMemberOnceAndSkipsOverridden()
    {
        // Arrange / Act
        var factory = Emit(ApiVisibility.Public);

        // Assert: Mid implements Compute, so only Name (still abstract) and Concrete remain from the base
        Assert.Equal(
            new[] { "### Inherited from [InheritedAbstractBase](InheritedAbstractBase.md)", "Name | Property", "Concrete | Method" },
            InheritedLines(factory, "InheritedAbstractMid"));

        // Assert: Leaf implements Name; Compute is owned by Mid and Concrete by the base
        Assert.Equal(
            new[]
            {
                "### Inherited from [InheritedAbstractMid](InheritedAbstractMid.md)",
                "Compute | Method",
                "### Inherited from [InheritedAbstractBase](InheritedAbstractBase.md)",
                "Concrete | Method",
            },
            InheritedLines(factory, "InheritedAbstractLeaf"));
    }

    /// <summary>Validates a resolved external base lists its protected members at the protected tier.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ExternalBase_ProtectedTier_ListsProtectedField()
    {
        // Arrange / Act
        var factory = Emit(ApiVisibility.PublicAndProtected, withReferences: true);

        // Assert
        Assert.Equal(
            new[] { "### Inherited from ExternalBaseClass", "DescribeBase | Method", "BaseCounter | Field" },
            InheritedLines(factory, "InheritedFromExternal"));
        Assert.Equal(
            new[] { "### Inherited from ExternalBaseClass", "BaseCounter | Field" },
            InheritedLines(factory, "InheritedFromExternalOverride"));
    }

    /// <summary>Validates an external base whose members are all overridden or hidden produces no Inherited Members section.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ExternalBase_AllMembersOverridden_NoSection()
    {
        // Arrange / Act
        var factory = Emit(ApiVisibility.Public, withReferences: true);

        // Assert
        Assert.Empty(InheritedLines(factory, "InheritedFromExternalOverride"));
        Assert.DoesNotContain(
            factory.Writers[$"{Ns}/InheritedFromExternalOverride"].Operations.OfType<HeadingOperation>(),
            h => h.Text == "Inherited Members");
    }

    /// <summary>Validates an unmapped external base renders as plain text (no dead link) while still listing its members.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ExternalBase_Unmapped_RendersPlainTextHeading()
    {
        // Arrange / Act
        var lines = InheritedLines(Emit(ApiVisibility.Public), "InheritedFromExternal");

        // Assert
        Assert.Equal(new[] { "### Inherited from ExternalBaseClass", "DescribeBase | Method" }, lines);
    }

    /// <summary>Validates a link-mapped external base heading links into the external documentation.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ExternalBase_Mapped_LinksIntoExternalDocs()
    {
        // Arrange / Act
        var factory = Emit(
            ApiVisibility.Public,
            withReferences: true,
            externalLinks: new Dictionary<string, string> { ["ApiMark.DotNet.Fixtures.External"] = "../OtherLib/api" });

        // Assert
        Assert.Equal(
            new[]
            {
                "### Inherited from [ExternalBaseClass](../../OtherLib/api/ApiMark.DotNet.Fixtures.External/ExternalBaseClass.md)",
                "DescribeBase | Method",
            },
            InheritedLines(factory, "InheritedFromExternal"));
    }

    /// <summary>
    ///     Validates a chain of external bases declared in the same external assembly renders every
    ///     heading as plain text when unmapped: such types are never pages of the documented assembly.
    /// </summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ExternalBaseChain_Unmapped_RendersPlainTextHeadings()
    {
        // Arrange / Act
        var factory = Emit(ApiVisibility.Public, withReferences: true);

        // Assert
        Assert.Equal(
            new[]
            {
                "### Inherited from ExternalChainMiddle",
                "MiddleOp | Method",
                "### Inherited from ExternalChainTop",
                "TopOp | Method",
            },
            InheritedLines(factory, "InheritedFromExternalChain"));
    }

    /// <summary>Validates every base in an external chain links into the mapped external documentation.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ExternalBaseChain_Mapped_LinksEveryHeadingIntoExternalDocs()
    {
        // Arrange / Act
        var factory = Emit(
            ApiVisibility.Public,
            withReferences: true,
            externalLinks: new Dictionary<string, string> { ["ApiMark.DotNet.Fixtures.External"] = "../OtherLib/api" });

        // Assert
        Assert.Equal(
            new[]
            {
                "### Inherited from [ExternalChainMiddle](../../OtherLib/api/ApiMark.DotNet.Fixtures.External/ExternalChainMiddle.md)",
                "MiddleOp | Method",
                "### Inherited from [ExternalChainTop](../../OtherLib/api/ApiMark.DotNet.Fixtures.External/ExternalChainTop.md)",
                "TopOp | Method",
            },
            InheritedLines(factory, "InheritedFromExternalChain"));
    }

    /// <summary>Validates generic and nested bases render a correct heading and only link to existing pages.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_GenericAndNestedBases_RenderExpectedHeadings()
    {
        // Arrange / Act
        var factory = Emit(ApiVisibility.Public);

        // Assert
        Assert.Equal(
            new[] { "### Inherited from [InheritedGenericBase](InheritedGenericBase{1}.md)\\<string\\>", "Take | Method" },
            InheritedLines(factory, "InheritedFromGeneric"));
        Assert.Equal(
            new[] { "### Inherited from [NestedBase](InheritedNestedHost/NestedBase.md)", "NestedOp | Method" },
            InheritedLines(factory, "InheritedFromNested"));
    }

    /// <summary>Validates every "Inherited from" link on every page resolves to an emitted page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_InheritedFromLinks_ResolveToEmittedPages()
    {
        // Arrange
        var factory = Emit(ApiVisibility.All);
        var pattern = new Regex(@"^Inherited from \[[^\]]+\]\(((?!https?:)[^)#\s]+)\.md\)", RegexOptions.Multiline);
        var checkedLinks = 0;

        // Act / Assert
        foreach (var (key, writer) in factory.Writers)
        {
            var folder = key.Contains('/', StringComparison.Ordinal) ? key.Substring(0, key.LastIndexOf('/')) : string.Empty;
            foreach (var heading in writer.Operations.OfType<HeadingOperation>().Where(h => h.Level == 3))
            {
                var match = pattern.Match(heading.Text);
                if (!match.Success)
                {
                    continue;
                }

                var target = Path.GetFullPath(Path.Combine("/root", folder, match.Groups[1].Value)).Replace('\\', '/');
                var rootless = target.Substring(target.IndexOf("/root/", StringComparison.Ordinal) >= 0 ? target.IndexOf("/root/", StringComparison.Ordinal) + 6 : target.IndexOf(':') + 2);
                Assert.True(factory.Writers.ContainsKey(rootless), $"'{key}' links to missing page '{rootless}'");
                checkedLinks++;
            }
        }

        Assert.True(checkedLinks >= 5, $"Expected several inherited-from links, saw {checkedLinks}");
    }

    /// <summary>
    ///     Validates every relative Markdown link on every page, whatever the section, resolves to an
    ///     emitted page when external assemblies are resolvable but unmapped — an unmapped external
    ///     type must never be rendered as a link into the documented tree.
    /// </summary>
    /// <param name="visibility">The visibility tier.</param>
    [Theory]
    [InlineData(ApiVisibility.Public)]
    [InlineData(ApiVisibility.All)]
    public void DotNetEmitterGradualDisclosure_Emit_WithReferences_AllRelativeLinksResolveToEmittedPages(ApiVisibility visibility)
    {
        // Arrange
        var factory = Emit(visibility, withReferences: true);
        var linkPattern = new Regex(@"\]\(((?!https?:|file:|#)[^)#\s]+)\.md(?:#[^)\s]*)?\)");
        var checkedLinks = 0;

        // Act / Assert
        foreach (var (key, writer) in factory.Writers)
        {
            var folder = key.Contains('/', StringComparison.Ordinal) ? key.Substring(0, key.LastIndexOf('/')) : string.Empty;
            var texts = writer.Operations.SelectMany(op => op switch
            {
                HeadingOperation h => new[] { h.Text },
                ParagraphOperation p => new[] { p.Text },
                TableOperation t => t.Rows.SelectMany(r => r),
                _ => Array.Empty<string>(),
            });
            foreach (var match in texts.SelectMany(t => linkPattern.Matches(t).Cast<Match>()))
            {
                var segments = new List<string>(folder.Split('/', StringSplitOptions.RemoveEmptyEntries));
                foreach (var part in Uri.UnescapeDataString(match.Groups[1].Value).Split('/'))
                {
                    if (part == "..")
                    {
                        segments.RemoveAt(segments.Count - 1);
                    }
                    else if (part != ".")
                    {
                        segments.Add(part);
                    }
                }

                var target = string.Join('/', segments);
                Assert.True(factory.Writers.ContainsKey(target), $"'{key}' links to missing page '{target}'");
                checkedLinks++;
            }
        }

        Assert.True(checkedLinks > 100, $"Expected many relative links, saw {checkedLinks}");
    }

    /// <summary>Validates the single-file emitter lists protected inherited members at the protected tier.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_InheritedMembers_ProtectedTierListsProtectedMembers()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(BuildOptions(ApiVisibility.PublicAndProtected, false, null)).Parse(new InMemoryContext())
            .Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Act
        var ops = factory.GetWriter(string.Empty, "api").Operations.ToList();
        var heading = ops.FindIndex(o => o is HeadingOperation h && h.Text.StartsWith("InheritedTierDerived", StringComparison.Ordinal));
        var section = ops.Skip(heading).TakeWhile((o, i) => i == 0 || o is not HeadingOperation { Level: <= 2 }).ToList();
        var rows = section.OfType<TableOperation>().SelectMany(t => t.Rows).Select(r => $"{r[0]} | {r[1]}").ToList();

        // Assert
        Assert.Contains("ProtectedOp | Method", rows);
        Assert.Contains("ProtectedField | Field", rows);
        Assert.DoesNotContain("InternalOp | Method", rows);
    }
}
