using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Integration tests for <see cref="DotNetGenerator"/>.</summary>
public class DotNetGeneratorTests
{
    /// <summary>Expected headers for the Constructors table — Member and Description only (no Returns column).</summary>
    private static readonly string[] ConstructorTableHeaders = ["Member", "Description"];

    /// <summary>Expected headers for the Properties table — Member, Type, and Description.</summary>
    private static readonly string[] PropertyTableHeaders = ["Member", "Type", "Description"];

    /// <summary>Expected headers for the Methods table — Member, Returns, and Description.</summary>
    private static readonly string[] MethodTableHeaders = ["Member", "Returns", "Description"];
    /// <summary>
    ///     Builds a <see cref="DotNetGeneratorOptions"/> pointing at the fixture assembly
    ///     with the specified visibility and obsolete settings.
    /// </summary>
    /// <param name="visibility">Which members to include in generated output.</param>
    /// <param name="includeObsolete">Whether to include obsolete members.</param>
    /// <param name="excludePatterns">Wildcard patterns for namespaces/types to exclude.</param>
    /// <returns>A fully configured <see cref="DotNetGeneratorOptions"/>.</returns>
    private static DotNetGeneratorOptions BuildOptions(
        ApiVisibility visibility = ApiVisibility.Public,
        bool includeObsolete = false,
        IReadOnlyList<string>? excludePatterns = null)
    {
        return new DotNetGeneratorOptions
        {
            AssemblyPath = FixturePaths.GetFixtureDll(),
            XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
            Visibility = visibility,
            IncludeObsolete = includeObsolete,
            ExcludePatterns = excludePatterns ?? [],
        };
    }

    /// <summary>Validates that <see cref="DotNetGenerator.Parse"/> throws <see cref="FileNotFoundException"/> when the XML doc file is missing.</summary>
    [Fact]
    public void DotNetGenerator_Generate_XmlDocMissing_ThrowsFileNotFoundException()
    {
        // Arrange
        var options = new DotNetGeneratorOptions
        {
            AssemblyPath = FixturePaths.GetFixtureDll(),
            XmlDocPath = "/nonexistent/path.xml",
        };
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(options);

        // Act / Assert
        Assert.Throws<FileNotFoundException>(() => generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext()));
    }

    /// <summary>Validates that a valid assembly produces an <c>api</c> entrypoint Markdown page.</summary>
    [Fact]
    public void DotNetGenerator_Generate_ValidAssembly_CreatesApiMarkdownPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.Writers.ContainsKey("api"), "Expected api.md to be created");
    }

    /// <summary>Validates that a valid assembly produces a namespace summary Markdown page.</summary>
    [Fact]
    public void DotNetGenerator_Generate_ValidAssembly_CreatesNamespacePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: root namespace page sits at root level (not inside a subfolder)
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures"),
            "Expected root-level namespace page for ApiMark.DotNet.Fixtures");
    }

    /// <summary>Validates that a valid assembly produces a type page for <c>SampleClass</c>.</summary>
    [Fact]
    public void DotNetGenerator_Generate_ValidAssembly_CreatesTypePageForSampleClass()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/SampleClass"),
            "Expected type page for SampleClass");
    }

    /// <summary>Validates that obsolete types are excluded when <see cref="DotNetGeneratorOptions.IncludeObsolete"/> is false.</summary>
    [Fact]
    public void DotNetGenerator_Generate_IncludeObsoleteFalse_ExcludesObsoleteClass()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions(includeObsolete: false));

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.False(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ObsoleteClass"),
            "ObsoleteClass should be excluded by default");
    }

    /// <summary>Validates that obsolete types are included when <see cref="DotNetGeneratorOptions.IncludeObsolete"/> is true.</summary>
    [Fact]
    public void DotNetGenerator_Generate_IncludeObsoleteTrue_IncludesObsoleteClass()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions(includeObsolete: true));

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ObsoleteClass"),
            "ObsoleteClass should be included when IncludeObsolete=true");
    }

    /// <summary>Validates that an exact-name exclude pattern removes only the matching type page while sibling types remain present.</summary>
    [Fact]
    public void DotNetGenerator_Generate_ExactExcludePattern_RemovesOnlyMatchingType()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions(excludePatterns: ["ApiMark.DotNet.Fixtures.SampleClass"]));

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.False(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/SampleClass"),
            "SampleClass should be excluded by an exact-name pattern");
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ISampleInterface"),
            "ISampleInterface should remain present when only SampleClass is excluded");
    }

    /// <summary>Validates that a wildcard pattern matching a namespace removes the entire namespace, and it disappears from indexes.</summary>
    [Fact]
    public void DotNetGenerator_Generate_WildcardExcludePattern_RemovesEntireNamespaceFromIndex()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions(excludePatterns: ["ApiMark.DotNet.Fixtures.ExcludedSample*"]));

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the type page, the namespace page, and any reference to the namespace in the
        // entrypoint index must all be absent because the namespace has zero surviving types.
        Assert.False(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ExcludedSample/ExcludedSampleClass"),
            "ExcludedSampleClass should be excluded by the wildcard pattern");
        Assert.False(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ExcludedSample"),
            "The ExcludedSample namespace page should not exist once fully excluded");
    }

    /// <summary>Validates that a non-matching exclude pattern leaves all other output unaffected (regression guard).</summary>
    [Fact]
    public void DotNetGenerator_Generate_NonMatchingExcludePattern_LeavesOutputUnaffected()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions(excludePatterns: ["Some.Nonexistent.Namespace.*"]));

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/SampleClass"));
        Assert.True(factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ISampleInterface"));
        Assert.True(factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ExcludedSample/ExcludedSampleClass"));
    }

    /// <summary>Validates that protected members are excluded when <see cref="ApiVisibility.Public"/> is selected.</summary>
    [Fact]
    public void DotNetGenerator_Generate_PublicVisibility_ExcludesProtectedMethod()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions(ApiVisibility.Public));

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: ProtectedMembersClass type page should exist (it is public)
        Assert.True(factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ProtectedMembersClass"));

        // Protected method should not get its own page under Public visibility
        Assert.False(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ProtectedMembersClass/ProtectedMethod"),
            "Protected method should not appear with Public visibility");
    }

    /// <summary>Validates that protected members are included when <see cref="ApiVisibility.PublicAndProtected"/> is selected.</summary>
    [Fact]
    public void DotNetGenerator_Generate_PublicAndProtectedVisibility_IncludesProtectedMethod()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions(ApiVisibility.PublicAndProtected));

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: ProtectedMethod has a parameter so it gets its own page
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ProtectedMembersClass/ProtectedMethod"),
            "Protected method should appear with PublicAndProtected visibility");
    }

    /// <summary>Validates that <see cref="ApiVisibility.All"/> includes private members that have parameters.</summary>
    [Fact]
    public void DotNetGenerator_Generate_AllVisibility_IncludesPrivateMembers()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions(ApiVisibility.All));

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: PrivateMethod has a parameter so it gets its own page under All visibility
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ProtectedMembersClass/PrivateMethod"),
            "Private method should appear with ApiVisibility.All");
    }

    /// <summary>Validates that a method with parameters receives its own Markdown member page.</summary>
    [Fact]
    public void DotNetGenerator_Generate_MethodWithParameters_CreatesMemberPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: GetGreeting(string name) has a parameter so it gets its own page
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/SampleClass/GetGreeting"),
            "Expected member page for SampleClass.GetGreeting");
    }

    /// <summary>Validates that a method with documented exceptions receives its own Markdown member page.</summary>
    [Fact]
    public void DotNetGenerator_Generate_MethodWithDocumentedExceptions_CreatesMemberPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: Connect(string host) has documented exceptions -> own page
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ExceptionDocClass/Connect"),
            "Expected member page for ExceptionDocClass.Connect");
    }

    /// <summary>Validates that a method with multi-line remarks receives its own Markdown member page.</summary>
    [Fact]
    public void DotNetGenerator_Generate_MethodWithMultiLineRemarks_CreatesMemberPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: Compute() has multi-line remarks -> own page
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/RemarksDocClass/Compute"),
            "Expected member page for RemarksDocClass.Compute");
    }

    /// <summary>Validates that XML summary text for a type appears as a paragraph in the type's Markdown page.</summary>
    [Fact]
    public void DotNetGenerator_Generate_TypeWithXmlSummary_WritesSummaryToParagraph()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SampleClass"];
        var paragraphs = writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("sample class"));
    }

    /// <summary>Validates that the <see cref="DotNetGenerator"/> constructor accepts valid options without throwing.</summary>
    [Fact]
    public void DotNetGenerator_Constructor_AcceptsAssemblyAndXmlPaths()
    {
        // Arrange / Act: constructor should not throw when given valid options
        var options = BuildOptions();
        var generator = new DotNetGenerator(options);

        // Assert: generator was created without exception and can produce output
        var factory = new InMemoryMarkdownWriterFactory();
        var exception = Record.Exception(() => generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext()));
        Assert.Null(exception);
    }

    /// <summary>Validates that Mono.Cecil assembly reading returns the expected types and members from the fixture assembly.</summary>
    [Fact]
    public void DotNetGenerator_ReadAssembly_WithMonoCecil_ReturnsTypesAndMembers()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: type pages exist for all expected fixture types
        Assert.True(factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/SampleClass"));
        Assert.True(factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ISampleInterface"));
        Assert.True(factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/SampleStatus"));
    }

    /// <summary>Validates that XML summary and remarks text appear in the generated Markdown output.</summary>
    [Fact]
    public void DotNetGenerator_ReadXmlComments_SummaryAndRemarks_AppearInMarkdown()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: SampleClass type page contains the XML summary text
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SampleClass"];
        var paragraphs = writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("sample class"));

        // Assert: RemarksDocClass type page exists and remarks member page has remarks text
        Assert.True(factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/RemarksDocClass/Compute", out var computeWriter));
        var computeParagraphs = computeWriter!.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(computeParagraphs, p => p.Contains("iterative"));
    }

    /// <summary>Validates that Public and PublicAndProtected visibility levels filter members as expected.</summary>
    [Fact]
    public void DotNetGenerator_Visibility_PublicPublicAndProtectedAll_FilterExpectedApis()
    {
        // Arrange: Public visibility — protected method should be absent
        var publicFactory = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(BuildOptions(ApiVisibility.Public)).Parse(new InMemoryContext()).Emit(publicFactory, new EmitConfig(), new InMemoryContext());

        // Arrange: PublicAndProtected visibility — protected method should be present
        var protectedFactory = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(BuildOptions(ApiVisibility.PublicAndProtected)).Parse(new InMemoryContext()).Emit(protectedFactory, new EmitConfig(), new InMemoryContext());

        // Assert: with Public visibility, protected method page does not exist
        Assert.False(publicFactory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ProtectedMembersClass/ProtectedMethod"));

        // Assert: with PublicAndProtected visibility, protected method page exists
        Assert.True(protectedFactory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ProtectedMembersClass/ProtectedMethod"));
    }

    /// <summary>Validates that toggling <see cref="DotNetGeneratorOptions.IncludeObsolete"/> controls whether obsolete APIs appear in output.</summary>
    [Fact]
    public void DotNetGenerator_IncludeObsolete_Toggle_ControlsObsoleteOutput()
    {
        // Arrange: IncludeObsolete=false
        var withoutObsolete = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(BuildOptions(includeObsolete: false)).Parse(new InMemoryContext()).Emit(withoutObsolete, new EmitConfig(), new InMemoryContext());

        // Arrange: IncludeObsolete=true
        var withObsolete = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(BuildOptions(includeObsolete: true)).Parse(new InMemoryContext()).Emit(withObsolete, new EmitConfig(), new InMemoryContext());

        // Assert: obsolete type excluded when false
        Assert.False(withoutObsolete.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ObsoleteClass"));

        // Assert: obsolete type included when true
        Assert.True(withObsolete.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ObsoleteClass"));
    }

    /// <summary>Validates that all members — regardless of parameters or docs — each receive separate Markdown files.</summary>
    [Fact]
    public void DotNetGenerator_AllMembers_GetSeparateFiles()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: method with parameters gets own page
        Assert.True(factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/SampleClass/GetGreeting"));

        // Assert: method with exception docs gets own page
        Assert.True(factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ExceptionDocClass/Connect"));

        // Assert: method with multi-line remarks gets own page
        Assert.True(factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/RemarksDocClass/Compute"));
    }

    /// <summary>Validates that generated output files follow the established naming convention.</summary>
    [Fact]
    public void DotNetGenerator_OutputFiles_FollowNamingConvention()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: root entrypoint is "api"
        Assert.True(factory.Writers.ContainsKey("api"));

        // Assert: root namespace file key is "{namespace}" (root level, not in a subfolder)
        Assert.True(factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures"));

        // Assert: type file key is "{namespace}/{typeName}"
        Assert.True(factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/SampleClass"));

        // Assert: member file key is "{namespace}/{typeName}/{memberName}"
        Assert.True(factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/SampleClass/GetGreeting"));
    }

    /// <summary>Validates end-to-end that a valid assembly and XML doc file produce Markdown output.</summary>
    [Fact]
    public void ApiMarkDotNet_Generate_ValidAssemblyAndXml_ProducesMarkdown()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the generator produces at least the api, namespace, and type pages
        Assert.True(factory.Writers.ContainsKey("api"));
        Assert.True(factory.Writers.Count > 3);
    }

    /// <summary>Validates that common .NET type names are rendered in a readable C# form in the generated output.</summary>
    [Fact]
    public void ApiMarkDotNet_TypeNames_CommonSignatures_RenderReadably()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: GetGreeting member page shows "string" (not "System.String") in the parameter table
        var greetingWriter = factory.Writers["ApiMark.DotNet.Fixtures/SampleClass/GetGreeting"];
        var tables = greetingWriter.Operations.OfType<TableOperation>().ToList();
        var paramTable = tables.FirstOrDefault(t => t.Headers.Contains("Parameter"));
        Assert.NotNull(paramTable);
        var nameRow = paramTable!.Rows.FirstOrDefault(r => r[0] == "name");
        Assert.NotNull(nameRow);
        Assert.Equal("string", nameRow![1]); // type column shows "string" not "System.String"
    }

    /// <summary>Validates that inline XML documentation references preserve symbol names in generated Markdown.</summary>
    [Fact]
    public void DotNetGenerator_Generate_InlineReferences_PreserveSymbolNamesInMarkdown()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var typeWriter = factory.Writers["ApiMark.DotNet.Fixtures/SampleStatusExtensions"];
        var typeParagraphs = typeWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains("Extensions for the SampleStatus enum.", typeParagraphs);

        var memberWriters = factory.Writers
            .Where(kvp => kvp.Key.StartsWith("ApiMark.DotNet.Fixtures/SampleStatusExtensions/IsPassed", StringComparison.Ordinal))
            .Select(kvp => kvp.Value)
            .ToList();
        Assert.Contains(
            memberWriters,
            writer => writer.Operations.OfType<ParagraphOperation>()
                .Any(p => p.Text == "Returns true when status is Active or Pending."));
    }

    /// <summary>Validates that static types render a <c>static class</c> signature.</summary>
    [Fact]
    public void DotNetGenerator_Generate_StaticTypeSignature_RendersStaticClass()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/ExceptionDocClass"];
        Assert.Contains(
            writer.Operations.OfType<SignatureOperation>(),
            s => s.Code == "public static class ExceptionDocClass");
    }

    /// <summary>Validates that extension method signatures include both <c>static</c> and <c>this</c>.</summary>
    [Fact]
    public void DotNetGenerator_Generate_ExtensionMethodSignature_RendersStaticAndThis()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var memberWriters = factory.Writers
            .Where(kvp => kvp.Key.StartsWith("ApiMark.DotNet.Fixtures/SampleStatusExtensions/IsPassed", StringComparison.Ordinal))
            .Select(kvp => kvp.Value)
            .ToList();
        Assert.Contains(
            memberWriters,
            writer => writer.Operations.OfType<SignatureOperation>()
                .Any(s => s.Code == "public static bool IsPassed(this SampleStatus status)"));
    }

    /// <summary>Validates that exception tables include both exception type and description text.</summary>
    [Fact]
    public void DotNetGenerator_Generate_ExceptionDocumentation_RendersExceptionTypeAndDescription()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/ExceptionDocClass/Connect"];
        var exceptionTable = writer.Operations
            .OfType<TableOperation>()
            .Single(t => t.Headers.Length == 2 && t.Headers[0] == "Exception" && t.Headers[1] == "Description");
        Assert.Contains(
            exceptionTable.Rows,
            row => row[0] == "InvalidOperationException" && row[1] == "Already connected.");
    }

    /// <summary>Validates that overloaded complex methods share a single Markdown file.</summary>
    [Fact]
    public void DotNetGenerator_Generate_OverloadedMethods_CreateSharedMemberPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.Equal(
            1,
            factory.Writers.Keys.Count(k =>
                k.StartsWith("ApiMark.DotNet.Fixtures/SampleStatusExtensions/IsPassed", StringComparison.Ordinal)));
    }

    /// <summary>Validates that overloads differing only in <c>int</c> vs <c>int[]</c> share one Markdown file.</summary>
    [Fact]
    public void DotNetGenerator_Generate_IntVsIntArray_CreateSharedMemberPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/IntVsIntArrayClass/Process"),
            "Expected a single Process member page");
        Assert.False(
            factory.Writers.Keys.Any(k =>
                k.StartsWith("ApiMark.DotNet.Fixtures/IntVsIntArrayClass/Process-", StringComparison.Ordinal)),
            "Did not expect parameter-type suffixed overload pages");
    }

    /// <summary>Validates that <c>api.md</c> contains a file naming and path convention section.</summary>
    [Fact]
    public void DotNetGenerator_Generate_ApiMd_ContainsNamingConventionSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: api.md has a heading for the naming convention section
        var apiWriter = factory.Writers["api"];
        var headings = apiWriter.Operations.OfType<HeadingOperation>().Select(h => h.Text).ToList();
        Assert.Contains(headings, h => h.Contains("File Naming") || h.Contains("Convention"));

        // Assert: a convention table is present in api.md; after FIX 6 the convention table is last
        // (namespace table comes first so consumers reach the namespace listing without scrolling)
        var tables = apiWriter.Operations.OfType<TableOperation>().ToList();
        Assert.True(tables.Count >= 2, "Expected at least a namespace table and a convention table in api.md");
        Assert.DoesNotContain(tables.Last().Rows, row => row[1].Contains("ParamTypes", StringComparison.Ordinal));
        Assert.Contains(tables.Last().Rows, row => row[1].Contains("{MemberName}.md", StringComparison.Ordinal));
    }

    /// <summary>Validates that <c>api.md</c> lists all namespaces (root and child) with a type count column.</summary>
    [Fact]
    public void DotNetGenerator_Generate_ApiMd_ListsAllNamespacesWithTypeCount()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: namespace table is first in api.md and contains both root and child namespaces
        var apiWriter = factory.Writers["api"];
        var tables = apiWriter.Operations.OfType<TableOperation>().ToList();
        var nsTable = tables.First();
        Assert.Contains(nsTable.Rows, row => row[0].Contains("ApiMark.DotNet.Fixtures"));
        Assert.Contains(nsTable.Rows, row => row[0].Contains("ApiMark.DotNet.Fixtures.Inner"));

        // Assert: each row has three columns — Namespace, Types, Description
        Assert.All(nsTable.Rows, row => Assert.Equal(3, row.Length));
    }

    /// <summary>Validates that the root namespace page lists its immediate child namespace.</summary>
    [Fact]
    public void DotNetGenerator_Generate_RootNamespacePage_ListsChildNamespace()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the root namespace page exists at root level
        Assert.True(factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures", out var rootNsWriter),
            "Root namespace page must be at root level");
        var tables = rootNsWriter.Operations.OfType<TableOperation>().ToList();
        Assert.True(tables.Count > 0, "Root namespace page must have at least one table");
        Assert.Contains(tables, t => t.Rows.Any(row => row[0].Contains("Inner")));
    }

    /// <summary>Validates that a type in a child namespace gets a page in the correct hierarchical path.</summary>
    [Fact]
    public void DotNetGenerator_Generate_ChildNamespaceType_CreatesPageInCorrectPath()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: InnerNamespaceClass is in ApiMark.DotNet.Fixtures.Inner namespace
        // Its page should be at ApiMark.DotNet.Fixtures/Inner/InnerNamespaceClass
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/Inner/InnerNamespaceClass"),
            "Expected type page for InnerNamespaceClass at ApiMark.DotNet.Fixtures/Inner/InnerNamespaceClass");
    }

    /// <summary>Validates that a complex member in a child namespace type gets a page in the correct hierarchical path.</summary>
    [Fact]
    public void DotNetGenerator_Generate_ChildNamespaceMember_CreatesPageInCorrectPath()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: Compute(int input) in InnerNamespaceClass has a parameter → gets its own page
        // Page must be at ApiMark.DotNet.Fixtures/Inner/InnerNamespaceClass/Compute
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/Inner/InnerNamespaceClass/Compute"),
            "Expected member page for InnerNamespaceClass.Compute at the correct hierarchical path");
    }

    /// <summary>Validates that a sealed class produces a type page with <c>sealed</c> in the signature.</summary>
    [Fact]
    public void DotNetGenerator_Generate_SealedClass_SignatureContainsSealedModifier()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: type page exists
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/SealedClass", out var writer),
            "Expected a type page for SealedClass");
        var signature = writer.Operations.OfType<SignatureOperation>().FirstOrDefault();
        Assert.NotNull(signature);
        Assert.Contains("sealed", signature.Code, StringComparison.Ordinal);
    }

    /// <summary>Validates that overloaded methods on <see cref="IntVsIntArrayClass"/> are documented together.</summary>
    [Fact]
    public void DotNetGenerator_Generate_OverloadedMethods_ShareOneMemberPageWithAllSignatures()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        var writer = factory.Writers["ApiMark.DotNet.Fixtures/IntVsIntArrayClass/Process"];
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();

        Assert.Contains("public static void Process(int value)", signatures);
        Assert.Contains("public static void Process(int[] values)", signatures);
    }

    /// <summary>Validates that the type page lists overloaded methods once and notes the overload count.</summary>
    [Fact]
    public void DotNetGenerator_Generate_TypePage_ListsOverloadedMethodOnceWithCount()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: after FIX 11, methods appear under a "Methods" sub-table with "Returns" header;
        // SampleStatusExtensions is a static class with only methods so there is exactly one table
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SampleStatusExtensions"];
        var tables = writer.Operations.OfType<TableOperation>().ToList();
        var memberTable = tables.Single(t => t.Headers.Contains("Returns"));
        var isPassedRows = memberTable.Rows.Where(row => row[0].Contains("IsPassed", StringComparison.Ordinal)).ToList();

        Assert.Single(isPassedRows);
        Assert.Contains("(2 overloads)", isPassedRows[0][0], StringComparison.Ordinal);
        Assert.Contains("(SampleStatusExtensions/IsPassed.md)", isPassedRows[0][0], StringComparison.Ordinal);
    }

    /// <summary>Validates that the child namespace page is placed inside the root namespace folder.</summary>
    [Fact]
    public void DotNetGenerator_Generate_ChildNamespacePage_PlacedInRootNamespaceFolder()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: child namespace page is at {rootNs}/{childShortName}, not at {childFullName}/{childFullName}
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/Inner"),
            "Child namespace page must be at ApiMark.DotNet.Fixtures/Inner");
        Assert.False(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures.Inner/ApiMark.DotNet.Fixtures.Inner"),
            "Child namespace page must NOT be at the old flat path");
    }

    /// <summary>Validates that a parameterless method is displayed with <c>()</c> in the Methods sub-table.</summary>
    [Fact]
    public void DotNetGenerator_MethodsTable_ParameterlessMethod_DisplaysMemberWithParentheses()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: find the Methods section on the SampleClass type page
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SampleClass"];
        var operations = writer.Operations.ToList();
        var methodsIndex = operations.FindIndex(op => op is HeadingOperation h && h.Text == "Methods");
        Assert.True(methodsIndex >= 0, "Expected 'Methods' heading on SampleClass page");
        var methodsTable = operations.Skip(methodsIndex + 1).OfType<TableOperation>().First();

        // Reset() is parameterless — the Member cell must still show the "()" suffix (as a link now all members get pages)
        Assert.Contains(methodsTable.Rows, row => row[0].Contains("Reset()"));
    }

    /// <summary>Validates that the compiler-generated parameterless constructor is displayed with <c>()</c> in the Constructors sub-table.</summary>
    [Fact]
    public void DotNetGenerator_ConstructorsTable_ParameterlessConstructor_DisplaysMemberWithParentheses()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: find the Constructors section on the SampleClass type page
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SampleClass"];
        var operations = writer.Operations.ToList();
        var ctorIndex = operations.FindIndex(op => op is HeadingOperation h && h.Text == "Constructors");
        Assert.True(ctorIndex >= 0, "Expected 'Constructors' heading on SampleClass page");
        var ctorTable = operations.Skip(ctorIndex + 1).OfType<TableOperation>().First();

        // Compiler-generated SampleClass() must appear with the "()" suffix (as a link now all members get pages)
        Assert.Contains(ctorTable.Rows, row => row[0].Contains("SampleClass()"));
    }

    /// <summary>Validates that a method with parameters shows parameter types — indicated by a <c>(</c> character — in the Methods sub-table Member cell.</summary>
    [Fact]
    public void DotNetGenerator_MethodsTable_MethodWithParameters_IncludesParameterTypesInMemberCell()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: find the Methods section on the SampleClass type page
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SampleClass"];
        var operations = writer.Operations.ToList();
        var methodsIndex = operations.FindIndex(op => op is HeadingOperation h && h.Text == "Methods");
        Assert.True(methodsIndex >= 0, "Expected 'Methods' heading on SampleClass page");
        var methodsTable = operations.Skip(methodsIndex + 1).OfType<TableOperation>().First();

        // GetGreeting(string name) must show a "(" in the Member cell, confirming parameter types are emitted
        Assert.Contains(
            methodsTable.Rows,
            row => row[0].Contains("GetGreeting", StringComparison.Ordinal) &&
                   row[0].Contains('('));
    }

    /// <summary>Validates that the SampleClass type page has a Constructors sub-section whose table headers are <c>Member</c> and <c>Description</c> (no Returns column).</summary>
    [Fact]
    public void DotNetGenerator_TypePage_SampleClass_HasConstructorsSectionWithCorrectHeaders()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: Constructors heading is present
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SampleClass"];
        var operations = writer.Operations.ToList();
        var ctorIndex = operations.FindIndex(op => op is HeadingOperation h && h.Text == "Constructors");
        Assert.True(ctorIndex >= 0, "Expected 'Constructors' heading on SampleClass page");

        // Assert: the table immediately following uses Member + Description (constructors have no Returns column)
        var ctorTable = operations.Skip(ctorIndex + 1).OfType<TableOperation>().First();
        Assert.Equal(ConstructorTableHeaders, ctorTable.Headers);
    }

    /// <summary>Validates that the SampleClass type page has a Properties sub-section whose table headers are <c>Member</c>, <c>Type</c>, and <c>Description</c>.</summary>
    [Fact]
    public void DotNetGenerator_TypePage_SampleClass_HasPropertiesSectionWithCorrectHeaders()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: Properties heading is present
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SampleClass"];
        var operations = writer.Operations.ToList();
        var propIndex = operations.FindIndex(op => op is HeadingOperation h && h.Text == "Properties");
        Assert.True(propIndex >= 0, "Expected 'Properties' heading on SampleClass page");

        // Assert: the table immediately following uses Member + Type + Description
        var propTable = operations.Skip(propIndex + 1).OfType<TableOperation>().First();
        Assert.Equal(PropertyTableHeaders, propTable.Headers);
    }

    /// <summary>Validates that the SampleClass type page has a Methods sub-section whose table uses <c>Returns</c> (not <c>Type</c>) as the second header.</summary>
    [Fact]
    public void DotNetGenerator_TypePage_SampleClass_HasMethodsSectionWithReturnsHeader()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: Methods heading is present
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SampleClass"];
        var operations = writer.Operations.ToList();
        var methodsIndex = operations.FindIndex(op => op is HeadingOperation h && h.Text == "Methods");
        Assert.True(methodsIndex >= 0, "Expected 'Methods' heading on SampleClass page");

        // Assert: the table immediately following uses Member + Returns + Description ("Returns" not "Type")
        var methodsTable = operations.Skip(methodsIndex + 1).OfType<TableOperation>().First();
        Assert.Equal(MethodTableHeaders, methodsTable.Headers);
    }

    /// <summary>Validates that no cell in the Constructors sub-table equals <c>"void"</c>.</summary>
    [Fact]
    public void DotNetGenerator_ConstructorsTable_SampleClass_ContainsNoVoidCells()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: find the Constructors table on the SampleClass type page
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SampleClass"];
        var operations = writer.Operations.ToList();
        var ctorIndex = operations.FindIndex(op => op is HeadingOperation h && h.Text == "Constructors");
        Assert.True(ctorIndex >= 0, "Expected 'Constructors' heading on SampleClass page");
        var ctorTable = operations.Skip(ctorIndex + 1).OfType<TableOperation>().First();

        // Constructors have no return type — "void" must never appear in any cell
        Assert.DoesNotContain(ctorTable.Rows, row => row.Any(cell => cell == "void"));
    }

    /// <summary>Validates that the compiler-generated <c>value__</c> enum backing field is absent from the SampleStatus enum type page.</summary>
    [Fact]
    public void DotNetGenerator_EnumPage_ValueUnderscoreBackingField_IsAbsent()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: no table on the SampleStatus page lists "value__" as a member
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SampleStatus"];
        var tables = writer.Operations.OfType<TableOperation>().ToList();
        Assert.DoesNotContain(tables, t => t.Rows.Any(row => row[0] == "value__"));
    }

    /// <summary>Validates that a type whose XML <c>&lt;summary&gt;</c> spans multiple lines is stored as a single-line description in the namespace type table.</summary>
    [Fact]
    public void DotNetGenerator_NamespacePage_MultiLineSummary_CollapsesToSingleLine()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: find the type table on the ApiMark.DotNet.Fixtures namespace page
        var nsWriter = factory.Writers["ApiMark.DotNet.Fixtures"];
        var typeTable = nsWriter.Operations
            .OfType<TableOperation>()
            .First(t => t.Headers[0] == "Type");

        // MultiLineSummaryClass has a multi-line XML summary — it must appear as a single line (no \n)
        var row = typeTable.Rows.FirstOrDefault(r => r[0].Contains("MultiLineSummaryClass", StringComparison.Ordinal));
        Assert.NotNull(row);
        Assert.DoesNotContain("\n", row[1], StringComparison.Ordinal);
    }

    /// <summary>Validates that a pipe character in an XML summary is faithfully captured in the namespace type table description cell.</summary>
    [Fact]
    public void DotNetGenerator_NamespacePage_PipeInSummary_PreservedInTableCell()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: find PipeSummaryClass in the namespace type table
        var nsWriter = factory.Writers["ApiMark.DotNet.Fixtures"];
        var typeTable = nsWriter.Operations
            .OfType<TableOperation>()
            .First(t => t.Headers[0] == "Type");
        var row = typeTable.Rows.FirstOrDefault(r => r[0].Contains("PipeSummaryClass", StringComparison.Ordinal));
        Assert.NotNull(row);

        // The in-memory writer stores raw cell values; FileMarkdownWriter handles pipe-escaping for the file.
        // This test verifies the pipe character from the XML summary is preserved in the captured description.
        Assert.Contains("|", row[1], StringComparison.Ordinal);
    }

    /// <summary>Validates that <c>api.md</c> has an H1 heading whose text ends with <c>API Reference</c>.</summary>
    [Fact]
    public void DotNetGenerator_ApiMd_H1Heading_EndsWithApiReference()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: api.md contains exactly one H1 heading that ends with "API Reference"
        var apiWriter = factory.Writers["api"];
        var h1 = apiWriter.Operations.OfType<HeadingOperation>().FirstOrDefault(h => h.Level == 1);
        Assert.NotNull(h1);
        Assert.EndsWith("API Reference", h1.Text, StringComparison.Ordinal);
    }

    /// <summary>Validates that the fixture assembly description is emitted as a paragraph in <c>api.md</c>.</summary>
    [Fact]
    public void DotNetGenerator_ApiMd_AssemblyDescription_EmittedAsParagraph()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: api.md contains a paragraph matching the <Description> set in the fixture .csproj
        var apiWriter = factory.Writers["api"];
        var paragraphs = apiWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(
            paragraphs,
            p => p.Contains("Test fixture assemblies", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Validates that the file naming and path convention table is the last table in <c>api.md</c>.</summary>
    [Fact]
    public void DotNetGenerator_ApiMd_ConventionTable_IsLastTable()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: api.md has at least a namespace table and the convention appendix table
        var apiWriter = factory.Writers["api"];
        var tables = apiWriter.Operations.OfType<TableOperation>().ToList();
        Assert.True(tables.Count >= 2, "Expected at least a namespace table and a convention table in api.md");

        // The last table must be the path-convention appendix, identifiable by {Namespace}.md rows
        var lastTable = tables.Last();
        Assert.Contains(
            lastTable.Rows,
            row => row[1].Contains("{Namespace}.md", StringComparison.Ordinal));
    }

    /// <summary>Validates that a member with no XML doc comment shows <c>*No description provided.*</c> in its parent table.</summary>
    [Fact]
    public void DotNetGenerator_TypePage_UndocumentedMember_ShowsNoDescriptionPlaceholder()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: find the Methods table on the SampleClass type page
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SampleClass"];
        var operations = writer.Operations.ToList();
        var methodsIndex = operations.FindIndex(op => op is HeadingOperation h && h.Text == "Methods");
        Assert.True(methodsIndex >= 0, "Expected 'Methods' heading on SampleClass page");
        var methodsTable = operations.Skip(methodsIndex + 1).OfType<TableOperation>().First();

        // Refresh() has no XML doc comment — the Description cell must use the standard no-description placeholder.
        // All members now get their own pages, so the Member cell is a Markdown link containing "Refresh()".
        var refreshRow = methodsTable.Rows.FirstOrDefault(row => row[0].Contains("Refresh()"));
        Assert.NotNull(refreshRow);
        Assert.Equal("*No description provided.*", refreshRow[2]);
    }

    /// <summary>Validates that an <c>internal static class NamespaceDoc</c> is excluded from the namespace type table and its summary is emitted as a paragraph.</summary>
    [Fact]
    public void DotNetGenerator_NamespacePage_NamespaceDocClass_ExcludedFromTypeListing()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the namespace type table must not contain NamespaceDoc as a listed type
        var nsWriter = factory.Writers["ApiMark.DotNet.Fixtures"];
        var operations = nsWriter.Operations.ToList();
        var typeTable = operations.OfType<TableOperation>().FirstOrDefault(t => t.Headers[0] == "Type");
        if (typeTable != null)
        {
            Assert.DoesNotContain(
                typeTable.Rows,
                row => row[0].Contains("NamespaceDoc", StringComparison.Ordinal));
        }

        // Assert: the NamespaceDoc <summary> must appear as a paragraph on the namespace page
        var paragraphs = operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(
            paragraphs,
            p => p.Contains("Contains types for testing", StringComparison.Ordinal));
    }

    /// <summary>Validates that the NamespaceDoc XML summary appears as a paragraph on the namespace page.</summary>
    [Fact]
    public void DotNetGenerator_NamespacePage_NamespaceDocSummary_AppearsAsNamespaceDescription()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the NamespaceDoc <summary> must appear as a paragraph on the namespace page
        var nsWriter = factory.Writers["ApiMark.DotNet.Fixtures"];
        var paragraphs = nsWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(
            paragraphs,
            p => p.Contains("Contains types for testing", StringComparison.Ordinal));
    }

    /// <summary>Validates that the NamespaceDoc XML remarks appear as a paragraph on the namespace page.</summary>
    [Fact]
    public void DotNetGenerator_NamespacePage_NamespaceDocRemarks_AppearsOnNamespacePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the NamespaceDoc <remarks> must appear as a paragraph on the namespace page
        var nsWriter = factory.Writers["ApiMark.DotNet.Fixtures"];
        var paragraphs = nsWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(
            paragraphs,
            p => p.Contains("Namespace-level remarks for verification", StringComparison.Ordinal));
    }

    /// <summary>Validates that the NamespaceDoc XML example is emitted as a code block on the namespace page.</summary>
    [Fact]
    public void DotNetGenerator_NamespacePage_NamespaceDocExample_EmitsCodeBlock()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the NamespaceDoc <example><code> must be emitted as a code block on the namespace page
        var nsWriter = factory.Writers["ApiMark.DotNet.Fixtures"];
        var codeBlocks = nsWriter.Operations.OfType<CodeBlockOperation>().Select(c => c.Code).ToList();
        Assert.Contains(
            codeBlocks,
            c => c.Contains("var x = 1", StringComparison.Ordinal));
    }

    /// <summary>Validates that a type's <c>&lt;remarks&gt;</c> bullet list is rendered as Markdown dash items on the type page.</summary>
    [Fact]
    public void DotNetGenerator_Generate_RemarksWithBulletList_RendersListInMarkdown()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the BulletListDocClass type page must contain the rendered bullet items
        var typeWriter = factory.Writers["ApiMark.DotNet.Fixtures/BulletListDocClass"];
        var paragraphs = typeWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(
            paragraphs,
            p => p.Contains("- Parse the input source.", StringComparison.Ordinal) &&
                 p.Contains("- Emit the Markdown output.", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that two members whose names differ only in case are combined onto a
    ///     single page named after the lowercase key.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_CaseCollisionClass_CreatesCombinedPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the combined page is written at the lowercase key path
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/CaseCollisionClass/name"),
            "Expected a combined page at the lowercase key 'name' for the colliding members");
    }

    /// <summary>
    ///     Validates that the generator does not create a separate page for the upper-case
    ///     member when a case-insensitive collision exists.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_CaseCollisionClass_DoesNotCreateSeparateCasedPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: no page should be created using the exact-case name "Name" when a collision
        // exists — the combined lowercase page replaces all individual pages for the group
        Assert.False(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/CaseCollisionClass/Name"),
            "Expected no separate page for 'Name' when a case-insensitive collision exists");
    }

    /// <summary>
    ///     Validates that the combined collision page contains H2 headings for both the
    ///     field <c>name</c> and the property <c>Name</c>.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_CaseCollisionClass_CombinedPageContainsBothMembers()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: combined page exists
        Assert.True(factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/CaseCollisionClass/name", out var writer));

        // Assert: both members appear as distinct H2 headings on the combined page
        var level2Headings = writer.Operations
            .OfType<HeadingOperation>()
            .Where(h => h.Level == 2)
            .Select(h => h.Text)
            .ToList();
        Assert.Contains(level2Headings, h => h.StartsWith("name", StringComparison.Ordinal));
        Assert.Contains(level2Headings, h => h.StartsWith("Name", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a class implementing an interface shows the interface name in its
    ///     type signature code block so readers can see the inheritance relationship at a
    ///     glance without opening the source file.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_SampleImplementation_TypeSignatureShowsInterface()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: SampleImplementation type page must exist
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/SampleImplementation", out var writer),
            "Expected type page for SampleImplementation");
        var signature = writer.Operations.OfType<SignatureOperation>().FirstOrDefault();
        Assert.NotNull(signature);
        Assert.Contains(": ISampleInterface", signature.Code, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the <c>SampleImplementation.Name</c> property detail page contains the
    ///     summary text inherited from <c>ISampleInterface.Name</c> via a bare <c>&lt;inheritdoc /&gt;</c>
    ///     tag, proving the full pipeline from Mono.Cecil inheritance mapping through XmlDocReader
    ///     resolution to emitted Markdown.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_SampleImplementationNameMemberPage_UsesInheritedSummary()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: SampleImplementation.Name member detail page must exist
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/SampleImplementation/Name", out var writer),
            "Expected member detail page for SampleImplementation.Name");
        var paragraphs = writer!.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("Gets the name."));
    }

    /// <summary>
    ///     Validates that the <c>SampleImplementation.Execute</c> method detail page contains both
    ///     the summary text and the <c>input</c> parameter description inherited from
    ///     <c>ISampleInterface.Execute</c> via a bare <c>&lt;inheritdoc /&gt;</c> tag.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_SampleImplementationExecuteMemberPage_UsesInheritedSummaryAndParamDescription()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: SampleImplementation.Execute member detail page must exist
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/SampleImplementation/Execute", out var writer),
            "Expected member detail page for SampleImplementation.Execute");

        // Assert: inherited summary paragraph is present
        var paragraphs = writer!.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("Executes the specified input."));

        // Assert: inherited parameter description appears in the parameter table
        var paramTable = writer.Operations
            .OfType<TableOperation>()
            .FirstOrDefault(t => t.Headers.Contains("Parameter", StringComparer.Ordinal));
        Assert.NotNull(paramTable);
        Assert.Contains(
            paramTable!.Rows,
            row => row[0] == "input" && row[2].Contains("The input to execute."));
    }

    /// <summary>
    ///     Validates that an enum type signature does not include a base class (such as
    ///     <c>System.Enum</c>) so that well-known implicit bases are suppressed and the
    ///     signature remains clean and readable.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_EnumTypeSignature_HasNoBaseClass()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: SampleStatus enum type page must exist
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/SampleStatus", out var writer),
            "Expected type page for SampleStatus");
        var signature = writer.Operations.OfType<SignatureOperation>().FirstOrDefault();
        Assert.NotNull(signature);
        Assert.DoesNotContain("System.Enum", signature.Code, StringComparison.Ordinal);
        Assert.DoesNotContain(" : ", signature.Code, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a method returning an intra-assembly type emits a Markdown link in
    ///     the Returns column of the type page's Methods table.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_IntraAssemblyReturnType_EmitsMarkdownLinkInReturnsCell()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: TypeLinkFixture type page must exist
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/TypeLinkFixture", out var writer),
            "Expected type page for TypeLinkFixture");
        var operations = writer.Operations.ToList();
        var methodsIndex = operations.FindIndex(op => op is HeadingOperation h && h.Text == "Methods");
        Assert.True(methodsIndex >= 0, "Expected 'Methods' heading on TypeLinkFixture page");
        var methodsTable = operations.Skip(methodsIndex + 1).OfType<TableOperation>().First();

        // Returns column (index 1) for GetSampleClass() must contain a Markdown link, not plain text
        var row = methodsTable.Rows.FirstOrDefault(r => r[0].Contains("GetSampleClass", StringComparison.Ordinal));
        Assert.NotNull(row);
        Assert.Contains("[SampleClass]", row[1], StringComparison.Ordinal);
        Assert.Contains("SampleClass.md", row[1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a method accepting an external non-System type causes an "External Types"
    ///     section to appear at the bottom of the member's detail page.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_ExternalNonSystemParameterType_EmitsExternalTypesSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: Log member page must exist
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/TypeLinkFixture/Log", out var writer),
            "Expected member page for TypeLinkFixture.Log");
        var headings = writer.Operations.OfType<HeadingOperation>().Select(h => h.Text).ToList();
        Assert.Contains("External Types", headings, StringComparer.Ordinal);

        // Assert: the External Types table must list ILogger with the Microsoft.Extensions.Logging namespace
        var externalTable = writer.Operations
            .OfType<TableOperation>()
            .FirstOrDefault(t => t.Headers.Length == 2 && t.Headers[0] == "Type" && t.Headers[1] == "Namespace");
        Assert.NotNull(externalTable);
        Assert.Contains(
            externalTable!.Rows,
            row => row[0].Contains("ILogger", StringComparison.Ordinal) &&
                   row[1].Contains("Microsoft.Extensions.Logging", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a delegate type produces a page with a <c>public delegate</c>
    ///     signature rather than <c>public sealed class</c>, using the parameter list
    ///     from the compiler-injected <c>Invoke</c> method.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_DelegateType_EmitsDelegateSignature()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: type page must exist
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/ServiceEvent", out var writer),
            "Expected type page for ServiceEvent delegate");

        var signature = writer.Operations.OfType<SignatureOperation>().FirstOrDefault();
        Assert.NotNull(signature);

        // Must use the delegate keyword, not sealed class
        Assert.Contains("public delegate", signature.Code, StringComparison.Ordinal);
        Assert.DoesNotContain("sealed class", signature.Code, StringComparison.Ordinal);

        // Must include the return type and each parameter
        Assert.Contains("void", signature.Code, StringComparison.Ordinal);
        Assert.Contains("DateTime timestamp", signature.Code, StringComparison.Ordinal);
        Assert.Contains("string service", signature.Code, StringComparison.Ordinal);
        Assert.Contains("object[] arguments", signature.Code, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a delegate type page contains no member tables — the
    ///     compiler-injected <c>Invoke</c>, <c>BeginInvoke</c>, <c>EndInvoke</c>, and
    ///     synthetic constructor must not appear in the output.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_DelegateType_HasNoMemberTables()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: type page must exist
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/ServiceEvent", out var writer),
            "Expected type page for ServiceEvent delegate");

        // No table rows must mention compiler-injected member names
        var allTableText = writer.Operations
            .OfType<TableOperation>()
            .SelectMany(t => t.Rows)
            .SelectMany(r => r)
            .ToList();

        Assert.DoesNotContain(allTableText, cell =>
            cell.Contains("Invoke", StringComparison.Ordinal) ||
            cell.Contains("BeginInvoke", StringComparison.Ordinal) ||
            cell.Contains("EndInvoke", StringComparison.Ordinal));

        // No member detail pages must be created for delegate synthetic members
        Assert.False(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ServiceEvent/Invoke"),
            "Delegate Invoke method must not produce a detail page");
    }

    /// <summary>
    ///     Validates that a generic delegate type signature includes the type parameter list,
    ///     e.g. <c>public delegate TResult SampleTransform&lt;TInput, TResult&gt;(TInput input)</c>.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_GenericDelegateType_IncludesTypeParameters()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: type page must exist (arity flattened: SampleTransform`2 → SampleTransform2)
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/SampleTransform2", out var writer),
            "Expected type page for SampleTransform generic delegate");
        var signature = writer.Operations.OfType<SignatureOperation>().FirstOrDefault();
        Assert.NotNull(signature);

        Assert.Contains("public delegate", signature.Code, StringComparison.Ordinal);
        Assert.Contains("TResult", signature.Code, StringComparison.Ordinal);
        Assert.Contains("TInput", signature.Code, StringComparison.Ordinal);
        Assert.Contains("TInput input", signature.Code, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a method returning <c>string[]?</c> (a nullable array reference)
    ///     renders the return type as <c>string[]?</c> — not <c>string[]</c> — in both the
    ///     type page's Methods table and the member's detail page signature.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_NullableArrayReturnType_RendersWithQuestionMark()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: ArrayAndNullableClass type page must exist
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/ArrayAndNullableClass", out var typePage),
            "Expected type page for ArrayAndNullableClass");
        var methodsTable = typePage.Operations
            .OfType<TableOperation>()
            .FirstOrDefault(t => t.Headers.Contains("Returns", StringComparer.Ordinal));
        Assert.NotNull(methodsTable);
        var nullableRow = methodsTable.Rows.FirstOrDefault(
            r => r[0].Contains("GetNullableNames", StringComparison.Ordinal));
        Assert.NotNull(nullableRow);
        Assert.Contains("string[]?", nullableRow[1], StringComparison.Ordinal);

        // Assert: the member detail page signature also shows string[]?
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/ArrayAndNullableClass/GetNullableNames", out var detailPage),
            "Expected detail page for GetNullableNames");
        var signature = detailPage.Operations.OfType<SignatureOperation>().FirstOrDefault();
        Assert.NotNull(signature);
        Assert.Contains("string[]?", signature.Code, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a constructor's XML <c>&lt;summary&gt;</c> text appears as a paragraph
    ///     on the constructor's member detail page.
    ///     Regression test for the <c>.ctor</c> → <c>#ctor</c> XML doc ID mismatch that caused
    ///     constructor summaries to be silently discarded.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_ConstructorWithXmlSummary_WritesSummaryToMemberPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: OperatorsStruct has a documented constructor; its detail page must exist
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/OperatorsStruct/OperatorsStruct", out var writer),
            "Expected member page for OperatorsStruct constructor");
        var paragraphs = writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("Initializes a new instance"));
        Assert.DoesNotContain(paragraphs, p => p.Contains("No description provided"));
    }

    /// <summary>
    ///     Validates that XML <c>&lt;param&gt;</c> descriptions for constructor parameters appear
    ///     in the parameter table on the constructor's member detail page.
    ///     Regression test for the same <c>.ctor</c> → <c>#ctor</c> XML doc ID mismatch.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_ConstructorWithXmlParams_WritesParamDescriptionsToMemberPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the constructor detail page has a Parameter table whose description column
        // contains the XML-authored text for the 'value' parameter
        Assert.True(factory.Writers.TryGetValue(
            "ApiMark.DotNet.Fixtures/OperatorsStruct/OperatorsStruct", out var writer));
        var paramTable = writer!.Operations
            .OfType<TableOperation>()
            .FirstOrDefault(t => t.Headers.Contains("Parameter", StringComparer.Ordinal));
        Assert.NotNull(paramTable);
        Assert.Contains(
            paramTable!.Rows,
            row => row[0] == "value" && row[2].Contains("scalar value"));
    }

    /// <summary>
    ///     Validates that a type with operator overloads produces a dedicated
    ///     <c>operators.md</c> page rather than individual per-operator pages.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_TypeWithOperators_CreatesOperatorsPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: shared operators page must exist
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/OperatorsStruct/operators"),
            "Expected operators.md page for OperatorsStruct");

        // Assert: no individual per-operator pages must be created
        Assert.False(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/OperatorsStruct/op_Addition"),
            "Individual operator pages must not be created; all operators share operators.md");
    }

    /// <summary>
    ///     Validates that the type page for a type with operators includes an Operators section
    ///     with a single row linking to <c>operators.md</c>.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_TypeWithOperators_TypePageHasOperatorsSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: Operators heading is present on the type page
        var typeWriter = factory.Writers["ApiMark.DotNet.Fixtures/OperatorsStruct"];
        var operations = typeWriter.Operations.ToList();
        var opsIndex = operations.FindIndex(op => op is HeadingOperation h && h.Text == "Operators");
        Assert.True(opsIndex >= 0, "Expected 'Operators' heading on OperatorsStruct type page");

        // Assert: the table immediately following links to operators.md
        var opsTable = operations.Skip(opsIndex + 1).OfType<TableOperation>().First();
        Assert.Single(opsTable.Rows);
        Assert.Contains("operators.md", opsTable.Rows[0][0], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the <c>operators.md</c> page contains the XML summary text for each
    ///     documented operator overload.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_TypeWithOperators_OperatorsPageContainsSummaries()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the operators page exists and its summaries come from the XML doc
        Assert.True(factory.Writers.TryGetValue(
            "ApiMark.DotNet.Fixtures/OperatorsStruct/operators", out var writer));
        var paragraphs = writer!.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("Adds two instances"));
        Assert.Contains(paragraphs, p => p.Contains("Subtracts one instance"));
    }

    /// <summary>
    ///     Validates that the <c>operators.md</c> page uses C# operator symbols (e.g. <c>operator +</c>)
    ///     as H2 headings rather than the raw IL names (e.g. <c>op_Addition</c>).
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_TypeWithOperators_OperatorsPageUsesSymbolHeadings()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.Writers.TryGetValue(
            "ApiMark.DotNet.Fixtures/OperatorsStruct/operators", out var writer));
        var h2Headings = writer!.Operations
            .OfType<HeadingOperation>()
            .Where(h => h.Level == 2)
            .Select(h => h.Text)
            .ToList();

        // Headings must use C# symbols, not IL names
        Assert.Contains(h2Headings, h => h.Contains("operator +"));
        Assert.Contains(h2Headings, h => h.Contains("operator -"));
        Assert.DoesNotContain(h2Headings, h => h.Contains("op_Addition"));
        Assert.DoesNotContain(h2Headings, h => h.Contains("op_Subtraction"));
    }

    /// <summary>
    ///     Validates that XML documentation summaries authored on <c>implicit</c> and
    ///     <c>explicit</c> conversion operators are resolved via the <c>~ReturnType</c>
    ///     XML doc ID suffix and appear on the <c>operators.md</c> page.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_TypeWithConversionOperators_OperatorsPageContainsSummaries()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: conversion operator summaries must appear on the operators page
        Assert.True(factory.Writers.TryGetValue(
            "ApiMark.DotNet.Fixtures/OperatorsStruct/operators", out var writer));
        var paragraphs = writer!.Operations
            .OfType<ParagraphOperation>()
            .Select(p => p.Text)
            .ToList();

        Assert.Contains(paragraphs, p => p.Contains("Implicitly converts an instance"));
        Assert.Contains(paragraphs, p => p.Contains("Explicitly converts an instance"));
    }

    /// <summary>
    ///     Validates that conversion operator headings on <c>operators.md</c> use C# syntax
    ///     (<c>implicit operator T</c> / <c>explicit operator T</c>) rather than the raw IL
    ///     method names (<c>op_Implicit</c> / <c>op_Explicit</c>).
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_TypeWithConversionOperators_OperatorsPageUsesConversionSyntax()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.Writers.TryGetValue(
            "ApiMark.DotNet.Fixtures/OperatorsStruct/operators", out var writer));
        var h2Headings = writer!.Operations
            .OfType<HeadingOperation>()
            .Where(h => h.Level == 2)
            .Select(h => h.Text)
            .ToList();

        Assert.Contains(h2Headings, h => h.Contains("implicit operator"));
        Assert.Contains(h2Headings, h => h.Contains("explicit operator"));
        Assert.DoesNotContain(h2Headings, h => h.Contains("op_Implicit"));
        Assert.DoesNotContain(h2Headings, h => h.Contains("op_Explicit"));
    }

    /// <summary>
    ///     Validates that a public nested class inside an outer type receives a dedicated page
    ///     at <c>{NamespacePath}/{OuterTypeName}/{NestedTypeName}</c> in the writer factory.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_NestedClass_CreatesNestedClassPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the nested type page must exist at the hierarchical key
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/OuterClass/Inner"),
            "Expected a dedicated page for OuterClass.Inner at ApiMark.DotNet.Fixtures/OuterClass/Inner");
    }

    /// <summary>
    ///     Validates that the outer type page includes a "Nested Types" H2 section with a
    ///     table row for each visible nested type so readers can navigate to them.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_NestedClass_ListedOnOuterClassPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: outer type page must exist
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/OuterClass", out var outerWriter),
            "Expected a type page for OuterClass");
        var operations = outerWriter.Operations.ToList();

        // Assert: a "Nested Types" heading must be present
        var nestedIndex = operations.FindIndex(op => op is HeadingOperation h && h.Level == 2 && h.Text == "Nested Types");
        Assert.True(nestedIndex >= 0, "Expected 'Nested Types' H2 heading on OuterClass page");

        // Assert: the table immediately following must contain a row for Inner
        var nestedTable = operations.Skip(nestedIndex + 1).OfType<TableOperation>().First();
        Assert.Contains(nestedTable.Rows, row => row[0].Contains("Inner", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that the dedicated page for a public nested class contains the XML
    ///     summary authored on that nested type.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_NestedClass_PageContainsSummary()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the nested type page must exist
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/OuterClass/Inner", out var nestedWriter),
            "Expected a dedicated page for OuterClass.Inner");
        var paragraphs = nestedWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("A public nested class inside OuterClass", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a conversion operator whose return type is a nested type resolves its
    ///     XML documentation correctly, confirming that the <c>~ReturnType</c> suffix in the XML
    ///     doc member ID uses <c>.</c> as the separator (matching the XML doc format) rather than
    ///     the <c>/</c> separator used by Cecil's <c>FullName</c>.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_ConversionOperatorReturningNestedType_OperatorsPageContainsSummary()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the operators page for OperatorsStruct must exist
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/OperatorsStruct/operators", out var opsWriter),
            "Expected operators page for OperatorsStruct");
        var paragraphs = opsWriter.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("Wraps this instance as a Wrapped value", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that single-file output writes all API surface into a single <c>api</c>
    ///     writer with H1 assembly title, H2 namespace, H3 type, and H4 member headings,
    ///     no group headings (Constructors/Methods/Properties), and a compact bullet-list
    ///     paragraph summarizing each type's members.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_SingleFileOutput_WritesSingleApiMarkdown()
    {
        // Arrange: configure with SingleFile format so all output goes into one api.md file
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(
            factory,
            new EmitConfig { Format = OutputFormat.SingleFile },
            new InMemoryContext());

        // Assert: exactly one writer, keyed "api"
        Assert.Single(factory.Writers);
        Assert.True(factory.Writers.TryGetValue("api", out var writer), "Expected a single api writer for single-file output");
        var headings = writer.Operations.OfType<HeadingOperation>().ToList();

        // Assert: H1 is the assembly-level title ending with "API Reference"
        Assert.Contains(headings, h => h.Level == 1 && h.Text.EndsWith("API Reference", StringComparison.Ordinal));

        // Assert: H2 is the namespace heading
        Assert.Contains(
            headings,
            h => h.Level == 2 && h.Text.Contains("ApiMark.DotNet.Fixtures", StringComparison.Ordinal));

        // Assert: H3 is a type heading — SampleClass is always present in the fixture assembly
        Assert.Contains(headings, h => h.Level == 3 && h.Text == "SampleClass");

        // Assert: H4 is a member heading — constructor or method contains parentheses
        Assert.Contains(
            headings,
            h => h.Level == 4 && h.Text.Contains("(", StringComparison.Ordinal));

        // Assert: no group headings — single-file format emits members directly without section labels
        Assert.DoesNotContain(headings, h => h.Text == "Constructors");
        Assert.DoesNotContain(headings, h => h.Text == "Methods");
        Assert.DoesNotContain(headings, h => h.Text == "Properties");

        // Assert: at least one compact bullet-list paragraph summarizing members is emitted
        var paragraphs = writer.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p => p.Text.Contains("- **", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a method with an <c>&lt;example&gt;&lt;code&gt;</c> block emits
    ///     a <see cref="CodeBlockOperation"/> on its member detail page (gradual-disclosure).
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_MethodWithExample_EmitsCodeBlockOnMemberPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: GetGreeting has a parameter so it gets its own member page
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/ExampleDocClass/GetGreeting", out var writer),
            "Expected member page for ExampleDocClass.GetGreeting");
        var codeBlocks = writer.Operations.OfType<CodeBlockOperation>().ToList();

        // Assert: at least one csharp code block is emitted from the <example><code> content
        Assert.Contains(codeBlocks, cb => cb.Language == "csharp" && cb.Code.Contains("GetGreeting", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a method with an <c>&lt;example&gt;&lt;code&gt;</c> block emits
    ///     a <see cref="CodeBlockOperation"/> in the single-file output.
    /// </summary>
    [Fact]
    public void DotNetGenerator_SingleFile_MethodWithExample_EmitsCodeBlock()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());
        var config = new EmitConfig { Format = OutputFormat.SingleFile };

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, config, new InMemoryContext());

        // Assert: single-file writes exactly one "api" file
        Assert.True(factory.Writers.TryGetValue("api", out var writer), "Expected single-file api writer");
        var codeBlocks = writer.Operations.OfType<CodeBlockOperation>().ToList();

        // Assert: at least one csharp code block from the ExampleDocClass.GetGreeting example
        Assert.Contains(codeBlocks, cb => cb.Language == "csharp" && cb.Code.Contains("GetGreeting", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Regression test: methods whose parameter types include generic instantiations
    ///     (e.g. <c>IEnumerable&lt;string&gt;</c>, <c>IReadOnlyDictionary&lt;string,object&gt;</c>,
    ///     <c>Action&lt;string&gt;</c>) must resolve their XML documentation and render the
    ///     authored <c>&lt;summary&gt;</c> text on the member detail page rather than
    ///     <c>No description provided.</c>
    ///     Root cause: Cecil's <c>TypeReference.FullName</c> encodes generics as
    ///     <c>TypeName`N&lt;Arg1,Arg2&gt;</c> but XML doc IDs require <c>TypeName{Arg1,Arg2}</c>.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_MethodWithGenericParameterTypes_RendersXmlDocSummary()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: GenericParameterClass.Process has parameters with generic types → gets its own page
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/GenericParameterClass/Process", out var processWriter),
            "Expected member page for GenericParameterClass.Process");

        var processParagraphs = processWriter!.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(processParagraphs, p => p.Contains("Processes a sequence of names"));
        Assert.DoesNotContain(processParagraphs, p => p.Contains("No description provided"));

        // Assert: GenericParameterClass.Configure has an Action<string> parameter → gets its own page
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/GenericParameterClass/Configure", out var configureWriter),
            "Expected member page for GenericParameterClass.Configure");

        var configureParagraphs = configureWriter!.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(configureParagraphs, p => p.Contains("Applies an action to a configured value"));
        Assert.DoesNotContain(configureParagraphs, p => p.Contains("No description provided"));
    }

    /// <summary>
    ///     Validates that bare <c>&lt;inheritdoc /&gt;</c> on a method whose parameters are
    ///     generic types (<c>IEnumerable&lt;string&gt;</c>) resolves the inherited summary
    ///     from the interface. This exercises the intersection of inheritdoc chain resolution
    ///     and the generic-type XML doc ID encoding fix.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_BareInheritdoc_GenericParamMethod_SingleGeneric_ResolvesSummary()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: GenericParamImplementation.Process has a generic param → gets its own page
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/GenericParamImplementation/Process", out var writer),
            "Expected member page for GenericParamImplementation.Process");

        var paragraphs = writer!.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("Processes a sequence of items"));
        Assert.DoesNotContain(paragraphs, p => p.Contains("No description provided"));
    }

    /// <summary>
    ///     Validates that bare <c>&lt;inheritdoc /&gt;</c> on a method whose parameters are
    ///     multiple generic types (<c>IReadOnlyDictionary&lt;string,object&gt;</c> and
    ///     <c>Action&lt;string&gt;</c>) resolves the inherited summary from the interface.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_BareInheritdoc_GenericParamMethod_MultipleGenerics_ResolvesSummary()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: Transform has IReadOnlyDictionary<string,object> and Action<string> params → gets its own page
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/GenericParamImplementation/Transform", out var writer),
            "Expected member page for GenericParamImplementation.Transform");

        var paragraphs = writer!.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("Transforms data using the provided callback"));
        Assert.DoesNotContain(paragraphs, p => p.Contains("No description provided"));
    }

    /// <summary>
    ///     Validates that <c>&lt;inheritdoc cref="..." /&gt;</c> using the short C# alias form
    ///     (e.g. <c>IEnumerable{string}</c>) resolves the summary from the referenced method.
    ///     The C# compiler normalizes the alias to the fully-qualified CLR form in the
    ///     compiled XML, so both alias and fully-qualified styles must work identically.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_CrefInheritdoc_GenericParamMethod_ShortAliasForm_ResolvesSummary()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: RunProcessShortForm has an IEnumerable<string> param → gets its own page
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/CrefGenericDocClass/RunProcessShortForm", out var writer),
            "Expected member page for CrefGenericDocClass.RunProcessShortForm");

        var paragraphs = writer!.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("Processes a sequence of items"));
        Assert.DoesNotContain(paragraphs, p => p.Contains("No description provided"));
    }

    /// <summary>
    ///     Validates that <c>&lt;inheritdoc cref="..." /&gt;</c> using the fully-qualified CLR
    ///     form (e.g. <c>System.Collections.Generic.IEnumerable{System.String}</c>) resolves
    ///     the summary from the referenced method.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_CrefInheritdoc_GenericParamMethod_FullyQualifiedForm_ResolvesSummary()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: RunProcessFullyQualified uses fully-qualified cref → same resolution
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/CrefGenericDocClass/RunProcessFullyQualified", out var writer),
            "Expected member page for CrefGenericDocClass.RunProcessFullyQualified");

        var paragraphs = writer!.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("Processes a sequence of items"));
        Assert.DoesNotContain(paragraphs, p => p.Contains("No description provided"));
    }

    /// <summary>
    ///     Validates that <c>&lt;inheritdoc cref="..." /&gt;</c> using the short alias form
    ///     resolves the summary for a method with multiple generic parameter types.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_CrefInheritdoc_MultipleGenericParams_ShortAliasForm_ResolvesSummary()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: RunTransformShortForm inherits from Transform via short-form cref
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/CrefGenericDocClass/RunTransformShortForm", out var writer),
            "Expected member page for CrefGenericDocClass.RunTransformShortForm");

        var paragraphs = writer!.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("Transforms data using the provided callback"));
        Assert.DoesNotContain(paragraphs, p => p.Contains("No description provided"));
    }

    /// <summary>
    ///     Validates that <c>&lt;inheritdoc cref="..." /&gt;</c> using the fully-qualified CLR
    ///     form resolves the summary for a method with multiple generic parameter types.
    /// </summary>
    [Fact]
    public void DotNetGenerator_Generate_CrefInheritdoc_MultipleGenericParams_FullyQualifiedForm_ResolvesSummary()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: RunTransformFullyQualified inherits from Transform via fully-qualified cref
        Assert.True(
            factory.Writers.TryGetValue("ApiMark.DotNet.Fixtures/CrefGenericDocClass/RunTransformFullyQualified", out var writer),
            "Expected member page for CrefGenericDocClass.RunTransformFullyQualified");

        var paragraphs = writer!.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("Transforms data using the provided callback"));
        Assert.DoesNotContain(paragraphs, p => p.Contains("No description provided"));
    }

    /// <summary>Validates that constructing <see cref="DotNetGenerator"/> with a null options argument throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void DotNetGenerator_Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new DotNetGenerator(null!));
    }

    /// <summary>Validates that <see cref="DotNetGenerator.Parse"/> throws <see cref="FileNotFoundException"/> when the assembly path does not exist.</summary>
    [Fact]
    public void DotNetGenerator_Parse_MissingAssemblyPath_ThrowsFileNotFoundException()
    {
        // Arrange
        var options = new DotNetGeneratorOptions
        {
            AssemblyPath = "/nonexistent/path/assembly.dll",
            XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
        };
        var generator = new DotNetGenerator(options);

        // Act / Assert
        Assert.Throws<FileNotFoundException>(() => generator.Parse(new InMemoryContext()));
    }

    /// <summary>Validates that an internal static NamespaceDoc class is excluded from the generated type listing.</summary>
    [Fact]
    public void DotNetGenerator_Generate_NamespaceDocClass_ExcludedFromTypeListing()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the namespace type table must not contain a row for NamespaceDoc
        var nsWriter = factory.Writers["ApiMark.DotNet.Fixtures"];
        var typeTable = nsWriter.Operations.OfType<TableOperation>().FirstOrDefault(t => t.Headers[0] == "Type");
        if (typeTable != null)
        {
            Assert.DoesNotContain(
                typeTable.Rows,
                row => row[0].Contains("NamespaceDoc", StringComparison.Ordinal));
        }
    }

    /// <summary>Validates that <see cref="DotNetGenerator.Parse"/> throws <see cref="ArgumentNullException"/> when context is null.</summary>
    [Fact]
    public void DotNetGenerator_Parse_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var generator = new DotNetGenerator(BuildOptions());

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => generator.Parse(null!));
    }
}
