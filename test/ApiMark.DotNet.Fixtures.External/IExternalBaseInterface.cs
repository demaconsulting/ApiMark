namespace ApiMark.DotNet.Fixtures.External;

/// <summary>
///     An interface in an external ("NuGet-like") assembly, used to verify that ApiMark.DotNet
///     resolves bare <c>&lt;inheritdoc/&gt;</c> elements against interface members defined outside
///     the assembly currently being documented.
/// </summary>
public interface IExternalBaseInterface
{
    /// <summary>Performs the external interface method's action.</summary>
    void ExternalInterfaceMethod();
}
