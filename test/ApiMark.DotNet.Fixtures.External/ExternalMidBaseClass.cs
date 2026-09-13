namespace ApiMark.DotNet.Fixtures.External;

/// <summary>
///     A "mid-tier" base class in an external ("NuGet-like") assembly that overrides
///     <see cref="ExternalGrandBaseClass.DescribeGrand"/> with a bare <c>&lt;inheritdoc/&gt;</c>
///     and no documentation of its own. A member in the assembly being documented that overrides
///     <see cref="DescribeGrand"/> in turn must chase <em>two</em> external hops (through this
///     class and up to <see cref="ExternalGrandBaseClass"/>) to resolve real documentation text.
/// </summary>
public class ExternalMidBaseClass : ExternalGrandBaseClass
{
    /// <inheritdoc />
    public override string DescribeGrand() => "mid";
}
