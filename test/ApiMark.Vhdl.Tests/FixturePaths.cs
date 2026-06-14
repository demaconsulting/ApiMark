using System.Runtime.CompilerServices;

namespace ApiMark.Vhdl.Tests;

/// <summary>Helper that resolves fixture file paths for VHDL tests.</summary>
internal static class FixturePaths
{
    /// <summary>Absolute path to the fixtures directory resolved at compile time.</summary>
    private static readonly string FixturesDir = ResolveFixturesDir();

    /// <summary>Gets the absolute path to the Fixtures directory.</summary>
    public static string FixturesDirectory => FixturesDir;

    /// <summary>Gets the absolute path to the mux.vhd fixture file.</summary>
    public static string MuxVhd => GetFixtureFilePath("mux.vhd");

    /// <summary>Gets the absolute path to the common_types.vhd fixture file.</summary>
    public static string CommonTypesVhd => GetFixtureFilePath("common_types.vhd");

    /// <summary>Returns the absolute path to a named fixture file.</summary>
    /// <param name="fileName">File name within the Fixtures directory.</param>
    /// <returns>Absolute path to the fixture file.</returns>
    public static string GetFixtureFilePath(string fileName) =>
        Path.Join(FixturesDir, fileName);

    /// <summary>
    ///     Resolves the fixtures directory at compile time using <see cref="CallerFilePathAttribute"/>.
    /// </summary>
    private static string ResolveFixturesDir([CallerFilePath] string? sourceFile = null)
    {
        var testDir = Path.GetDirectoryName(sourceFile)!;
        return Path.GetFullPath(Path.Join(testDir, "Fixtures"));
    }
}
