namespace SampleLib;

/// <summary>
///     A sample class used as a fixture for ApiMark.MSBuild package integration tests, referencing
///     a NuGet package (Newtonsoft.Json) so that <c>@(ReferencePath)</c> is populated after restore,
///     exercising the <c>ApiMarkReferencePaths</c> auto-harvest logic in the <c>.targets</c> file.
/// </summary>
public class SampleClass
{
    /// <summary>Gets the name of this sample instance.</summary>
    public string Name { get; } = "sample";

    /// <summary>Serializes <see cref="Name"/> to JSON using the referenced Newtonsoft.Json package.</summary>
    /// <returns>The JSON representation of <see cref="Name"/>.</returns>
    public string ToJson() => Newtonsoft.Json.JsonConvert.SerializeObject(Name);
}
