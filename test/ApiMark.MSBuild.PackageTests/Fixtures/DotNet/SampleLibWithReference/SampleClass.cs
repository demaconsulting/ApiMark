namespace SampleLib;

/// <summary>
///     A sample class used as a fixture for ApiMark.MSBuild package integration tests, referencing
///     a companion project (ReferencedLib) so that <c>@(ReferencePath)</c> is populated after
///     restore, exercising the <c>ApiMarkReferencePaths</c> auto-harvest logic in the
///     <c>.targets</c> file.
/// </summary>
public class SampleClass
{
    /// <summary>Gets the name of this sample instance.</summary>
    public string Name { get; } = "sample";

    /// <summary>Builds a greeting using <see cref="Name"/> and the referenced project's greeting.</summary>
    /// <returns>A combined greeting string.</returns>
    public string BuildGreeting() => $"{new ReferencedLib.ReferencedClass().Greeting}, {Name}";
}

