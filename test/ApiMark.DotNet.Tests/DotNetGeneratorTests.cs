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

    /// <summary>Validates that complex members (parameters, exceptions, multi-line remarks) each receive separate Markdown files.</summary>
    [Fact]
    public void DotNetGenerator_ComplexityRule_ComplexMembers_GetSeparateFiles()
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

    /// <summary>Validates that overloaded complex methods produce separate Markdown files.</summary>
    [Fact]
    public void DotNetGenerator_Generate_OverloadedMethods_CreateDistinctMemberPages()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert
        Assert.Equal(
            2,
            factory.Writers.Keys.Count(k =>
                k.StartsWith("ApiMark.DotNet.Fixtures/SampleStatusExtensions/IsPassed", StringComparison.Ordinal)));
    }

    /// <summary>Validates that overloads differing only in <c>int</c> vs <c>int[]</c> produce separate, distinct Markdown files.</summary>
    [Fact]
    public void DotNetGenerator_Generate_IntVsIntArray_CreateDistinctMemberPages()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new DotNetGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: both overloads generate pages
        Assert.Equal(
            2,
            factory.Writers.Keys.Count(k =>
                k.StartsWith("ApiMark.DotNet.Fixtures/IntVsIntArrayClass/Process", StringComparison.Ordinal)));

        // Assert: int and int[] overloads have distinct page keys
        var intPage = factory.Writers.Keys.FirstOrDefault(k =>
            k.StartsWith("ApiMark.DotNet.Fixtures/IntVsIntArrayClass/Process", StringComparison.Ordinal) &&
            k.Contains("Int32Array", StringComparison.Ordinal));
        var scalarPage = factory.Writers.Keys.FirstOrDefault(k =>
            k.StartsWith("ApiMark.DotNet.Fixtures/IntVsIntArrayClass/Process", StringComparison.Ordinal) &&
            !k.Contains("Int32Array", StringComparison.Ordinal));

        Assert.NotNull(intPage);    // int[] overload gets an "Array" token in its file name
        Assert.NotNull(scalarPage); // int overload does not contain "Array"
        Assert.NotEqual(intPage, scalarPage);
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

        // Assert: a convention table is present in api.md
        var tables = apiWriter.Operations.OfType<TableOperation>().ToList();
        Assert.True(tables.Count >= 2, "Expected at least a convention table and a namespace table in api.md");
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

        // Assert: api.md namespace table contains the root namespace
        var apiWriter = factory.Writers["api"];
        var tables = apiWriter.Operations.OfType<TableOperation>().ToList();
        var nsTable = tables.Last(); // last table is the namespace listing
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
}
