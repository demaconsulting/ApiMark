using ApiMark.DotNet.Fixtures.External;

namespace ApiMark.DotNet.Fixtures;

/// <summary>
///     A sample class deriving from an external base class and implementing an external
///     interface, both defined in the <c>ApiMark.DotNet.Fixtures.External</c> assembly, using
///     bare <c>&lt;inheritdoc/&gt;</c> to verify cross-assembly inheritdoc resolution.
/// </summary>
public class ExternalInheritDocClass : ExternalBaseClass, IExternalBaseInterface
{
    /// <inheritdoc/>
    public override string DescribeBase() => "derived";

    /// <inheritdoc/>
    public void ExternalInterfaceMethod() { }
}
