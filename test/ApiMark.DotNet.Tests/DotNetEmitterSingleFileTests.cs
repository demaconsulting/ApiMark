// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Unit tests for <see cref="DotNetEmitterSingleFile"/>.</summary>
public class DotNetEmitterSingleFileTests
{
    /// <summary>Builds DotNetGeneratorOptions pointing at the fixture assembly.</summary>
    private static DotNetGeneratorOptions BuildOptions() => new()
    {
        AssemblyPath = FixturePaths.GetFixtureDll(),
        XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
        Visibility = ApiVisibility.Public,
    };

    /// <summary>Validates that the single-file emitter creates exactly one writer.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_ValidModel_CreatesExactlyOneWriter()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        Assert.Single(factory.Writers);
    }

    /// <summary>Validates that the single-file emitter creates the api writer only.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_ValidModel_CreatesApiFileOnly()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("", "api"), "Expected api writer to be created");
    }

    /// <summary>Validates that the api file contains an assembly-level heading.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_ValidModel_ApiFileContainsAssemblyHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("Fixtures", StringComparison.Ordinal));
    }

    /// <summary>Validates that the api file contains a namespace-level heading.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_ValidModel_ApiFileContainsNamespaceHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: a heading containing the fixture namespace name exists
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("ApiMark.DotNet.Fixtures", StringComparison.Ordinal));
    }

    /// <summary>Validates that the api file contains a type-level heading for SampleClass.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_ValidModel_ApiFileContainsTypeHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: a heading for SampleClass exists
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("SampleClass", StringComparison.Ordinal));
    }

    /// <summary>Validates that all heading levels are offset by HeadingDepth when it is set to a non-default value.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_NonDefaultHeadingDepth_OffsetsHeadings()
    {
        // Arrange: HeadingDepth = 2 means assembly → H2, namespace → H3, type → H4
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());
        var config = new EmitConfig { Format = OutputFormat.SingleFile, HeadingDepth = 2 };

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, config, new InMemoryContext());

        // Assert: check heading levels by Level property
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();

        // Assembly heading must be H2
        Assert.Contains(headings, h => h.Text.Contains("Fixtures", StringComparison.Ordinal) && h.Level == 2);

        // Namespace heading must be H3
        Assert.Contains(headings, h => h.Text.Contains("ApiMark.DotNet.Fixtures", StringComparison.Ordinal) && h.Level == 3);

        // Type heading must be H4
        Assert.Contains(headings, h => h.Text.Contains("SampleClass", StringComparison.Ordinal) && h.Level == 4);
    }

    /// <summary>Validates that an AssemblyDescriptionAttribute value is emitted as a paragraph after the assembly-level heading.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_AssemblyWithDescription_EmitsDescriptionParagraph()
    {
        // Arrange: the fixture assembly carries a Description property in its csproj
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: a paragraph containing the assembly description appears in the output
        var apiWriter = factory.GetWriter("", "api");
        var paragraphs = apiWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p => p.Text.Contains("fixture", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Validates that the NamespaceDoc XML summary is emitted as a paragraph following the namespace heading.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_NamespaceWithDoc_EmitsNamespaceSummary()
    {
        // Arrange: the fixture namespace has an internal static NamespaceDoc class with a summary
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: a paragraph containing part of the NamespaceDoc summary appears in the output
        var apiWriter = factory.GetWriter("", "api");
        var paragraphs = apiWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p => p.Text.Contains("testing the ApiMark", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Validates that a compact bullet list paragraph appears before per-member heading sections within a type section.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_TypeWithMembers_EmitsBulletListBeforeMemberHeadings()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: a ParagraphOperation whose text starts with "- **" (the bullet list) appears before
        // a HeadingOperation at depth+3 level that represents a member heading for SampleClass
        var apiWriter = factory.GetWriter("", "api");
        var ops = apiWriter.Operations.ToList();

        // Find the SampleClass heading index
        var sampleClassHeadingIdx = ops
            .Select((op, i) => (op, i))
            .First(x => x.op is HeadingOperation h && h.Text.Contains("SampleClass", StringComparison.Ordinal))
            .i;

        // Find the bullet list paragraph after SampleClass heading
        var bulletParagraphIdx = ops
            .Select((op, i) => (op, i))
            .FirstOrDefault(x => x.i > sampleClassHeadingIdx && x.op is ParagraphOperation p && p.Text.StartsWith("- **", StringComparison.Ordinal))
            .i;

        // Find the first H4 member heading after SampleClass heading (depth=1 → members at H4)
        var firstMemberHeadingIdx = ops
            .Select((op, i) => (op, i))
            .FirstOrDefault(x => x.i > sampleClassHeadingIdx && x.op is HeadingOperation h && h.Level == 4)
            .i;

        Assert.True(bulletParagraphIdx > 0, "A bullet-list paragraph must be present in the SampleClass section.");
        Assert.True(firstMemberHeadingIdx > 0, "A member-level heading must be present in the SampleClass section.");
        Assert.True(bulletParagraphIdx < firstMemberHeadingIdx, "The bullet-list paragraph must appear before the first member heading.");
    }

    /// <summary>Validates that constructor members appear before other members in single-file output.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_TypeWithConstructorAndMethods_ConstructorAppearsFirst()
    {
        // Arrange: OuterClass has both a constructor and a property; constructor must appear first
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: within the OuterClass section, find all H4 member headings and confirm the
        // constructor heading (".ctor" / "OuterClass") appears before the property heading ("Value")
        var apiWriter = factory.GetWriter("", "api");
        var ops = apiWriter.Operations.ToList();

        var outerClassIdx = ops
            .Select((op, i) => (op, i))
            .First(x => x.op is HeadingOperation h && h.Text == "OuterClass")
            .i;

        // Find H4 headings after OuterClass that mention constructor-related text and property text
        var h4HeadingsAfterOuter = ops
            .Select((op, i) => (op, i))
            .Where(x => x.i > outerClassIdx && x.op is HeadingOperation h && h.Level == 4)
            .Select(x => (idx: x.i, text: ((HeadingOperation)x.op).Text))
            .ToList();

        var ctorIdx = h4HeadingsAfterOuter.FirstOrDefault(x => x.text.Contains("OuterClass(", StringComparison.Ordinal) || x.text == "OuterClass()").idx;
        var valueIdx = h4HeadingsAfterOuter.FirstOrDefault(x => x.text == "Value").idx;

        Assert.True(ctorIdx > 0, "A constructor heading must be present in the OuterClass section.");
        Assert.True(valueIdx > 0, "A Value property heading must be present in the OuterClass section.");
        Assert.True(ctorIdx < valueIdx, "The constructor heading must appear before the Value property heading.");
    }

    /// <summary>Validates that delegate types do not emit compiler-generated member sections.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_DelegateType_NoMemberSectionsEmitted()
    {
        // Arrange: ServiceEvent is a public delegate in the fixture assembly
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: no heading containing "Invoke", "BeginInvoke", or "EndInvoke" appears after ServiceEvent
        var apiWriter = factory.GetWriter("", "api");
        var ops = apiWriter.Operations.ToList();

        var serviceEventIdx = ops
            .Select((op, i) => (op, i))
            .First(x => x.op is HeadingOperation h && h.Text.Contains("ServiceEvent", StringComparison.Ordinal))
            .i;

        // Find the next type-level heading after ServiceEvent to bound our search
        var nextTypeHeadingIdx = ops
            .Select((op, i) => (op, i))
            .FirstOrDefault(x => x.i > serviceEventIdx && x.op is HeadingOperation h && h.Level == 3)
            .i;

        var upperBound = nextTypeHeadingIdx > serviceEventIdx ? nextTypeHeadingIdx : ops.Count;

        var hasCompilerMember = ops
            .Skip(serviceEventIdx + 1)
            .Take(upperBound - serviceEventIdx - 1)
            .Any(op => op is HeadingOperation h &&
                       (h.Text.Contains("Invoke", StringComparison.Ordinal)));

        Assert.False(hasCompilerMember, "Delegate compiler-generated members (Invoke, BeginInvoke, EndInvoke) must not be emitted.");
    }

    /// <summary>Validates that nested types have a parent-context notice paragraph indicating their containing type.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_NestedType_EmitsParentNotice()
    {
        // Arrange: OuterClass.Inner is a nested type — it must carry a "Nested type of `OuterClass`." notice
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: a ParagraphOperation containing "Nested type of" and "OuterClass" appears in the output
        var apiWriter = factory.GetWriter("", "api");
        var paragraphs = apiWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p =>
            p.Text.Contains("Nested type of", StringComparison.Ordinal) &&
            p.Text.Contains("OuterClass", StringComparison.Ordinal));
    }

    /// <summary>Validates that parameter type cells in single-file output are plain text, not Markdown links.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_MethodWithParameter_TypeCellIsPlainText()
    {
        // Arrange: SampleClass.GetGreeting(string name) — the Type cell must be "string" not a Markdown link
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: find the parameter table for GetGreeting and verify the Type cell does not contain a Markdown link
        var apiWriter = factory.GetWriter("", "api");
        var tables = apiWriter.Operations.OfType<TableOperation>().ToList();

        // Find a parameter table (headers: Parameter, Type, Description) whose first row Type cell is "string"
        var paramTable = tables.FirstOrDefault(t =>
            t.Headers.Length == 3 &&
            t.Headers[0] == "Parameter" &&
            t.Headers[1] == "Type" &&
            t.Rows.Any(r => r[0] == "name" && r[1] == "string"));

        Assert.NotNull(paramTable);
        var nameRow = paramTable.Rows.First(r => r[0] == "name");
        Assert.Equal("string", nameRow[1]);
        Assert.DoesNotContain("[", nameRow[1], StringComparison.Ordinal);
    }
}
