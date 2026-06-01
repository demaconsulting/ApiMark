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

        // Assert
        Assert.True(
            factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ApiMark.DotNet.Fixtures"),
            "Expected namespace page for ApiMark.DotNet.Fixtures");
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

        // Assert: namespace file key is "{namespace}/{namespace}"
        Assert.True(factory.Writers.ContainsKey("ApiMark.DotNet.Fixtures/ApiMark.DotNet.Fixtures"));

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
}
