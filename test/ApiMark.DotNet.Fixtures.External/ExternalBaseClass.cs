namespace ApiMark.DotNet.Fixtures.External;

/// <summary>
///     A base class in an external ("NuGet-like") assembly, used to verify that ApiMark.DotNet
///     resolves bare <c>&lt;inheritdoc/&gt;</c> elements against base members defined outside the
///     assembly currently being documented.
/// </summary>
public class ExternalBaseClass
{
    /// <summary>Describes the base implementation.</summary>
    /// <returns>The string <c>"base"</c>.</returns>
    public virtual string DescribeBase() => "base";
}
