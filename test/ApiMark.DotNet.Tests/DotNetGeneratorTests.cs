using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Integration tests for <see cref="DotNetGenerator"/>.</summary>
public class DotNetGeneratorTests
{
    /// <summary>
    ///     Builds a <see cref="DotNetGeneratorOptions"/> pointing at the fixture assembly
    ///     with the specified visibility and obsolete settings.
    /// </summary>
    /// <param name="visibility">Which members to include in generated output.</param>
    /// <param name="includeObsolete">Whether to include obsolete members.</param>
    /// <returns>A fully configured <see cref="DotNetGeneratorOptions"/>.</returns>
    private static DotNetGeneratorOptions BuildOptions(
        ApiVisibility visibility = ApiVisibility.Public,
        bool includeObsolete = false)
    {
        return new DotNetGeneratorOptions
        {
            AssemblyPath = FixturePaths.GetFixtureDll(),
            XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
            Visibility = visibility,
            IncludeObsolete = includeObsolete,
        };
    }

    /// <summary>Validates that <see cref="DotNetGenerator.Generate"/> throws <see cref="FileNotFoundException"/> when the XML doc file is missing.</summary>
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
        Assert.Throws<FileNotFoundException>(() => generator.Generate(factory));
    }

    /// <summary>Validates that a valid assembly produces an <c>api</c> entrypoint Markdown page.</summary>
    [Fact]
    public void DotNetGenerator_Generate_ValidAssembly_CreatesApiMarkdownPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

        // Assert
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ObsoleteClass"),
            "ObsoleteClass should be included when IncludeObsolete=true");
    }

    /// <summary>Validates that protected members are excluded when <see cref="ApiVisibility.Public"/> is selected.</summary>
    [Fact]
    public void DotNetGenerator_Generate_PublicVisibility_ExcludesProtectedMethod()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions(ApiVisibility.Public));

        // Act
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        var exception = Record.Exception(() => generator.Generate(factory));
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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        new DotNetGenerator(BuildOptions(ApiVisibility.Public)).Generate(publicFactory);

        // Arrange: PublicAndProtected visibility — protected method should be present
        var protectedFactory = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(BuildOptions(ApiVisibility.PublicAndProtected)).Generate(protectedFactory);

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
        new DotNetGenerator(BuildOptions(includeObsolete: false)).Generate(withoutObsolete);

        // Arrange: IncludeObsolete=true
        var withObsolete = new InMemoryMarkdownWriterFactory();
        new DotNetGenerator(BuildOptions(includeObsolete: true)).Generate(withObsolete);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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

    /// <summary>Validates that <c>api.md</c> lists only root namespaces, not child namespaces.</summary>
    [Fact]
    public void DotNetGenerator_Generate_ApiMd_ListsOnlyRootNamespaces()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: api.md namespace table contains the root namespace; after FIX 6 the namespace
        // table is first in api.md (convention appendix moved to the end)
        var apiWriter = factory.Writers["api"];
        var tables = apiWriter.Operations.OfType<TableOperation>().ToList();
        var nsTable = tables.First(); // first table is the namespace listing
        Assert.Contains(nsTable.Rows, row => row[0].Contains("ApiMark.DotNet.Fixtures"));

        // Assert: the child namespace does NOT appear in api.md's namespace table
        Assert.DoesNotContain(nsTable.Rows, row => row[0].Contains("ApiMark.DotNet.Fixtures.Inner"));
    }

    /// <summary>Validates that the root namespace page lists its immediate child namespace.</summary>
    [Fact]
    public void DotNetGenerator_Generate_RootNamespacePage_ListsChildNamespace()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the root namespace page exists at root level
        Assert.True(factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures"),
            "Root namespace page must be at root level");

        // Assert: the root namespace page has a table that references the child namespace
        var rootNsWriter = factory.Writers["ApiMark.DotNet.Fixtures"];
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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

        // Assert: type page exists
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/SealedClass"),
            "Expected a type page for SealedClass");

        // Assert: signature contains the sealed modifier
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SealedClass"];
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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
                   row[0].Contains("(", StringComparison.Ordinal));
    }

    /// <summary>Validates that the SampleClass type page has a Constructors sub-section whose table headers are <c>Member</c> and <c>Description</c> (no Returns column).</summary>
    [Fact]
    public void DotNetGenerator_TypePage_SampleClass_HasConstructorsSectionWithCorrectHeaders()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: Constructors heading is present
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SampleClass"];
        var operations = writer.Operations.ToList();
        var ctorIndex = operations.FindIndex(op => op is HeadingOperation h && h.Text == "Constructors");
        Assert.True(ctorIndex >= 0, "Expected 'Constructors' heading on SampleClass page");

        // Assert: the table immediately following uses Member + Description (constructors have no Returns column)
        var ctorTable = operations.Skip(ctorIndex + 1).OfType<TableOperation>().First();
        Assert.Equal(new[] { "Member", "Description" }, ctorTable.Headers);
    }

    /// <summary>Validates that the SampleClass type page has a Properties sub-section whose table headers are <c>Member</c>, <c>Type</c>, and <c>Description</c>.</summary>
    [Fact]
    public void DotNetGenerator_TypePage_SampleClass_HasPropertiesSectionWithCorrectHeaders()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: Properties heading is present
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SampleClass"];
        var operations = writer.Operations.ToList();
        var propIndex = operations.FindIndex(op => op is HeadingOperation h && h.Text == "Properties");
        Assert.True(propIndex >= 0, "Expected 'Properties' heading on SampleClass page");

        // Assert: the table immediately following uses Member + Type + Description
        var propTable = operations.Skip(propIndex + 1).OfType<TableOperation>().First();
        Assert.Equal(new[] { "Member", "Type", "Description" }, propTable.Headers);
    }

    /// <summary>Validates that the SampleClass type page has a Methods sub-section whose table uses <c>Returns</c> (not <c>Type</c>) as the second header.</summary>
    [Fact]
    public void DotNetGenerator_TypePage_SampleClass_HasMethodsSectionWithReturnsHeader()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: Methods heading is present
        var writer = factory.Writers["ApiMark.DotNet.Fixtures/SampleClass"];
        var operations = writer.Operations.ToList();
        var methodsIndex = operations.FindIndex(op => op is HeadingOperation h && h.Text == "Methods");
        Assert.True(methodsIndex >= 0, "Expected 'Methods' heading on SampleClass page");

        // Assert: the table immediately following uses Member + Returns + Description ("Returns" not "Type")
        var methodsTable = operations.Skip(methodsIndex + 1).OfType<TableOperation>().First();
        Assert.Equal(new[] { "Member", "Returns", "Description" }, methodsTable.Headers);
    }

    /// <summary>Validates that no cell in the Constructors sub-table equals <c>"void"</c>.</summary>
    [Fact]
    public void DotNetGenerator_ConstructorsTable_SampleClass_ContainsNoVoidCells()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
        generator.Generate(factory);

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
}
