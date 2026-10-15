// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>
///     Exact-output tests for the generic type file-name scheme (<c>Foo{1}.md</c> style): distinct files for
///     <c>Foo</c>, <c>Foo1</c>, <c>Foo&lt;T&gt;</c> and <c>Foo&lt;T1, T2&gt;</c>, and links that resolve to them.
/// </summary>
public class DotNetEmitterGenericNamingTests
{
    /// <summary>The namespace of the naming fixtures.</summary>
    private const string Ns = "ApiMark.DotNet.Fixtures/Naming";

    /// <summary>Emits the fixture assembly in gradual-disclosure format.</summary>
    /// <returns>The populated factory.</returns>
    private static InMemoryMarkdownWriterFactory EmitGradual()
    {
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = BuildEmitter();
        new DotNetEmitterGradualDisclosure(emitter, emitter.Model).Emit(factory, new EmitConfig(), new InMemoryContext());
        return factory;
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

    /// <summary>Returns every text fragment (paragraphs and table cells) of a page.</summary>
    /// <param name="writer">The page writer.</param>
    /// <returns>The text fragments.</returns>
    private static IEnumerable<string> Texts(InMemoryMarkdownWriter writer) =>
        writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text)
            .Concat(writer.Operations.OfType<TableOperation>().SelectMany(t => t.Rows.SelectMany(r => r)));

    /// <summary>Validates the four same-named types produce four distinct, hyphenated-arity files.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_SameNamedGenericTypes_ProduceDistinctFiles()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert
        var keys = factory.Writers.Keys.Where(k => k.StartsWith(Ns + "/", StringComparison.Ordinal) && k.IndexOf('/', Ns.Length + 1) < 0).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(
            new[]
            {
                Ns + "/Foo",
                Ns + "/Foo1",
                Ns + "/Foo{1}",
                Ns + "/Foo{2}",
                Ns + "/Outer{1}",
            },
            keys);
        Assert.Equal("public class Foo<T>", factory.Writers[Ns + "/Foo{1}"].Operations.OfType<SignatureOperation>().Single().Code);
        Assert.Equal("public class Foo1", factory.Writers[Ns + "/Foo1"].Operations.OfType<SignatureOperation>().Single().Code);
        Assert.Equal("public class Foo<T1, T2> : Foo<T1>", factory.Writers[Ns + "/Foo{2}"].Operations.OfType<SignatureOperation>().Single().Code);
    }

    /// <summary>Validates the namespace index links use the hyphenated-arity file names.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_NamespaceIndex_LinksHyphenatedArityFiles()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert
        var links = factory.Writers[Ns].Operations.OfType<TableOperation>().SelectMany(t => t.Rows).Select(r => r[0]).ToList();
        Assert.Contains($"[Foo](Naming/Foo.md)", links);
        Assert.Contains($"[Foo1](Naming/Foo1.md)", links);
        Assert.Contains("[Foo\\<T\\>](Naming/Foo{1}.md)", links);
        Assert.Contains("[Foo\\<T1, T2\\>](Naming/Foo{2}.md)", links);
        Assert.Contains("[Outer\\<T\\>](Naming/Outer{1}.md)", links);
    }

    /// <summary>Validates nested generic type pages live in the hyphenated outer folder.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_NestedGenericType_UsesHyphenatedFolderAndFile()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert
        Assert.Contains(Texts(factory.Writers[Ns + "/Outer{1}"]), t => t.Contains("(Outer{1}/Inner{1}.md)", StringComparison.Ordinal));
        Assert.Contains(factory.Writers[Ns + "/Outer{1}/Inner{1}"].Operations.OfType<SignatureOperation>(), s => s.Code.Contains("Inner<U>", StringComparison.Ordinal));
    }

    /// <summary>Validates no emitted page key or link uses the old digit-suffix form for generic types.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_AllPages_NoDigitSuffixFileNamesForGenericTypes()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert: PropertyEditorRegistry<TEditor> etc. use the hyphen form
        Assert.Contains("ApiMark.DotNet.Fixtures/PropertyEditorRegistry{1}", factory.Writers.Keys);
        Assert.DoesNotContain("ApiMark.DotNet.Fixtures/PropertyEditorRegistry1", factory.Writers.Keys);
        Assert.Contains("ApiMark.DotNet.Fixtures/KeyedPair{2}", factory.Writers.Keys);
        Assert.DoesNotContain("ApiMark.DotNet.Fixtures/KeyedPair2", factory.Writers.Keys);
    }

    /// <summary>Validates every relative markdown link on every page resolves to an emitted page (index, cref, base type, nested).</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_AllRelativeLinks_ResolveToEmittedPages()
    {
        // Arrange
        var factory = EmitGradual();
        var pattern = new Regex(@"\]\(((?!https?:)[^)#\s]+\.md)(?:#[^)\s]*)?\)");
        var checkedLinks = 0;

        // Act / Assert
        foreach (var (key, writer) in factory.Writers)
        {
            var slash = key.LastIndexOf('/');
            var folder = slash >= 0 ? key.Substring(0, slash) : string.Empty;
            foreach (var text in Texts(writer))
            {
                foreach (Match match in pattern.Matches(text))
                {
                    var target = ResolveRelative(folder, match.Groups[1].Value);
                    Assert.True(factory.Writers.ContainsKey(target), $"Page '{key}' links to missing page '{target}'");
                    checkedLinks++;
                }
            }
        }

        Assert.True(checkedLinks > 100, $"Expected many links, saw {checkedLinks}");
    }

    /// <summary>Validates the cref links on the Foo&lt;T&gt; member page use the new names.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_CrefAndBaseTypeLinks_UseHyphenatedArityNames()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert
        var foo1Texts = Texts(factory.Writers[Ns + "/Foo{1}/Link"]).ToList();
        Assert.Contains(foo1Texts, t => t.Contains("(../Foo1.md)", StringComparison.Ordinal));
        Assert.Contains(foo1Texts, t => t.Contains("(../Foo{2}.md)", StringComparison.Ordinal));
    }

    /// <summary>Resolves a relative link from a folder against the page-key namespace.</summary>
    /// <param name="folder">The folder of the linking page (page-key style).</param>
    /// <param name="relative">The relative link.</param>
    /// <returns>The page key of the target (no extension).</returns>
    private static string ResolveRelative(string folder, string relative)
    {
        var parts = new List<string>(folder.Length == 0 ? Array.Empty<string>() : folder.Split('/'));
        foreach (var segment in relative.Split('/'))
        {
            if (segment == "..")
            {
                parts.RemoveAt(parts.Count - 1);
            }
            else if (segment != ".")
            {
                parts.Add(segment);
            }
        }

        var joined = string.Join('/', parts);
        return joined.Substring(0, joined.Length - ".md".Length);
    }
}
