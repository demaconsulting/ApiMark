namespace ApiMark.DotNet.Fixtures;

/// <summary>A fixture class for testing bullet list rendering in XML documentation.</summary>
/// <remarks>
/// The processing pipeline has these stages:
/// <list type="bullet">
/// <item><description>Parse the input source.</description></item>
/// <item><description>Transform the intermediate model.</description></item>
/// <item><description>Emit the Markdown output.</description></item>
/// </list>
/// </remarks>
public static class BulletListDocClass
{
}

/// <summary>A fixture class for testing numbered list rendering in XML documentation.</summary>
/// <remarks>
/// Follow these steps in order:
/// <list type="number">
/// <item><description>Restore dependencies.</description></item>
/// <item><description>Build the solution.</description></item>
/// <item><description>Run the tests.</description></item>
/// </list>
/// </remarks>
public static class NumberListDocClass
{
}

/// <summary>A fixture class for testing table list rendering in XML documentation.</summary>
/// <remarks>
/// Supported output formats:
/// <list type="table">
/// <listheader>
/// <term>Format</term>
/// <description>Behavior</description>
/// </listheader>
/// <item>
/// <term>SingleFile</term>
/// <description>Writes all output to a single <c>api.md</c> file.</description>
/// </item>
/// <item>
/// <term>GradualDisclosure</term>
/// <description>Writes one page per namespace and type.</description>
/// </item>
/// </list>
/// </remarks>
public static class TableListDocClass
{
}
