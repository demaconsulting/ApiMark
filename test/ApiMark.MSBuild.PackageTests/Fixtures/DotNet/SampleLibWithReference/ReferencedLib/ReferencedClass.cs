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

    /// <summary>
    ///     A distinctive sentinel description used solely to verify, end-to-end, that ApiMark's
    ///     cross-assembly &lt;inheritdoc/&gt; resolution actually consumes this project's XML
    ///     documentation file when referenced only via the MSBuild <c>.targets</c> file's
    ///     auto-harvested <c>ApiMarkReferencePaths</c> (see <c>SampleClass.Describable</c>).
    /// </summary>
    /// <returns>The sentinel description text.</returns>
    public virtual string Describe() => "referenced-lib-description";
}
