// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>
///     Exact-output regression tests for overload indexes, escaped generic headings, generic type
///     documentation, namespace navigation and external-type tables of the gradual-disclosure and
///     single-file emitters.
/// </summary>
public class DotNetEmitterGradualDisclosureGenericsTests
{
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
    /// <returns>The populated factory.</returns>
    private static InMemoryMarkdownWriterFactory EmitSingle()
    {
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = BuildEmitter();
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());
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

    /// <summary>Returns the page with the given key.</summary>
    /// <param name="factory">The factory.</param>
    /// <param name="key">The page key.</param>
    /// <returns>The recorded writer.</returns>
    private static InMemoryMarkdownWriter Page(InMemoryMarkdownWriterFactory factory, string key) => factory.Writers[key];

    /// <summary>Returns the first row of any table on a page whose first cell starts with the given text.</summary>
    /// <param name="factory">The factory.</param>
    /// <param name="key">The page key.</param>
    /// <param name="prefix">The expected start of the first cell.</param>
    /// <returns>The matching row.</returns>
    private static string[] Row(InMemoryMarkdownWriterFactory factory, string key, string prefix) =>
        Page(factory, key).Operations.OfType<TableOperation>().SelectMany(t => t.Rows)
            .Single(r => r[0].StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>Returns the headings of a page as (level, text) strings.</summary>
    /// <param name="factory">The factory.</param>
    /// <param name="key">The page key.</param>
    /// <returns>The heading texts prefixed with their markdown hashes.</returns>
    private static List<string> Headings(InMemoryMarkdownWriterFactory factory, string key) =>
        Page(factory, key).Operations.OfType<HeadingOperation>().Select(h => new string('#', h.Level) + " " + h.Text).ToList();

    /// <summary>Returns the paragraph texts of a page.</summary>
    /// <param name="factory">The factory.</param>
    /// <param name="key">The page key.</param>
    /// <returns>The paragraph texts.</returns>
    private static List<string> Paragraphs(InMemoryMarkdownWriterFactory factory, string key) =>
        Page(factory, key).Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();

    /// <summary>Validates the overview rows list distinct return types in overload order.</summary>
    /// <param name="typeKey">The page key of the type.</param>
    /// <param name="memberLabel">The Member cell prefix.</param>
    /// <param name="expectedReturns">The exact Returns cell.</param>
    [Theory]
    [InlineData("ApiMark.DotNet.Fixtures/OverloadMatrix", "[ToAvalonia (4 overloads)]", "decimal, float, long, char[]")]
    [InlineData("ApiMark.DotNet.Fixtures/OverloadMatrix", "[Scale (2 overloads)]", "double")]
    [InlineData("ApiMark.DotNet.Fixtures/OverloadMatrix", "[Transact (2 overloads)]", "int, T")]
    [InlineData("ApiMark.DotNet.Fixtures/OverloadMatrix-1", "[Transact (2 overloads)]", "TItem, T")]
    public void DotNetEmitterGradualDisclosure_Emit_OverloadGroupRow_ListsDistinctReturnTypesInOrder(
        string typeKey,
        string memberLabel,
        string expectedReturns)
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert
        Assert.Equal(expectedReturns, Row(factory, typeKey, memberLabel)[1]);
    }

    /// <summary>Validates generic and non-generic overload headings are escaped.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_OverloadPage_EscapesGenericHeadings()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert
        Assert.Equal(
            new[] { "# Transact", "## Transact(Func\\<int\\>)", "## Transact\\<T\\>(Func\\<T\\>)" },
            Headings(factory, "ApiMark.DotNet.Fixtures/OverloadMatrix/Transact"));
        Assert.Equal(
            new[] { "# Transact", "## Transact(Func\\<TItem\\>)", "## Transact\\<T\\>(Func\\<T\\>)" },
            Headings(factory, "ApiMark.DotNet.Fixtures/OverloadMatrix-1/Transact"));
    }

    /// <summary>
    ///     Validates (issue 6b) that an example belongs only to the overload that documents it, in both a non-generic
    ///     and a generic containing type: the code block follows the generic section and the non-generic section has none.
    /// </summary>
    /// <param name="typeKey">The page key of the type's Transact overload page.</param>
    /// <param name="code">The expected example code.</param>
    [Theory]
    [InlineData("ApiMark.DotNet.Fixtures/OverloadMatrix/Transact", "var text = matrix.Transact(() => \"generic\");")]
    [InlineData("ApiMark.DotNet.Fixtures/OverloadMatrix-1/Transact", "var count = matrix.Transact(() => 3);")]
    public void DotNetEmitterGradualDisclosure_Emit_OverloadPage_ExampleAttachedToDocumentingOverload(string typeKey, string code)
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert: operation order is H1, breadcrumb, H2 non-generic, ..., H2 generic, ..., code block (last)
        var ops = Page(factory, typeKey).Operations.ToList();
        var genericHeading = ops.FindIndex(o => o is HeadingOperation { Level: 2 } h && h.Text.StartsWith("Transact\\<T\\>", StringComparison.Ordinal));
        var codeIndexes = ops.Select((o, i) => (o, i)).Where(t => t.o is CodeBlockOperation).Select(t => t.i).ToList();
        var block = Assert.Single(codeIndexes);
        Assert.True(block > genericHeading, "The example must appear after the generic overload heading");
        Assert.Equal(code, ((CodeBlockOperation)ops[block]).Code);
        Assert.Equal("csharp", ((CodeBlockOperation)ops[block]).Language);
    }

    /// <summary>Validates a text-only example is rendered as prose, not as a csharp fence.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_TextOnlyExample_RendersAsProse()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert
        var page = Page(factory, "ApiMark.DotNet.Fixtures/OverloadMatrix/Pick");
        Assert.DoesNotContain(page.Operations, o => o is CodeBlockOperation);
        Assert.Contains("Pass any value and the same value comes back.", Paragraphs(factory, "ApiMark.DotNet.Fixtures/OverloadMatrix/Pick"));
    }

    /// <summary>Validates the exact generic type page (H1, breadcrumb, signature, type parameter table, link text).</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_GenericTypePage_ShowsEscapedHeadingAndTypeParameters()
    {
        // Arrange / Act
        var factory = EmitGradual();
        const string key = "ApiMark.DotNet.Fixtures/PropertyEditorRegistry-1";

        // Assert: file name keeps the Name{arity} form, H1 is escaped
        Assert.Equal(new[] { "# PropertyEditorRegistry\\<TEditor\\>", "## Type Parameters", "## Constructors", "## Methods" }, Headings(factory, key));
        Assert.Equal("[API Reference](../api.md) › [ApiMark.DotNet.Fixtures](../ApiMark.DotNet.Fixtures.md)", Paragraphs(factory, key)[0]);
        var sig = Page(factory, key).Operations.OfType<SignatureOperation>().Single();
        Assert.Equal("public class PropertyEditorRegistry<TEditor> where TEditor : class", sig.Code);
        var table = Page(factory, key).Operations.OfType<TableOperation>().First();
        Assert.Equal(new[] { "Type Parameter", "Description" }, table.Headers);
        Assert.Equal(new[] { "TEditor", "The editor type stored in the registry." }, Assert.Single(table.Rows));

        // Assert: the namespace page link text is escaped and points at the arity file
        var nsRow = Row(factory, "ApiMark.DotNet.Fixtures", "[PropertyEditorRegistry");
        Assert.Equal("[PropertyEditorRegistry\\<TEditor\\>](ApiMark.DotNet.Fixtures/PropertyEditorRegistry-1.md)", nsRow[0]);
    }

    /// <summary>Validates an undocumented type parameter gets the placeholder and every constraint is rendered.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_MultiParameterGenericType_ShowsPlaceholderAndConstraints()
    {
        // Arrange / Act
        var factory = EmitGradual();
        const string key = "ApiMark.DotNet.Fixtures/KeyedPair-2";

        // Assert
        Assert.Equal("# KeyedPair\\<TKey, TValue\\>", Headings(factory, key)[0]);
        Assert.Equal(
            "public class KeyedPair<TKey, TValue> where TKey : IComparable<TKey> where TValue : new()",
            Page(factory, key).Operations.OfType<SignatureOperation>().Single().Code);
        var rows = Page(factory, key).Operations.OfType<TableOperation>().First().Rows;
        Assert.Equal(new[] { "TKey", "The key type." }, rows[0]);
        Assert.Equal(new[] { "TValue", "*No description provided.*" }, rows[1]);
    }

    /// <summary>Validates generic method pages show a Type Parameters table and the where clause.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_GenericMethodPage_ShowsConstraintAndTypeParameters()
    {
        // Arrange / Act
        var factory = EmitGradual();
        const string key = "ApiMark.DotNet.Fixtures/PropertyEditorRegistry-1/Convert";

        // Assert
        Assert.Equal("# Convert\\<TResult\\>(TEditor)", Headings(factory, key)[0]);
        Assert.Equal("public TResult Convert<TResult>(TEditor editor) where TResult : struct", Page(factory, key).Operations.OfType<SignatureOperation>().Single().Code);
        var table = Page(factory, key).Operations.OfType<TableOperation>().First();
        Assert.Equal(new[] { "Type Parameter", "Description" }, table.Headers);
        Assert.Equal(new[] { "TResult", "The value type to produce." }, Assert.Single(table.Rows));
        Assert.Equal(
            "[API Reference](../../api.md) › [ApiMark.DotNet.Fixtures](../../ApiMark.DotNet.Fixtures.md) › [PropertyEditorRegistry\\<TEditor\\>](../PropertyEditorRegistry-1.md)",
            Paragraphs(factory, key)[0]);
    }

    /// <summary>Validates a non-generic type nested in a generic type has no Type Parameters section.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_NestedTypeOfGenericHost_HasNoOwnTypeParameters()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert
        Assert.DoesNotContain("## Type Parameters", Headings(factory, "ApiMark.DotNet.Fixtures/GenericHost-1/Nested"));
        Assert.Equal("public class Nested", Page(factory, "ApiMark.DotNet.Fixtures/GenericHost-1/Nested").Operations.OfType<SignatureOperation>().Single().Code);
        Assert.Contains("## Type Parameters", Headings(factory, "ApiMark.DotNet.Fixtures/GenericHost-1"));
    }

    /// <summary>Validates no emitted heading or table link label anywhere contains an unescaped angle bracket.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_AllFixtures_NoUnescapedAngleBracketsInHeadingsOrLinkLabels()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert
        var headings = factory.Writers.Values.SelectMany(w => w.Operations.OfType<HeadingOperation>()).Select(h => h.Text);
        Assert.All(headings, h => Assert.DoesNotMatch(@"(?<!\\)[<>]", h));
        var labels = factory.Writers.Values.SelectMany(w => w.Operations.OfType<TableOperation>())
            .SelectMany(t => t.Rows).Select(r => r[0]).Where(c => c.StartsWith('['));
        Assert.All(labels, c => Assert.DoesNotMatch(@"^\[[^\]]*(?<!\\)[<>]", c));
    }

    /// <summary>Validates the single-file emitter escapes generic headings and emits type-parameter tables.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_GenericType_EscapesHeadingsAndListsTypeParameters()
    {
        // Arrange / Act
        var factory = EmitSingle();
        var ops = factory.GetWriter("", "api").Operations;

        // Assert
        var headings = ops.OfType<HeadingOperation>().Select(h => h.Text).ToList();
        Assert.Contains("PropertyEditorRegistry\\<TEditor\\>", headings);
        Assert.Contains("Convert\\<TResult\\>(TEditor)", headings);
        Assert.Contains(ops.OfType<SignatureOperation>(), s => s.Code == "public class PropertyEditorRegistry<TEditor> where TEditor : class");
        var typeParameterTables = ops.OfType<TableOperation>().Where(t => t.Headers[0] == "Type Parameter").ToList();
        Assert.Contains(typeParameterTables, t => t.Rows.Any(r => r[0] == "TEditor" && r[1] == "The editor type stored in the registry."));
        Assert.Contains(typeParameterTables, t => t.Rows.Any(r => r[0] == "TResult" && r[1] == "The value type to produce."));
        Assert.All(headings, h => Assert.DoesNotMatch(@"(?<!\\)[<>]", h));
    }

    /// <summary>Validates the exact breadcrumb of the root and a child namespace page.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_NamespacePages_ShowBreadcrumb()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert
        Assert.Equal("[API Reference](api.md) › ApiMark.DotNet.Fixtures", Paragraphs(factory, "ApiMark.DotNet.Fixtures")[0]);
        Assert.Equal(
            "[API Reference](../api.md) › [ApiMark.DotNet.Fixtures](../ApiMark.DotNet.Fixtures.md) › ApiMark.DotNet.Fixtures.Navigation",
            Paragraphs(factory, "ApiMark.DotNet.Fixtures/Navigation")[0]);
        Assert.Equal(
            "[API Reference](../../api.md) › [ApiMark.DotNet.Fixtures](../../ApiMark.DotNet.Fixtures.md) › [ApiMark.DotNet.Fixtures.Inner](../Inner.md) › ApiMark.DotNet.Fixtures.Inner.RemarksOnly",
            Paragraphs(factory, "ApiMark.DotNet.Fixtures/Inner/RemarksOnly")[0]);
    }

    /// <summary>Validates the api.md Start here link and the Key Types column (documented first, then alphabetical, max 3).</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ApiPage_HasStartHereAndKeyTypes()
    {
        // Arrange / Act
        var factory = EmitGradual();
        var api = factory.GetWriter("", "api");

        // Assert
        Assert.Contains("**Start here:** [ApiMark.DotNet.Fixtures](ApiMark.DotNet.Fixtures.md)", api.Operations.OfType<ParagraphOperation>().Select(p => p.Text));
        var table = api.Operations.OfType<TableOperation>().First();
        Assert.Equal(new[] { "Namespace", "Types", "Key Types", "Description" }, table.Headers);
        var nav = table.Rows.Single(r => r[0].StartsWith("[ApiMark.DotNet.Fixtures.Navigation]", StringComparison.Ordinal));
        Assert.Equal(
            "[Bravo](ApiMark.DotNet.Fixtures/Navigation/Bravo.md), [Charlie](ApiMark.DotNet.Fixtures/Navigation/Charlie.md), [Delta](ApiMark.DotNet.Fixtures/Navigation/Delta.md)",
            nav[2]);
        var single = table.Rows.Single(r => r[0].StartsWith("[ApiMark.DotNet.Fixtures.Inner]", StringComparison.Ordinal));
        Assert.Equal("[InnerNamespaceClass](ApiMark.DotNet.Fixtures/Inner/InnerNamespaceClass.md)", single[2]);
    }

    /// <summary>Validates the External Types table shows simplified, escaped generic arguments.</summary>
    [Fact]
    public void DotNetEmitterGradualDisclosure_Emit_ExternalGenericTypes_AreSimplifiedAndEscaped()
    {
        // Arrange / Act
        var factory = EmitGradual();

        // Assert
        var table = Page(factory, "ApiMark.DotNet.Fixtures/ExternalGenericConsumer").Operations.OfType<TableOperation>()
            .Single(t => t.Headers[0] == "Type" && t.Headers[1] == "Namespace");
        var ns = "ApiMark.DotNet.Fixtures.External";
        Assert.Equal(
            new[]
            {
                new[] { "ExternalGenericFormat\\<Dictionary\\<string, List\\<int\\>\\>\\>", ns },
                new[] { "ExternalGenericFormat\\<ExternalGenericFormat\\<string\\>\\>", ns },
                new[] { "ExternalGenericFormat\\<int[]\\>", ns },
                new[] { "ExternalGenericFormat\\<string\\>", ns },
            },
            table.Rows.ToArray());
    }
}
