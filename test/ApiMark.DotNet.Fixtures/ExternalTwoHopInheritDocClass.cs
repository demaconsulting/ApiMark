using ApiMark.DotNet.Fixtures.External;

namespace ApiMark.DotNet.Fixtures;

/// <summary>
///     A sample class overriding a member of an external "mid-tier" base class, whose own
///     override of that member is itself a bare <c>&lt;inheritdoc/&gt;</c> pointing further up an
///     external hierarchy — used to verify that ApiMark.DotNet resolves bare
///     <c>&lt;inheritdoc/&gt;</c> across <em>two</em> external hops rather than stopping at the
///     first externally-resolved candidate.
/// </summary>
public class ExternalTwoHopInheritDocClass : ExternalMidBaseClass
{
    /// <inheritdoc/>
    public override string DescribeGrand() => "two-hop-derived";
}
