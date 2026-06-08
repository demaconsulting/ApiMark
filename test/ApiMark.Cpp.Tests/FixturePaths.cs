using System.Runtime.CompilerServices;

namespace ApiMark.Cpp.Tests;

/// <summary>Helper that resolves fixture header paths for integration tests.</summary>
internal static class FixturePaths
{
    /// <summary>Absolute path to the fixture include directory resolved at compile time.</summary>
    private static readonly string IncludeDir = ResolveIncludeDir();

    /// <summary>
    ///     Returns the path to the fixtures include directory.
    /// </summary>
    /// <returns>Absolute path to the include/ directory containing fixture headers.</returns>
    public static string GetFixtureIncludeDir() => IncludeDir;

    /// <summary>Returns the path to the fixtures namespace directory.</summary>
    /// <returns>Absolute path to the include/fixtures/ directory containing per-fixture headers.</returns>
    public static string GetFixtureNamespaceDir() =>
        Path.Combine(IncludeDir, "fixtures");

    /// <summary>
    ///     Resolves the fixture include directory at compile time using <see cref="CallerFilePathAttribute"/>.
    /// </summary>
    /// <remarks>
    ///     <see cref="CallerFilePathAttribute"/> injects the compile-time path of this source file
    ///     (i.e. <c>test/ApiMark.Cpp.Tests/FixturePaths.cs</c>). Navigating one level up reaches
    ///     <c>test/</c>, from which the fixture headers under
    ///     <c>ApiMark.Cpp.Fixtures/include/</c> are directly reachable — no build output copy needed.
    /// </remarks>
    private static string ResolveIncludeDir([CallerFilePath] string? sourceFile = null)
    {
        var testDir = Path.GetDirectoryName(sourceFile)!;
        return Path.GetFullPath(Path.Combine(testDir, "..", "ApiMark.Cpp.Fixtures", "include"));
    }
}
