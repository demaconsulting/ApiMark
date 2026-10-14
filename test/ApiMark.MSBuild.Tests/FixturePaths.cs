using System.Runtime.CompilerServices;

namespace ApiMark.MSBuild.Tests;

/// <summary>Helper that resolves C++ fixture header paths for integration tests.</summary>
internal static class FixturePaths
{
    /// <summary>Absolute path to the fixture include directory resolved at compile time.</summary>
    private static readonly string IncludeDir = ResolveIncludeDir();

    /// <summary>
    ///     Returns the absolute path to the C++ fixture include directory.
    /// </summary>
    /// <returns>Absolute path to the <c>include/</c> directory containing fixture headers.</returns>
    public static string GetFixtureIncludeDir() => IncludeDir;

    /// <summary>
    ///     Returns the absolute path to the source MSBuild targets file of the unit under test.
    /// </summary>
    /// <param name="sourceFile">The compile-time path of this source file, injected by the compiler.</param>
    /// <returns>Absolute path to <c>src/ApiMark.MSBuild/build/DemaConsulting.ApiMark.MSBuild.targets</c>.</returns>
    public static string GetTargetsFilePath([CallerFilePath] string? sourceFile = null)
    {
        var testDir = Path.GetDirectoryName(sourceFile)!;
        return Path.GetFullPath(Path.Join(
            testDir, "..", "..", "src", "ApiMark.MSBuild", "build", "DemaConsulting.ApiMark.MSBuild.targets"));
    }

    /// <summary>
    ///     Resolves the fixture include directory at compile time using <see cref="CallerFilePathAttribute"/>.
    /// </summary>
    /// <remarks>
    ///     <see cref="CallerFilePathAttribute"/> injects the compile-time path of this source file
    ///     (i.e. <c>test/ApiMark.MSBuild.Tests/FixturePaths.cs</c>). Navigating one level up reaches
    ///     <c>test/</c>, from which the fixture headers under
    ///     <c>ApiMark.Cpp.Fixtures/include/</c> are directly reachable — no build output copy needed.
    /// </remarks>
    /// <param name="sourceFile">The compile-time path of this source file, injected by the compiler.</param>
    /// <returns>Absolute path to the <c>ApiMark.Cpp.Fixtures/include/</c> directory.</returns>
    private static string ResolveIncludeDir([CallerFilePath] string? sourceFile = null)
    {
        var testDir = Path.GetDirectoryName(sourceFile)!;
        return Path.GetFullPath(Path.Join(testDir, "..", "ApiMark.Cpp.Fixtures", "include"));
    }
}
