using ApiMark.DotNet.Fixtures;

namespace ApiMark.DotNet.Tests;

/// <summary>Helper that resolves fixture assembly paths for integration tests.</summary>
internal static class FixturePaths
{
    /// <summary>Returns the path to the fixtures DLL.</summary>
    /// <returns>Absolute path to the ApiMark.DotNet.Fixtures assembly.</returns>
    public static string GetFixtureDll() =>
        typeof(SampleClass).Assembly.Location;

    /// <summary>Returns the path to the fixtures XML documentation file.</summary>
    /// <returns>Absolute path to the XML documentation file produced alongside the fixtures assembly.</returns>
    public static string GetFixtureXmlDoc() =>
        Path.ChangeExtension(GetFixtureDll(), ".xml");
}
