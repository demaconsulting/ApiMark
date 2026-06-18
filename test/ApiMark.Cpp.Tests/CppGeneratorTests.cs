// cspell:ignore deletedmembersclass
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.Cpp;
using ApiMark.Cpp.CppAst;
using Xunit;

namespace ApiMark.Cpp.Tests;

/// <summary>
///     Integration tests for <see cref="CppGenerator"/> using a shared <see cref="CppGeneratorFixture"/>
///     to avoid invoking clang more than 4 times per test run.
/// </summary>
public class CppGeneratorTests : IClassFixture<CppGeneratorFixture>
{
    /// <summary>The shared fixture providing pre-generated factories for common option configurations.</summary>
    private readonly CppGeneratorFixture _fixture;

    /// <summary>
    ///     Initializes the test class with the shared <see cref="CppGeneratorFixture"/>.
    /// </summary>
    /// <param name="fixture">
    ///     The fixture providing pre-generated factories for each standard option combination.
    ///     Must not be null.
    /// </param>
    public CppGeneratorTests(CppGeneratorFixture fixture)
    {
        // Store the shared fixture so every test can read from the pre-generated factories
        _fixture = fixture;
    }

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

    /// <summary>Validates that passing a null factory to <see cref="CppGenerator.Parse"/> throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void CppGenerator_Generate_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var generator = new CppGenerator(BuildOptions());

        // Act / Assert: null factory must be rejected before any I/O is attempted
        Assert.Throws<ArgumentNullException>(() => generator.Parse(new InMemoryContext()).Emit(null!, new EmitConfig(), new InMemoryContext()));
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
        Assert.Throws<DirectoryNotFoundException>(() => generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext()));
    }

    /// <summary>Validates that generating from valid headers creates the <c>api</c> entrypoint page.</summary>
    [Fact]
    public void CppGenerator_Generate_ValidHeaders_CreatesApiEntrypoint()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert
        Assert.True(factory.Writers.ContainsKey("api"), "Expected api.md to be created");
    }

    /// <summary>Validates that generating from valid headers creates a namespace summary page for the fixtures namespace.</summary>
    [Fact]
    public void CppGenerator_Generate_ValidHeaders_CreatesNamespacePage()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.WithDeprecatedFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicAndProtectedFactory;

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
        var factory = _fixture.AllFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
    ///     Validates that an individual method member page uses an H1 heading, consistent
    ///     with the page-as-standalone-document convention.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_MemberPage_UsesH1Heading()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: the GetGreeting member page must open with an H1 heading containing the
        // class and method name so every generated page starts at H1
        var writer = factory.Writers["fixtures/SampleClass/GetGreeting"];
        var firstHeading = writer.Operations.OfType<HeadingOperation>().First();
        Assert.Equal(1, firstHeading.Level);
        Assert.Contains("GetGreeting", firstHeading.Text, StringComparison.Ordinal);
    }

    /// <summary>Validates that parameterless methods and free functions all receive separate pages.</summary>
    [Fact]
    public void CppGenerator_AllMembers_GetSeparateFiles()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

        // Assert: Circle inherits from Shape and must receive its own documentation page
        Assert.True(
            factory.Writers.ContainsKey("fixtures/Circle"),
            "Expected type page for Circle at 'fixtures/Circle'");
    }

    /// <summary>
    ///     Validates that the type page for a class with a base class includes the base class
    ///     name in the signature block so that the inheritance relationship is visible without
    ///     opening the header file.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_InheritanceClass_EmitsBaseClassInSignature()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: the Circle type page must exist
        Assert.True(factory.Writers.TryGetValue("fixtures/Circle", out var writer), "Expected type page for Circle");
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.Contains(
            signatures,
            s => s.Contains(": public Shape", StringComparison.Ordinal));
    }

    /// <summary>Validates that the <c>Circle</c> constructor receives its own member detail page.</summary>
    [Fact]
    public void CppGenerator_Generate_Constructor_CreatesConstructorPage()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

        // Assert: no page should be created using the exact-case name "Name" when a collision
        // exists — the combined lowercase page replaces all individual pages for the group
        Assert.False(
            factory.Writers.ContainsKey("fixtures/CaseCollisionClass/Name"),
            "Expected no separate page for 'Name' when a case-insensitive collision exists");
    }

    /// <summary>
    ///     Validates that the combined collision page contains H2 headings for both the
    ///     method <c>Name()</c> and the field <c>name</c>.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_CaseCollisionClass_CombinedPageContainsBothMembers()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: combined page exists
        Assert.True(factory.Writers.TryGetValue("fixtures/CaseCollisionClass/name", out var writer));

        // Assert: both members appear as distinct H2 headings on the combined page
        var level2Headings = writer.Operations
            .OfType<HeadingOperation>()
            .Where(h => h.Level == 2)
            .Select(h => h.Text)
            .ToList();
        Assert.Contains(level2Headings, h => h.StartsWith("Name", StringComparison.Ordinal));
        Assert.Contains(level2Headings, h => h.StartsWith("name", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a method with a <c>@details</c> Doxygen block emits the extended
    ///     details text as a second paragraph after the brief summary paragraph.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_MethodWithDetails_WritesDetailsParagraph()
    {
        // Arrange: RemarksClass::Compute carries a @details block in its Doxygen comment
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

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
        var factory = _fixture.PublicFactory;

        // Assert: the Add free-function page must contain an #include directive in its signature
        // block so readers can include the correct header without browsing the source tree
        var writer = factory.Writers["fixtures/Add"];
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.Contains(
            signatures,
            s => s.Contains("#include", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that when no <see cref="CppGeneratorOptions.ApiHeaderPatterns"/> are set,
    ///     all headers under the include roots with recognized C++ extensions are documented.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_NoApiHeaderPatterns_DocumentsAllHeaders()
    {
        // Arrange: options with no ApiHeaderPatterns — the default glob should apply
        var options = new CppGeneratorOptions
        {
            LibraryName = "Fixtures",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
            // ApiHeaderPatterns intentionally left empty to exercise the default behavior
        };
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(options);

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: pages from multiple fixture headers must all be present, confirming the
        // default glob includes every recognized header under the include root
        Assert.True(
            factory.Writers.ContainsKey("fixtures/SampleClass"),
            "Expected SampleClass page when no ApiHeaderPatterns are set");
        Assert.True(
            factory.Writers.ContainsKey("fixtures/SampleStatus"),
            "Expected SampleStatus page when no ApiHeaderPatterns are set");
        Assert.True(
            factory.Writers.ContainsKey("fixtures/FinalClass"),
            "Expected FinalClass page when no ApiHeaderPatterns are set");
    }

    /// <summary>
    ///     Validates that <see cref="CppGeneratorOptions.ApiHeaderPatterns"/> with a specific
    ///     include pattern restricts header enumeration to files matching that pattern.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_ApiHeaderPatterns_IncludePattern_OnlyMatchingFilesDocumented()
    {
        // Arrange: include only SampleClass.h to isolate a single type
        var options = new CppGeneratorOptions
        {
            LibraryName = "Fixtures",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
            ApiHeaderPatterns = ["**/SampleClass.h"],
        };
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(options);

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

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
    ///     Validates that a <c>!</c>-prefixed exclusion pattern in
    ///     <see cref="CppGeneratorOptions.ApiHeaderPatterns"/> excludes matching headers while
    ///     leaving all other headers documented.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_ApiHeaderPatterns_ExcludePattern_ExcludesMatchingFiles()
    {
        // Arrange: catch-all followed by an exclusion pattern for SampleClass.h
        var options = new CppGeneratorOptions
        {
            LibraryName = "Fixtures",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
            ApiHeaderPatterns = ["**/*", "!**/SampleClass.h"],
        };
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(options);

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: SampleClass page must not exist because its header was excluded by the exclusion pattern
        Assert.False(
            factory.Writers.ContainsKey("fixtures/SampleClass"),
            "Expected SampleClass to be absent when its header is excluded by exclusion pattern");

        // Assert: SampleStatus page must still exist because SampleEnum.h was not excluded
        Assert.True(
            factory.Writers.ContainsKey("fixtures/SampleStatus"),
            "Expected SampleStatus to be present when only SampleClass.h is excluded");
    }

    /// <summary>
    ///     Validates gitignore-style re-include semantics: a header that is first excluded by
    ///     an exclusion pattern and then re-included by a later positive pattern is documented.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_ApiHeaderPatterns_ReInclude_GitignoreSemantics_IncludesReIncludedHeader()
    {
        // Arrange: catch-all, exclude DeprecatedClass.h, then re-include it;
        // IncludeDeprecated=true so the class appears in output even when its header is parsed
        var options = new CppGeneratorOptions
        {
            LibraryName = "Fixtures",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
            ApiHeaderPatterns = ["**/*", "!**/DeprecatedClass.h", "**/DeprecatedClass.h"],
            IncludeDeprecated = true,
        };
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(options);

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: DeprecatedClass must be documented because the re-include pattern wins
        Assert.True(
            factory.Writers.ContainsKey("fixtures/DeprecatedClass"),
            "Expected DeprecatedClass page when re-included after exclusion");
    }

    /// <summary>
    ///     Validates that an exclusion pattern without a subsequent re-include permanently
    ///     removes a header from documentation, confirming last-match-wins semantics.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_ApiHeaderPatterns_ExcludeWithoutReInclude_ExcludesHeader()
    {
        // Arrange: catch-all followed by exclusion pattern for DeprecatedClass.h (no re-include);
        // IncludeDeprecated=true so the class would appear if the header were parsed
        var options = new CppGeneratorOptions
        {
            LibraryName = "Fixtures",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
            ApiHeaderPatterns = ["**/*", "!**/DeprecatedClass.h"],
            IncludeDeprecated = true,
        };
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(options);

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: DeprecatedClass must not be documented because the exclusion pattern is final
        Assert.False(
            factory.Writers.ContainsKey("fixtures/DeprecatedClass"),
            "Expected DeprecatedClass to be absent when excluded without re-include");
    }

    /// <summary>
    ///     Validates that symbols from a transitively-included header that is under a configured
    ///     PublicIncludeRoot but was NOT selected by ApiHeaderPatterns are excluded from output.
    ///     This confirms that api-headers filtering controls symbol ownership, not just which
    ///     files are passed to clang.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_ApiHeaderPatterns_TransitiveInclude_ExcludesNonSelectedSymbols()
    {
        // Arrange: select only TypeLinkClass.h; InheritanceClass.h is transitively included by it
        // but must NOT be treated as an owned source file
        var options = new CppGeneratorOptions
        {
            LibraryName = "Fixtures",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
            ApiHeaderPatterns = ["**/TypeLinkClass.h"],
        };
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(options);

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: TypeLinkClass page must exist — its header was selected
        Assert.True(
            factory.Writers.ContainsKey("fixtures/TypeLinkClass"),
            "Expected TypeLinkClass page when its header is selected");

        // Assert: Shape and Circle must be absent — they are defined in InheritanceClass.h which
        // was not selected by --api-headers, even though it is under PublicIncludeRoot
        Assert.False(
            factory.Writers.ContainsKey("fixtures/Shape"),
            "Expected Shape to be absent: InheritanceClass.h was not selected by --api-headers");
        Assert.False(
            factory.Writers.ContainsKey("fixtures/Circle"),
            "Expected Circle to be absent: InheritanceClass.h was not selected by --api-headers");
    }

    /// <summary>
    ///     Validates that a CWD-relative <see cref="CppGeneratorOptions.ApiHeaderPatterns"/> entry
    ///     selects only the file it names when resolved from the current working directory, confirming
    ///     that relative patterns are resolved from the CWD and not from each include root.
    /// </summary>
    /// <remarks>
    ///     This test would fail with the old include-root-expansion behavior because that behavior
    ///     joined the pattern onto each include root, producing a doubled path that matches nothing.
    ///     The pattern is computed at runtime via <see cref="Path.GetRelativePath"/> so that the test
    ///     works on any developer machine or CI environment regardless of where the repo is checked out.
    /// </remarks>
    [Fact]
    public void CppGenerator_Generate_ApiHeaderPatterns_CwdRelativePattern_OnlyMatchingFilesDocumented()
    {
        // Arrange: compute a CWD-relative path to SampleClass.h that is NOT root-agnostic.
        // Using a non-**/ prefix means the pattern can only resolve correctly from the CWD.
        var absoluteSampleClassPath = Path.Join(FixturePaths.GetFixtureNamespaceDir(), "SampleClass.h");
        var cwdRelativePattern = Path.GetRelativePath(Directory.GetCurrentDirectory(), absoluteSampleClassPath);

        var options = new CppGeneratorOptions
        {
            LibraryName = "Fixtures",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
            ApiHeaderPatterns = [cwdRelativePattern],
        };
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(options);

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: SampleClass page must exist because its header was selected by the CWD-relative pattern
        Assert.True(
            factory.Writers.ContainsKey("fixtures/SampleClass"),
            "Expected SampleClass page when selected by a CWD-relative pattern");

        // Assert: SampleStatus page must not exist because SampleEnum.h was not selected
        Assert.False(
            factory.Writers.ContainsKey("fixtures/SampleStatus"),
            "Expected SampleStatus to be absent when not matched by the CWD-relative include pattern");
    }

    /// <summary>
    ///     Validates that a CWD-relative exclusion pattern removes the named file from the documented
    ///     header set while leaving all other headers present, confirming that CWD-relative resolution
    ///     applies to both inclusion and exclusion patterns.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_ApiHeaderPatterns_CwdRelativeExclusionPattern_ExcludesMatchingFiles()
    {
        // Arrange: compute a CWD-relative path to SampleClass.h and use it as an exclusion.
        var absoluteSampleClassPath = Path.Join(FixturePaths.GetFixtureNamespaceDir(), "SampleClass.h");
        var cwdRelativeExclusion = "!" + Path.GetRelativePath(Directory.GetCurrentDirectory(), absoluteSampleClassPath);

        var options = new CppGeneratorOptions
        {
            LibraryName = "Fixtures",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
            ApiHeaderPatterns = ["**/*", cwdRelativeExclusion],
        };
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(options);

        // Act
        generator.Parse(new InMemoryContext()).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: SampleClass page must not exist because its header was excluded by the CWD-relative exclusion pattern
        Assert.False(
            factory.Writers.ContainsKey("fixtures/SampleClass"),
            "Expected SampleClass to be absent when its header is excluded by a CWD-relative exclusion pattern");

        // Assert: SampleStatus page must still exist because SampleEnum.h was not excluded
        Assert.True(
            factory.Writers.ContainsKey("fixtures/SampleStatus"),
            "Expected SampleStatus to be present when only SampleClass.h is excluded by CWD-relative pattern");
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
        var factory = _fixture.PublicFactory;

        // Assert: the FinalClass type page must exist
        Assert.True(factory.Writers.TryGetValue("fixtures/FinalClass", out var writer), "Expected type page for FinalClass");
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
        var factory = _fixture.PublicFactory;

        // Assert: the SampleClass type page must exist and must not contain "final"
        // because SampleClass is not declared final
        Assert.True(factory.Writers.TryGetValue("fixtures/SampleClass", out var writer), "Expected type page for SampleClass");
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.DoesNotContain(
            signatures,
            s => s.Contains("final", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a class with operator overloads produces a shared <c>operators.md</c>
    ///     page at the expected key, rather than individual colliding pages for each operator.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_ClassWithOperators_CreatesOperatorsPage()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: a single shared operators page is written for OperatorClass instead of
        // individual pages that would collide because operator+, operator-, etc. all
        // sanitize to the same file name
        Assert.True(
            factory.Writers.ContainsKey("fixtures/OperatorClass/operators"),
            "Expected a shared operators page at 'fixtures/OperatorClass/operators'");
    }

    /// <summary>
    ///     Validates that the operators page for a class with operator overloads contains
    ///     an entry for each declared operator, confirming all overloads are documented.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_ClassWithOperators_OperatorsPageContainsOperatorEntry()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: the operators page must exist
        Assert.True(factory.Writers.TryGetValue("fixtures/OperatorClass/operators", out var writer));

        // Assert: operator+ must appear as a heading on the operators page so readers
        // can navigate directly to the addition overload
        var headings = writer.Operations
            .OfType<HeadingOperation>()
            .Select(h => h.Text)
            .ToList();
        Assert.Contains(headings, h => h.Contains("operator+", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that the type page for a class with operator overloads contains a link
    ///     to the shared <c>operators.md</c> page rather than to individual operator pages.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_ClassWithOperators_TypePageLinksToOperatorsPage()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: the OperatorClass type page must exist
        Assert.True(factory.Writers.TryGetValue("fixtures/OperatorClass", out var writer));

        // Assert: at least one table on the type page must contain a cell referencing
        // operators.md so that readers can navigate from the type page to the operators page
        var tables = writer.Operations.OfType<TableOperation>().ToList();
        Assert.Contains(
            tables,
            t => t.Rows.Any(row => row.Any(cell => cell.Contains("operators.md", StringComparison.Ordinal))));
    }

    /// <summary>
    ///     Validates that namespace-level operator free functions are grouped onto a single
    ///     <c>operators.md</c> page at the namespace level, rather than producing individual
    ///     colliding pages for each operator overload.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_NamespaceFreeOperator_CreatesNamespaceOperatorsPage()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: a shared namespace operators page is written under the fixtures namespace
        // for the free operator<< declared in OperatorClass.h
        Assert.True(
            factory.Writers.ContainsKey("fixtures/operators"),
            "Expected a shared namespace operators page at 'fixtures/operators'");
    }

    /// <summary>
    ///     Validates that a clang diagnostic whose source path does not belong to any
    ///     public header file is routed to <see cref="InMemoryContext.Lines"/> rather
    ///     than causing an <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void CppGenerator_CheckForErrors_SystemHeaderDiagnostic_RoutesToContextLines()
    {
        // Arrange: a compilation result that contains one error from a path that does
        // not match any public header — simulating a clang diagnostic from a system
        // or third-party header included transitively by the public headers
        const string systemError = "/usr/include/stdio.h:42:5: error: unknown builtin";
        var result = new CppCompilationResult([], [systemError]);
        var context = new InMemoryContext();

        // Act: invoke CheckForErrors with an empty public-header list so no error
        // is classified as a user error; the method must not throw
        CppGenerator.CheckForErrors(result, [], context);

        // Assert: the system-header diagnostic is forwarded to context.Lines with
        // the "[CppGenerator] clang:" prefix so callers can identify its origin
        Assert.Contains(
            context.Lines,
            line => line.Contains("[CppGenerator] clang:", StringComparison.Ordinal)
                    && line.Contains(systemError, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a method returning an intra-library type emits a Markdown link in
    ///     the Returns column of the type page's Methods table.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_IntraLibraryReturnType_EmitsMarkdownLinkInReturnsCell()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: TypeLinkClass type page must exist
        Assert.True(
            factory.Writers.TryGetValue("fixtures/TypeLinkClass", out var writer),
            "Expected type page for TypeLinkClass");
        var operations = writer.Operations.ToList();
        var methodsIndex = operations.FindIndex(op => op is HeadingOperation h && h.Text == "Methods");
        Assert.True(methodsIndex >= 0, "Expected 'Methods' heading on TypeLinkClass page");
        var methodsTable = operations.Skip(methodsIndex + 1).OfType<TableOperation>().First();

        // Returns column (index 1) for CreateShape must contain a Markdown link, not plain text
        var row = methodsTable.Rows.FirstOrDefault(r => r[0].Contains("CreateShape", StringComparison.Ordinal));
        Assert.NotNull(row);
        Assert.Contains("[Shape]", row![1], StringComparison.Ordinal);
        Assert.Contains("Shape.md", row[1], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that <see cref="CppTypeLinkResolver.Linkify"/> emits plain text and records
    ///     the type in the external set when the type is not in the known-types dictionary.
    /// </summary>
    [Fact]
    public void CppTypeLinkResolver_Linkify_UnknownNamespacedType_TracksExternalType()
    {
        // Arrange: a resolver with no known types, simulating a fully external reference
        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal));
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Act: resolve a type that belongs to a non-std namespace not in the known-types map
        var result = resolver.Linkify("acme::Logger *", string.Empty, externalTypes);

        // Assert: the original type string is returned as-is (no link to an unknown page)
        Assert.Equal("acme::Logger *", result);

        // Assert: the type is tracked in the external types set with the correct namespace
        Assert.Single(externalTypes);
        Assert.Equal("Logger", externalTypes.First().TypeString);
        Assert.Equal("acme", externalTypes.First().Namespace);
    }

    /// <summary>
    ///     Validates that a deleted copy constructor is still documented and that its
    ///     signature contains <c>= delete</c> so that readers know copying is explicitly
    ///     forbidden without needing to open the header file.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_DeletedCopyConstructor_EmitsDeleteSuffix()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: the type page must exist
        Assert.True(
            factory.Writers.ContainsKey("fixtures/DeletedMembersClass"),
            "Expected type page for DeletedMembersClass");

        // Assert: both constructors share the same base name, so they are combined onto
        // a single page keyed by the lowercase class name
        Assert.True(
            factory.Writers.TryGetValue("fixtures/DeletedMembersClass/deletedmembersclass", out var writer),
            "Expected combined constructor page for DeletedMembersClass");

        // Assert: the combined constructor page must contain "= delete" for the deleted overload
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.Contains(
            signatures,
            s => s.Contains("= delete", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a deleted copy-assignment operator is still documented and that its
    ///     signature contains <c>= delete</c> so that readers know assignment is explicitly
    ///     forbidden without needing to open the header file.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_DeletedCopyAssignmentOperator_EmitsDeleteSuffix()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: the operators page for DeletedMembersClass must contain "= delete"
        Assert.True(
            factory.Writers.TryGetValue("fixtures/DeletedMembersClass/operators", out var writer),
            "Expected operators page for DeletedMembersClass");
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.Contains(
            signatures,
            s => s.Contains("= delete", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that generating from valid fixture headers creates individual pages
    ///     for each <c>using</c> type alias declared in the <c>TypeAliasFixtures.h</c> header.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_TypeAlias_CreatesAliasPages()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: each alias gets its own page under the namespace folder
        Assert.True(
            factory.Writers.ContainsKey("fixtures/item_id_t"),
            "Expected type alias page for item_id_t at 'fixtures/item_id_t'");
        Assert.True(
            factory.Writers.ContainsKey("fixtures/label_t"),
            "Expected type alias page for label_t at 'fixtures/label_t'");
    }

    /// <summary>
    ///     Validates that the type alias page for <c>item_id_t</c> contains the correct
    ///     <c>using</c> declaration and the doc-comment summary.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_TypeAliasPage_ContainsDeclarationAndSummary()
    {
        // Arrange
        var factory = _fixture.PublicFactory;
        var writer = factory.Writers["fixtures/item_id_t"];

        // Assert: the signature block must include the using declaration
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.Contains(
            signatures,
            s => s.Contains("using item_id_t = int32_t", StringComparison.Ordinal));

        // Assert: the summary paragraph must be present
        var paragraphs = writer.Operations.OfType<ParagraphOperation>().Select(p => p.Text).ToList();
        Assert.Contains(
            paragraphs,
            p => p.Contains("32-bit signed integer identifier", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that the <c>fixtures</c> namespace summary page lists each type alias
    ///     in a "Type Aliases" section so readers can discover them without opening individual pages.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_NamespacePage_ListsTypeAliases()
    {
        // Arrange
        var factory = _fixture.PublicFactory;
        var writer = factory.Writers["fixtures"];

        // Assert: the namespace page must contain a "Type Aliases" heading
        var headings = writer.Operations.OfType<HeadingOperation>().Select(h => h.Text).ToList();
        Assert.Contains(headings, h => h.Contains("Type Aliases", StringComparison.Ordinal));

        // Assert: the namespace page table must contain links to both alias pages
        var tables = writer.Operations.OfType<TableOperation>().ToList();
        var allCells = tables.SelectMany(t => t.Rows).SelectMany(r => r).ToList();
        Assert.Contains(allCells, c => c.Contains("item_id_t", StringComparison.Ordinal));
        Assert.Contains(allCells, c => c.Contains("label_t", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that the type alias page for <c>label_t</c> shows a simplified underlying
    ///     type (<c>std::string</c> rather than the verbose clang form
    ///     <c>std::basic_string&lt;char, ...&gt;</c>), and that the namespace summary table
    ///     also uses the simplified form.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_TypeAliasPage_SimplifiesUnderlyingType()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: the alias page declaration must use the simplified form (not the verbose clang form)
        var aliasWriter = factory.Writers["fixtures/label_t"];
        var signatures = aliasWriter.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.Contains(
            signatures,
            s => s.Contains("using label_t = std::string", StringComparison.Ordinal));
        Assert.DoesNotContain(
            signatures,
            s => s.Contains("basic_string", StringComparison.Ordinal));

        // Assert: the namespace summary table must also show the simplified underlying type
        var nsWriter = factory.Writers["fixtures"];
        var allCells = nsWriter.Operations.OfType<TableOperation>()
            .SelectMany(t => t.Rows).SelectMany(r => r).ToList();
        var underlyingCell = allCells
            .SkipWhile(c => !c.Contains("label_t", StringComparison.Ordinal))
            .Skip(1)
            .FirstOrDefault();
        Assert.NotNull(underlyingCell);
        Assert.DoesNotContain("basic_string", underlyingCell, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a free function with a default parameter value includes the default
    ///     in its signature block (e.g. <c>uint32_t seed = 0</c>).
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_DefaultParameter_SignatureContainsDefault()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: crc32 page must show the default value in its signature
        Assert.True(factory.Writers.TryGetValue("fixtures/crc32", out var writer));
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.Contains(
            signatures,
            s => s.Contains("seed = 0", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a Doxygen <c>@note</c> tag is rendered as a blockquote paragraph
    ///     (prefixed with <c>&gt; **Note:**</c>) on the function's detail page.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_BoolDefaultParameter_SignatureContainsFalse()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: configure page must show the bool default in its signature
        Assert.True(factory.Writers.TryGetValue("fixtures/configure", out var writer));
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.Contains(
            signatures,
            s => s.Contains("initial = false", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a negative integer default argument (e.g. <c>int max = -1</c>)
    ///     is rendered correctly in the function signature.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_NegativeIntDefaultParameter_SignatureContainsNegativeValue()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: count_capped page must show the negative default in its signature
        Assert.True(factory.Writers.TryGetValue("fixtures/count_capped", out var writer));
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.Contains(
            signatures,
            s => s.Contains("max = -1", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a floating-point default argument (e.g. <c>float factor = 1.5f</c>)
    ///     is rendered in the function signature without the type suffix.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_FloatDefaultParameter_SignatureContainsValue()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: scale page must show the float default in its signature
        Assert.True(factory.Writers.TryGetValue("fixtures/scale", out var writer));
        var signatures = writer.Operations.OfType<SignatureOperation>().Select(s => s.Code).ToList();
        Assert.Contains(
            signatures,
            s => s.Contains("factor = 1.5", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a nested class declared inside a public outer class receives its
    ///     own type page under the outer class's folder.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_NestedClass_CreatesNestedClassPage()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: Inner is a public nested class of Outer; its page must be under Outer's folder
        Assert.True(
            factory.Writers.ContainsKey("fixtures/Outer/Inner"),
            "Expected nested class page at 'fixtures/Outer/Inner'");
    }

    /// <summary>
    ///     Validates that the outer class's type page lists the nested class in a
    ///     "Nested Classes" section so readers can discover inner types without opening the header.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_NestedClass_ListedOnOuterClassPage()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: the Outer type page must exist
        Assert.True(factory.Writers.TryGetValue("fixtures/Outer", out var writer), "Expected type page for Outer");

        // Assert: the page must include a "Nested Classes" heading
        var headings = writer.Operations.OfType<HeadingOperation>().Select(h => h.Text).ToList();
        Assert.Contains(headings, h => h.Contains("Nested Classes", StringComparison.Ordinal));

        // Assert: the "Nested Classes" table must contain "Inner" in a link cell
        var tables = writer.Operations.OfType<TableOperation>().ToList();
        var allCells = tables.SelectMany(t => t.Rows).SelectMany(r => r).ToList();
        Assert.Contains(allCells, c => c.Contains("Inner", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a <c>using</c> type alias declared inside a public class body receives
    ///     its own page under the class's folder so readers can navigate to it directly.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_ClassScopedTypeAlias_CreatesAliasPage()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: Outer::size_type must receive its own page under the Outer class folder
        Assert.True(
            factory.Writers.ContainsKey("fixtures/Outer/size_type"),
            "Expected class-scoped alias page at 'fixtures/Outer/size_type'");
    }

    /// <summary>
    ///     Validates that the outer class's type page lists its class-scoped type alias in a
    ///     "Type Aliases" section so readers can see the alias without navigating to a sub-page.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_ClassScopedTypeAlias_ListedOnClassPage()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: the Outer type page must exist
        Assert.True(factory.Writers.TryGetValue("fixtures/Outer", out var writer), "Expected type page for Outer");

        // Assert: the page must include a "Type Aliases" heading
        var headings = writer.Operations.OfType<HeadingOperation>().Select(h => h.Text).ToList();
        Assert.Contains(headings, h => h.Contains("Type Aliases", StringComparison.Ordinal));

        // Assert: the "Type Aliases" table must contain "size_type"
        var tables = writer.Operations.OfType<TableOperation>().ToList();
        var allCells = tables.SelectMany(t => t.Rows).SelectMany(r => r).ToList();
        Assert.Contains(allCells, c => c.Contains("size_type", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that two classes declaring the same alias name (<c>size_type</c>) each
    ///     produce a distinct page keyed by their fully-qualified class scope, confirming that
    ///     the <c>knownTypes</c> map does not collide across different owning classes.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_ClassScopedTypeAlias_DoesNotCollideAcrossClasses()
    {
        // Arrange
        var factory = _fixture.PublicFactory;

        // Assert: Outer::size_type and Other::size_type must each get their own distinct page;
        // a collision would cause only one of them to exist
        Assert.True(
            factory.Writers.TryGetValue("fixtures/Outer/size_type", out var outerSizeTypeWriter),
            "Expected alias page for Outer::size_type at 'fixtures/Outer/size_type'");
        Assert.True(
            factory.Writers.TryGetValue("fixtures/Other/size_type", out var otherSizeTypeWriter),
            "Expected alias page for Other::size_type at 'fixtures/Other/size_type'");

        // Assert: the two pages must be distinct writer instances so they contain different content
        Assert.NotSame(outerSizeTypeWriter, otherSizeTypeWriter);
    }

    /// <summary>
    ///     Validates that single-file output writes all API surface into a single <c>api</c>
    ///     writer with H1 library name, H2 namespace, H3 class, and H4 member headings,
    ///     no group headings (Constructors/Methods), and a compact bullet-list paragraph
    ///     summarizing each class's members.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_SingleFileOutput_WritesSingleApiMarkdown()
    {
        // Arrange: run a fresh generator with SingleFile format — the shared fixture uses
        // GradualDisclosure and cannot be reused for this test
        var factory = new InMemoryMarkdownWriterFactory();
        var generator = new CppGenerator(BuildOptions());

        // Act
        generator.Parse(new InMemoryContext()).Emit(
            factory,
            new EmitConfig { Format = OutputFormat.SingleFile },
            new InMemoryContext());

        // Assert: exactly one writer, keyed "api"
        Assert.Single(factory.Writers);
        Assert.True(factory.Writers.TryGetValue("api", out var writer), "Expected a single api writer for single-file output");
        var headings = writer.Operations.OfType<HeadingOperation>().ToList();

        // Assert: H1 is the library-level title containing the library name
        Assert.Contains(
            headings,
            h => h.Level == 1 && h.Text.Contains("Fixtures", StringComparison.Ordinal));

        // Assert: H2 is the namespace heading
        Assert.Contains(
            headings,
            h => h.Level == 2 && h.Text.Contains("fixtures", StringComparison.Ordinal));

        // Assert: H3 is a class heading — SampleClass is always present in the fixture headers
        Assert.Contains(headings, h => h.Level == 3 && h.Text == "SampleClass");

        // Assert: H4 is a member heading — methods contain parentheses
        Assert.Contains(
            headings,
            h => h.Level == 4 && h.Text.Contains("(", StringComparison.Ordinal));

        // Assert: no group headings — single-file format emits members directly without section labels
        Assert.DoesNotContain(headings, h => h.Text == "Constructors");
        Assert.DoesNotContain(headings, h => h.Text == "Methods");

        // Assert: at least one compact bullet-list paragraph summarizing members is emitted
        var paragraphs = writer.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p => p.Text.Contains("- **", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a method with a Doxygen <c>@code</c>/<c>@endcode</c> example block
    ///     produces a fenced <c>cpp</c> code block on its gradual-disclosure member page.
    /// </summary>
    [Fact]
    public void CppGenerator_Generate_MethodWithCodeExample_EmitsCodeBlockOnMemberPage()
    {
        // Act: locate the member page for ExampleDocClass::GetGreeting
        var memberPage = _fixture.PublicFactory.Writers["fixtures/ExampleDocClass/GetGreeting"];

        // Assert: a fenced cpp code block was written
        var codeBlocks = memberPage.Operations.OfType<CodeBlockOperation>().ToList();
        Assert.Contains(codeBlocks, cb =>
            string.Equals(cb.Language, "cpp", StringComparison.Ordinal) &&
            cb.Code.Contains("GetGreeting", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a method with a Doxygen <c>@code</c>/<c>@endcode</c> example block
    ///     produces a fenced <c>cpp</c> code block in single-file output.
    /// </summary>
    [Fact]
    public void CppGenerator_SingleFile_MethodWithCodeExample_EmitsCodeBlock()
    {
        // Act: locate the single api.md writer and find the code blocks
        var writer = _fixture.PublicSingleFileFactory.Writers["api"];

        // Assert: a fenced cpp code block containing the example content was written
        var codeBlocks = writer.Operations.OfType<CodeBlockOperation>().ToList();
        Assert.Contains(codeBlocks, cb =>
            string.Equals(cb.Language, "cpp", StringComparison.Ordinal) &&
            cb.Code.Contains("GetGreeting", StringComparison.Ordinal));
    }
}
