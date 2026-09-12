using ApiMark.DotNet.Fixtures;
using ApiMark.DotNet.Fixtures.External;

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

    /// <summary>Returns the path to the external ("NuGet-like") base-library fixture DLL.</summary>
    /// <returns>Absolute path to the ApiMark.DotNet.Fixtures.External assembly.</returns>
    public static string GetExternalFixtureDll() =>
        typeof(ExternalBaseClass).Assembly.Location;

    /// <summary>Returns the path to the external fixture's XML documentation file.</summary>
    /// <returns>Absolute path to the XML documentation file produced alongside the external fixture assembly.</returns>
    public static string GetExternalFixtureXmlDoc() =>
        Path.ChangeExtension(GetExternalFixtureDll(), ".xml");
}
