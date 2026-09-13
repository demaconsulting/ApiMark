namespace ApiMark.DotNet.Fixtures.External;

/// <summary>
///     A "grandparent" base class in an external ("NuGet-like") assembly, used together with
///     <see cref="ExternalMidBaseClass"/> to verify that ApiMark.DotNet resolves bare
///     <c>&lt;inheritdoc/&gt;</c> elements across <em>two</em> external hops: a locally-defined
///     override whose immediate external base member is itself an undocumented bare
///     <c>&lt;inheritdoc/&gt;</c> override of a member defined further up the external hierarchy.
/// </summary>
public class ExternalGrandBaseClass
{
    /// <summary>Describes the grandparent implementation.</summary>
    /// <returns>The string <c>"grand"</c>.</returns>
    public virtual string DescribeGrand() => "grand";
}
