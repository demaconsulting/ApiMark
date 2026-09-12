namespace ReferencedLib;

/// <summary>
///     A minimal, dependency-free class used solely as a <c>ProjectReference</c> target for the
///     <c>SampleLibWithReference</c> fixture, so that <c>@(ReferencePath)</c> is populated after
///     restore/resolve without requiring any network-fetched NuGet package (see <c>SampleLib.cs</c>
///     for how this is consumed).
/// </summary>
public class ReferencedClass
{
    /// <summary>Gets a fixed greeting string.</summary>
    public string Greeting { get; } = "hello";
}
