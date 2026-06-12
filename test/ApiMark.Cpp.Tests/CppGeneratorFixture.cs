using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.Cpp;

namespace ApiMark.Cpp.Tests;

/// <summary>
///     xUnit class fixture that runs <see cref="CppGenerator.Generate"/> exactly once per
///     option combination per test run, caching the resulting
///     <see cref="InMemoryMarkdownWriterFactory"/> instances for every test in
///     <see cref="CppGeneratorTests"/> to read.
/// </summary>
/// <remarks>
///     Without this fixture each test would invoke clang independently, making the suite
///     proportionally slower as new tests are added.  Sharing a single fixture instance
///     caps clang invocations at five regardless of test count, while keeping each test
///     focused on a single assertion over pre-built output.
///     The five option combinations covered are:
///     <list type="bullet">
///       <item><see cref="PublicFactory"/> — <see cref="ApiVisibility.Public"/>, <c>IncludeDeprecated = false</c></item>
///       <item><see cref="WithDeprecatedFactory"/> — <see cref="ApiVisibility.Public"/>, <c>IncludeDeprecated = true</c></item>
///       <item><see cref="PublicAndProtectedFactory"/> — <see cref="ApiVisibility.PublicAndProtected"/>, <c>IncludeDeprecated = false</c></item>
///       <item><see cref="AllFactory"/> — <see cref="ApiVisibility.All"/>, <c>IncludeDeprecated = false</c></item>
///       <item><see cref="PublicSingleFileFactory"/> — <see cref="ApiVisibility.Public"/>, single-file output format</item>
///     </list>
///     All factories are immutable after construction; tests must only read from them.
/// </remarks>
public sealed class CppGeneratorFixture
{
    /// <summary>
    ///     Gets the factory produced by a <see cref="CppGenerator"/> configured for
    ///     <see cref="ApiVisibility.Public"/> with <c>IncludeDeprecated = false</c>.
    /// </summary>
    /// <value>
    ///     A populated <see cref="InMemoryMarkdownWriterFactory"/> whose writers reflect
    ///     the public API of the fixture headers, excluding deprecated declarations.
    /// </value>
    public InMemoryMarkdownWriterFactory PublicFactory { get; }

    /// <summary>
    ///     Gets the factory produced by a <see cref="CppGenerator"/> configured for
    ///     <see cref="ApiVisibility.Public"/> with <c>IncludeDeprecated = true</c>.
    /// </summary>
    /// <value>
    ///     A populated <see cref="InMemoryMarkdownWriterFactory"/> whose writers reflect
    ///     the public API of the fixture headers, including deprecated declarations.
    /// </value>
    public InMemoryMarkdownWriterFactory WithDeprecatedFactory { get; }

    /// <summary>
    ///     Gets the factory produced by a <see cref="CppGenerator"/> configured for
    ///     <see cref="ApiVisibility.PublicAndProtected"/> with <c>IncludeDeprecated = false</c>.
    /// </summary>
    /// <value>
    ///     A populated <see cref="InMemoryMarkdownWriterFactory"/> whose writers reflect
    ///     the public and protected API of the fixture headers, excluding deprecated declarations.
    /// </value>
    public InMemoryMarkdownWriterFactory PublicAndProtectedFactory { get; }

    /// <summary>
    ///     Gets the factory produced by a <see cref="CppGenerator"/> configured for
    ///     <see cref="ApiVisibility.All"/> with <c>IncludeDeprecated = false</c>.
    /// </summary>
    /// <value>
    ///     A populated <see cref="InMemoryMarkdownWriterFactory"/> whose writers reflect
    ///     the entire API surface (public, protected, and private) of the fixture headers,
    ///     excluding deprecated declarations.
    /// </value>
    public InMemoryMarkdownWriterFactory AllFactory { get; }

    /// <summary>
    ///     Gets the factory produced by a <see cref="CppGenerator"/> configured for
    ///     <see cref="ApiVisibility.Public"/> using <see cref="OutputFormat.SingleFile"/> output.
    /// </summary>
    /// <value>
    ///     A populated <see cref="InMemoryMarkdownWriterFactory"/> whose single writer reflects
    ///     the entire public API surface in a single <c>api.md</c> document.
    /// </value>
    public InMemoryMarkdownWriterFactory PublicSingleFileFactory { get; }

    /// <summary>
    ///     Initializes the fixture by invoking <see cref="CppGenerator.Generate"/> once for
    ///     each of the four standard option combinations and storing the resulting factories.
    /// </summary>
    /// <remarks>
    ///     xUnit constructs this fixture once per test class and shares it across all tests
    ///     in <see cref="CppGeneratorTests"/>, so clang is invoked at most four times per run.
    /// </remarks>
    public CppGeneratorFixture()
    {
        // Run with Public visibility, excluding deprecated declarations
        var publicFactory = new InMemoryMarkdownWriterFactory();
        new CppGenerator(BuildOptions(ApiVisibility.Public, includeDeprecated: false))
            .Parse(new InMemoryContext()).Emit(publicFactory, new EmitConfig(), new InMemoryContext());
        PublicFactory = publicFactory;

        // Run with Public visibility, including deprecated declarations
        var withDeprecatedFactory = new InMemoryMarkdownWriterFactory();
        new CppGenerator(BuildOptions(ApiVisibility.Public, includeDeprecated: true))
            .Parse(new InMemoryContext()).Emit(withDeprecatedFactory, new EmitConfig(), new InMemoryContext());
        WithDeprecatedFactory = withDeprecatedFactory;

        // Run with PublicAndProtected visibility, excluding deprecated declarations
        var publicAndProtectedFactory = new InMemoryMarkdownWriterFactory();
        new CppGenerator(BuildOptions(ApiVisibility.PublicAndProtected, includeDeprecated: false))
            .Parse(new InMemoryContext()).Emit(publicAndProtectedFactory, new EmitConfig(), new InMemoryContext());
        PublicAndProtectedFactory = publicAndProtectedFactory;

        // Run with All visibility, excluding deprecated declarations
        var allFactory = new InMemoryMarkdownWriterFactory();
        new CppGenerator(BuildOptions(ApiVisibility.All, includeDeprecated: false))
            .Parse(new InMemoryContext()).Emit(allFactory, new EmitConfig(), new InMemoryContext());
        AllFactory = allFactory;

        // Run with Public visibility in single-file format
        var publicSingleFileFactory = new InMemoryMarkdownWriterFactory();
        new CppGenerator(BuildOptions(ApiVisibility.Public, includeDeprecated: false))
            .Parse(new InMemoryContext()).Emit(publicSingleFileFactory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());
        PublicSingleFileFactory = publicSingleFileFactory;
    }

    /// <summary>
    ///     Builds a <see cref="CppGeneratorOptions"/> pointing at the fixture include directory
    ///     with the specified visibility and deprecated settings.
    /// </summary>
    /// <param name="visibility">Which members to include in generated output.</param>
    /// <param name="includeDeprecated">Whether to include deprecated declarations.</param>
    /// <returns>A fully configured <see cref="CppGeneratorOptions"/> for the fixture headers.</returns>
    private static CppGeneratorOptions BuildOptions(
        ApiVisibility visibility,
        bool includeDeprecated)
    {
        return new CppGeneratorOptions
        {
            LibraryName = "Fixtures",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
            Visibility = visibility,
            IncludeDeprecated = includeDeprecated,
        };
    }
}
