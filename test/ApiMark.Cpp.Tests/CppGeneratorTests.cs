using ApiMark.Core.TestHelpers;
using ApiMark.Cpp;
using Xunit;

namespace ApiMark.Cpp.Tests;

/// <summary>Integration tests for <see cref="CppGenerator"/>.</summary>
public class CppGeneratorTests
{
    /// <summary>
    ///     Builds a <see cref="CppGeneratorOptions"/> pointing at the fixture include directory
    ///     with the specified visibility and deprecated settings.
    /// </summary>
    /// <param name="visibility">Which members to include in generated output.</param>
    /// <param name="includeDeprecated">Whether to include deprecated declarations.</param>
    /// <returns>A fully configured <see cref="CppGeneratorOptions"/>.</returns>
    private static CppGeneratorOptions BuildOptions(
        ApiVisibility visibility = ApiVisibility.Public,
        bool includeDeprecated = false)
    {
        return new CppGeneratorOptions
        {
            LibraryName = "Fixtures",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
            Visibility = visibility,
            IncludeDeprecated = includeDeprecated,
        };
    }

    /// <summary>Validates that passing null options to the constructor throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void CppGenerator_Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Act / Assert: null options must be rejected immediately at construction time
        Assert.Throws<ArgumentNullException>(() => new CppGenerator(null!));
    }

    /// <summary>
    ///     Validates that passing options with an empty <see cref="CppGeneratorOptions.LibraryName"/>
    ///     to the constructor throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void CppGenerator_Constructor_EmptyLibraryName_ThrowsArgumentException()
    {
        // Arrange: options with an empty library name — required to be non-empty
        var options = new CppGeneratorOptions
        {
            LibraryName = "",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
        };

        // Act / Assert: empty LibraryName must be rejected at construction time
        Assert.Throws<ArgumentException>(() => new CppGenerator(options));
    }

    /// <summary>
    ///     Validates that passing options with empty <see cref="CppGeneratorOptions.PublicIncludeRoots"/>
    ///     to the constructor throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void CppGenerator_Constructor_EmptyPublicIncludeRoots_ThrowsArgumentException()
    {
        // Arrange: options with no include roots — at least one is required
        var options = new CppGeneratorOptions
        {
            LibraryName = "TestLibrary",
            PublicIncludeRoots = [],
        };

        // Act / Assert: empty PublicIncludeRoots must be rejected at construction time
        Assert.Throws<ArgumentException>(() => new CppGenerator(options));
    }

    /// <summary>Validates that passing a null factory to <see cref="CppGenerator.Generate"/> throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void CppGenerator_Generate_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var generator = new CppGenerator(BuildOptions());

        // Act / Assert: null factory must be rejected before any I/O is attempted
        Assert.Throws<ArgumentNullException>(() => generator.Generate(null!));
    }

    /// <summary>Validates that a nonexistent public include root throws <see cref="DirectoryNotFoundException"/>.</summary>
    [Fact]
    public void CppGenerator_Generate_NonexistentIncludeRoot_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var options = new CppGeneratorOptions
        {
            LibraryName = "Fixtures",
            PublicIncludeRoots = ["/nonexistent/include/path"],
        };
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(options);

        // Act / Assert: a missing root must fail early rather than silently producing empty output
        Assert.Throws<DirectoryNotFoundException>(() => generator.Generate(factory));
    }

    /// <summary>Validates that generating from valid headers creates the <c>api</c> entrypoint page.</summary>
    [Fact]
    public void CppGenerator_Generate_ValidHeaders_CreatesApiEntrypoint()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert
        Assert.True(factory.Writers.ContainsKey("api"), "Expected api.md to be created");
    }

    /// <summary>Validates that generating from valid headers creates a namespace summary page for the fixtures namespace.</summary>
    [Fact]
    public void CppGenerator_Generate_ValidHeaders_CreatesNamespacePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the fixtures namespace maps to key "fixtures" at the root level
        Assert.True(
            factory.Writers.ContainsKey("fixtures"),
            "Expected namespace summary page keyed 'fixtures'");
    }

    /// <summary>Validates that <c>api.md</c> lists namespaces with a declaration count column.</summary>
    [Fact]
    public void CppGenerator_Generate_ApiMd_ListsNamespacesWithTypeCount()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: namespace table is first in api.md and contains the fixtures namespace
        var apiWriter = factory.Writers["api"];
        var tables = apiWriter.Operations.OfType<TableOperation>().ToList();
        var nsTable = tables.First();
        Assert.Contains(nsTable.Rows, row => row[0].Contains("fixtures"));

        // Assert: each row has three columns — Namespace, Declarations, Description
        Assert.All(nsTable.Rows, row => Assert.Equal(3, row.Length));
    }

    /// <summary>Validates that generating from valid headers creates a type page for <c>SampleClass</c>.</summary>
    [Fact]
    public void CppGenerator_Generate_ValidHeaders_CreatesTypePageForSampleClass()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: type page key is "{namespace}/{typeName}"
        Assert.True(
            factory.Writers.ContainsKey("fixtures/SampleClass"),
            "Expected type page for SampleClass at 'fixtures/SampleClass'");
    }

    /// <summary>Validates that a deprecated class is excluded when <see cref="CppGeneratorOptions.IncludeDeprecated"/> is false.</summary>
    [Fact]
    public void CppGenerator_Generate_IncludeDeprecatedFalse_ExcludesDeprecatedClass()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions(includeDeprecated: false));

        // Act
        generator.Generate(factory);

        // Assert
        Assert.False(
            factory.Writers.ContainsKey("fixtures/DeprecatedClass"),
            "DeprecatedClass should be excluded when IncludeDeprecated=false");
    }

    /// <summary>Validates that a deprecated class is included when <see cref="CppGeneratorOptions.IncludeDeprecated"/> is true.</summary>
    [Fact]
    public void CppGenerator_Generate_IncludeDeprecatedTrue_IncludesDeprecatedClass()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions(includeDeprecated: true));

        // Act
        generator.Generate(factory);

        // Assert
        Assert.True(
            factory.Writers.ContainsKey("fixtures/DeprecatedClass"),
            "DeprecatedClass should be included when IncludeDeprecated=true");
    }

    /// <summary>Validates that protected members are excluded under <see cref="ApiVisibility.Public"/>.</summary>
    [Fact]
    public void CppGenerator_Generate_PublicVisibility_ExcludesProtectedMethod()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions(ApiVisibility.Public));

        // Act
        generator.Generate(factory);

        // Assert: the type page itself must exist (the class is public)
        Assert.True(factory.Writers.ContainsKey("fixtures/ProtectedMembersClass"));

        // Assert: the protected method must not receive its own page under Public visibility
        Assert.False(
            factory.Writers.ContainsKey("fixtures/ProtectedMembersClass/ProtectedMethod"),
            "Protected method should not appear with Public visibility");
    }

    /// <summary>Validates that protected members are included under <see cref="ApiVisibility.PublicAndProtected"/>.</summary>
    [Fact]
    public void CppGenerator_Generate_PublicAndProtectedVisibility_IncludesProtectedMethod()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions(ApiVisibility.PublicAndProtected));

        // Act
        generator.Generate(factory);

        // Assert: ProtectedMethod has a parameter, so it gets its own page
        Assert.True(
            factory.Writers.ContainsKey("fixtures/ProtectedMembersClass/ProtectedMethod"),
            "Protected method should appear with PublicAndProtected visibility");
    }

    /// <summary>Validates that private members are included under <see cref="ApiVisibility.All"/>.</summary>
    [Fact]
    public void CppGenerator_Generate_AllVisibility_IncludesPrivateMethod()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions(ApiVisibility.All));

        // Act
        generator.Generate(factory);

        // Assert: PrivateMethod has a parameter so it gets its own page under All visibility
        Assert.True(
            factory.Writers.ContainsKey("fixtures/ProtectedMembersClass/PrivateMethod"),
            "Private method should appear with ApiVisibility.All");
    }

    /// <summary>Validates that a method with parameters receives its own member page.</summary>
    [Fact]
    public void CppGenerator_Generate_MethodWithParameters_CreatesMemberPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: GetGreeting has a parameter so it gets its own page
        Assert.True(
            factory.Writers.ContainsKey("fixtures/SampleClass/GetGreeting"),
            "Expected member page for SampleClass::GetGreeting");
    }

    /// <summary>
    ///     Validates that the type page for <c>SampleClass</c> lists the parameter type in
    ///     the <c>GetGreeting</c> method row link text so readers can distinguish overloads.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_SampleClass_TypePage_MethodRowShowsParameterType()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the SampleClass type page must include a method row whose link cell
        // starts with "[GetGreeting(" followed by a non-empty parameter type, confirming
        // the parentheses are not empty as they were before the fix
        var writer = factory.Writers["fixtures/SampleClass"];
        var tables = writer.Operations.OfType<TableOperation>().ToList();
        var allCells = tables.SelectMany(t => t.Rows).SelectMany(r => r).ToList();
        Assert.Contains(
            allCells,
            c => c.StartsWith("[GetGreeting(", StringComparison.Ordinal)
                 && !c.StartsWith("[GetGreeting()", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that an individual method member page uses an H3 heading, consistent
    ///     with the DotNet generator and the combined member page convention.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_MemberPage_UsesH3Heading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the GetGreeting member page must open with an H3 heading containing the
        // class and method name so the heading level matches combined member pages
        var writer = factory.Writers["fixtures/SampleClass/GetGreeting"];
        var firstHeading = writer.Operations.OfType<HeadingOperation>().First();
        Assert.Equal(3, firstHeading.Level);
        Assert.Contains("GetGreeting", firstHeading.Text, StringComparison.Ordinal);
    }

    /// <summary>Validates that parameterless methods and free functions all receive separate pages.</summary>
    [Fact]
    public void CppGenerator_AllMembers_GetSeparateFiles()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: parameterless method in SampleClass gets own page
        Assert.True(
            factory.Writers.ContainsKey("fixtures/SampleClass/Reset"),
            "Expected member page for SampleClass::Reset");

        // Assert: parameterless method in RemarksClass gets own page
        Assert.True(
            factory.Writers.ContainsKey("fixtures/RemarksClass/Compute"),
            "Expected member page for RemarksClass::Compute");

        // Assert: free function in fixtures namespace gets own page
        Assert.True(
            factory.Writers.ContainsKey("fixtures/Add"),
            "Expected member page for free function fixtures::Add");
    }

    /// <summary>Validates that generated output files follow the established naming convention.</summary>
    [Fact]
    public void CppGenerator_OutputFiles_FollowNamingConvention()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: root entrypoint is "api"
        Assert.True(factory.Writers.ContainsKey("api"));

        // Assert: namespace file key is "{namespace}" (root level, not in a subfolder)
        Assert.True(factory.Writers.ContainsKey("fixtures"));

        // Assert: type file key is "{namespace}/{typeName}"
        Assert.True(factory.Writers.ContainsKey("fixtures/SampleClass"));

        // Assert: member file key is "{namespace}/{typeName}/{memberName}"
        Assert.True(factory.Writers.ContainsKey("fixtures/SampleClass/Reset"));
    }

    /// <summary>Validates that a type with a doc comment writes the summary text as a paragraph.</summary>
    [Fact]
    public void CppGenerator_Generate_TypeWithDocComment_WritesSummaryToParagraph()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the SampleClass page must contain the key summary words from the @brief tag
        var writer = factory.Writers["fixtures/SampleClass"];
        var paragraphs = writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("sample class", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Validates that a method with a doc comment writes the summary text as a paragraph.</summary>
    [Fact]
    public void CppGenerator_Generate_MethodWithDocComment_WritesSummaryToParagraph()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the GetGreeting page must contain the key summary word from the @brief tag
        var writer = factory.Writers["fixtures/SampleClass/GetGreeting"];
        var paragraphs = writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(paragraphs, p => p.Contains("greeting", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Validates that a member with no doc comment writes the no-description placeholder.</summary>
    [Fact]
    public void CppGenerator_Generate_MissingDocComment_WritesPlaceholder()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: Refresh() has no Doxygen comment — the generator must emit the placeholder
        var writer = factory.Writers["fixtures/SampleClass/Refresh"];
        var paragraphs = writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains("*No description provided.*", paragraphs);
    }

    /// <summary>Validates that free functions receive their own top-level pages under the namespace key.</summary>
    [Fact]
    public void CppGenerator_Generate_FreeFunctions_GetOwnPages()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: free functions in the fixtures namespace get pages at "{namespace}/{functionName}"
        Assert.True(
            factory.Writers.ContainsKey("fixtures/Add"),
            "Expected page for free function fixtures::Add at 'fixtures/Add'");
    }

    /// <summary>Validates that generating from valid fixture headers creates a page for <c>SampleStatus</c> enum.</summary>
    [Fact]
    public void CppGenerator_Generate_ValidHeaders_CreatesEnumPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: enum page key follows the same "{namespace}/{typeName}" pattern as classes
        Assert.True(
            factory.Writers.ContainsKey("fixtures/SampleStatus"),
            "Expected enum page for SampleStatus at 'fixtures/SampleStatus'");
    }

    /// <summary>Validates that the <c>SampleStatus</c> enum page contains all declared enum value names.</summary>
    [Fact]
    public void CppGenerator_Generate_EnumPage_ContainsValues()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the enum values table must contain every declared SampleStatus value
        var writer = factory.Writers["fixtures/SampleStatus"];
        var tables = writer.Operations.OfType<TableOperation>().ToList();
        var allCells = tables.SelectMany(t => t.Rows).SelectMany(r => r).ToList();
        Assert.Contains(allCells, c => c.Contains("Active", StringComparison.Ordinal));
        Assert.Contains(allCells, c => c.Contains("Pending", StringComparison.Ordinal));
        Assert.Contains(allCells, c => c.Contains("Failed", StringComparison.Ordinal));
    }

    /// <summary>Validates that the template class <c>Stack</c> receives its own type page.</summary>
    [Fact]
    public void CppGenerator_Generate_TemplateClass_CreatesTypePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: Stack is a template class and must receive its own documentation page
        Assert.True(
            factory.Writers.ContainsKey("fixtures/Stack"),
            "Expected type page for template class Stack at 'fixtures/Stack'");
    }

    /// <summary>Validates that the <c>Circle</c> inheritance class receives its own type page.</summary>
    [Fact]
    public void CppGenerator_Generate_InheritanceClass_CreatesTypePage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: Circle inherits from Shape and must receive its own documentation page
        Assert.True(
            factory.Writers.ContainsKey("fixtures/Circle"),
            "Expected type page for Circle at 'fixtures/Circle'");
    }

    /// <summary>Validates that the <c>Circle</c> constructor receives its own member detail page.</summary>
    [Fact]
    public void CppGenerator_Generate_Constructor_CreatesConstructorPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: Circle has an explicit constructor that must receive its own page
        Assert.True(
            factory.Writers.ContainsKey("fixtures/Circle/Circle"),
            "Expected constructor page for Circle::Circle at 'fixtures/Circle/Circle'");
    }

    /// <summary>Validates that a type page contains the fully-qualified C++ name in its signature block.</summary>
    [Fact]
    public void CppGenerator_Generate_TypePage_ContainsQualifiedName()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the SampleClass type page must include "fixtures::SampleClass" in its signature
        // comment so an AI reader knows the exact qualified name to use in code
        var writer = factory.Writers["fixtures/SampleClass"];
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.Contains(
            signatures,
            s => s.Contains("fixtures::SampleClass", StringComparison.Ordinal));
    }

    /// <summary>Validates that a member page contains the fully-qualified C++ name in its signature block.</summary>
    [Fact]
    public void CppGenerator_Generate_MemberPage_ContainsQualifiedName()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the GetGreeting member page must include the fully-qualified name so that
        // an AI reader can call "fixtures::SampleClass::GetGreeting" without guessing the namespace
        var writer = factory.Writers["fixtures/SampleClass/GetGreeting"];
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.Contains(
            signatures,
            s => s.Contains("fixtures::SampleClass::GetGreeting", StringComparison.Ordinal));
    }

    /// <summary>Validates that the variadic free function <c>Format</c> receives its own page.</summary>
    [Fact]
    public void CppGenerator_Generate_VariadicFunction_CreatesPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: Format is a variadic free function and must receive its own documentation page
        Assert.True(
            factory.Writers.ContainsKey("fixtures/Format"),
            "Expected page for variadic free function fixtures::Format at 'fixtures/Format'");
    }

    /// <summary>
    ///     Validates that two members whose names differ only in case are combined onto a
    ///     single page named after the lowercase key.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_CaseCollisionClass_CreatesCombinedPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the combined page is written at the lowercase key path
        Assert.True(
            factory.Writers.ContainsKey("fixtures/CaseCollisionClass/name"),
            "Expected a combined page at the lowercase key 'name' for the colliding members");
    }

    /// <summary>
    ///     Validates that the generator does not create a separate page for the upper-case
    ///     member when a case-insensitive collision exists.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_CaseCollisionClass_DoesNotCreateSeparateCasedPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: no page should be created using the exact-case name "Name" when a collision
        // exists — the combined lowercase page replaces all individual pages for the group
        Assert.False(
            factory.Writers.ContainsKey("fixtures/CaseCollisionClass/Name"),
            "Expected no separate page for 'Name' when a case-insensitive collision exists");
    }

    /// <summary>
    ///     Validates that the combined collision page contains H4 headings for both the
    ///     method <c>Name()</c> and the field <c>name</c>.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_CaseCollisionClass_CombinedPageContainsBothMembers()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: combined page exists
        Assert.True(factory.Writers.ContainsKey("fixtures/CaseCollisionClass/name"));
        var writer = factory.Writers["fixtures/CaseCollisionClass/name"];

        // Assert: both members appear as distinct H4 headings on the combined page
        var level4Headings = writer.Operations
            .OfType<HeadingOperation>()
            .Where(h => h.Level == 4)
            .Select(h => h.Text)
            .ToList();
        Assert.Contains(level4Headings, h => h.StartsWith("Name", StringComparison.Ordinal));
        Assert.Contains(level4Headings, h => h.StartsWith("name", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a method with a <c>@details</c> Doxygen block emits the extended
    ///     details text as a second paragraph after the brief summary paragraph.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_MethodWithDetails_WritesDetailsParagraph()
    {
        // Arrange: RemarksClass::Compute carries a @details block in its Doxygen comment
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the Compute member page must contain a paragraph with the @details text
        var writer = factory.Writers["fixtures/RemarksClass/Compute"];
        var paragraphs = writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(
            paragraphs,
            p => p.Contains("iterative algorithm", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Validates that the namespace description in <c>api.md</c> falls back to the
    ///     no-description placeholder when no namespace-level doc comment is present, rather
    ///     than incorrectly using a class summary.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_NamespaceWithoutDocComment_UsesPlaceholderDescription()
    {
        // Arrange: no fixture namespace carries a namespace-level Doxygen comment
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the namespace table in api.md must list the placeholder for the fixtures namespace,
        // confirming the description is taken from the namespace object (none present) and not
        // misattributed from a class summary
        var apiWriter = factory.Writers["api"];
        var nsTable = apiWriter.Operations.OfType<TableOperation>().First();
        var fixturesRow = nsTable.Rows.First(r => r[0].Contains("fixtures", StringComparison.Ordinal));
        Assert.Equal("*No description provided.*", fixturesRow[2]);
    }

    /// <summary>
    ///     Validates that an enum page contains the <c>#include</c> directive for the header
    ///     that declares the enum.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_EnumPage_ContainsIncludeDirective()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the SampleStatus enum page must contain an #include directive in its signature
        // block so readers can copy-paste the include path without consulting the header tree
        var writer = factory.Writers["fixtures/SampleStatus"];
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.Contains(
            signatures,
            s => s.Contains("#include", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a free-function page contains the <c>#include</c> directive for the
    ///     header that declares the function.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_FreeFunctionPage_ContainsIncludeDirective()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the Add free-function page must contain an #include directive in its signature
        // block so readers can include the correct header without browsing the source tree
        var writer = factory.Writers["fixtures/Add"];
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.Contains(
            signatures,
            s => s.Contains("#include", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that <see cref="CppGeneratorOptions.IncludePatterns"/> restricts header
    ///     enumeration to files matching at least one pattern.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_IncludePatterns_OnlyIncludesMatchingFiles()
    {
        // Arrange: include only SampleClass.h to isolate a single type
        var options = new CppGeneratorOptions
        {
            LibraryName = "Fixtures",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
            IncludePatterns = ["**/SampleClass.h"],
        };
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(options);

        // Act
        generator.Generate(factory);

        // Assert: SampleClass page must exist because its header matched the include pattern
        Assert.True(
            factory.Writers.ContainsKey("fixtures/SampleClass"),
            "Expected SampleClass page when included by pattern");

        // Assert: SampleStatus page must not exist because SampleEnum.h was not included
        Assert.False(
            factory.Writers.ContainsKey("fixtures/SampleStatus"),
            "Expected SampleStatus to be absent when not matched by include pattern");
    }

    /// <summary>
    ///     Validates that <see cref="CppGeneratorOptions.ExcludePatterns"/> removes specific
    ///     files from the header enumeration while leaving all other headers intact.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_ExcludePatterns_ExcludesMatchingFiles()
    {
        // Arrange: exclude SampleClass.h to verify its type disappears from output
        var options = new CppGeneratorOptions
        {
            LibraryName = "Fixtures",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
            ExcludePatterns = ["**/SampleClass.h"],
        };
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(options);

        // Act
        generator.Generate(factory);

        // Assert: SampleClass page must not exist because its header was excluded
        Assert.False(
            factory.Writers.ContainsKey("fixtures/SampleClass"),
            "Expected SampleClass to be absent when its header is excluded");

        // Assert: SampleStatus page must still exist because SampleEnum.h was not excluded
        Assert.True(
            factory.Writers.ContainsKey("fixtures/SampleStatus"),
            "Expected SampleStatus to be present when only SampleClass.h is excluded");
    }

    /// <summary>
    ///     Validates that the type page for a <c>final</c> class contains the <c>final</c>
    ///     keyword in its signature block so that AI readers immediately know the class
    ///     cannot be used as a base class.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_FinalClass_EmitsFinalKeywordInSignature()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the FinalClass type page must exist
        Assert.True(factory.Writers.ContainsKey("fixtures/FinalClass"), "Expected type page for FinalClass");

        // Assert: the FinalClass type page signature must contain "final" so that
        // readers know the class cannot be subclassed without opening the header
        var writer = factory.Writers["fixtures/FinalClass"];
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.Contains(
            signatures,
            s => s.Contains("final", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that the type page for a non-<c>final</c> class does not emit the
    ///     <c>final</c> keyword in its signature block, confirming that the <c>final</c>
    ///     annotation is only applied when the class is explicitly declared <c>final</c>.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_NonFinalClass_DoesNotEmitFinalKeyword()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Generate(factory);

        // Assert: the SampleClass type page must exist and must not contain "final"
        // because SampleClass is not declared final
        Assert.True(factory.Writers.ContainsKey("fixtures/SampleClass"), "Expected type page for SampleClass");

        var writer = factory.Writers["fixtures/SampleClass"];
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.DoesNotContain(
            signatures,
            s => s.Contains("final", StringComparison.Ordinal));
    }
}
