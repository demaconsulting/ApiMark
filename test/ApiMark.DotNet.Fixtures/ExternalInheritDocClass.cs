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

/// <summary>
///     A class with an explicit constructor that stores a protected field declared by an
///     external base class — used to verify that constructor-implicitness detection does not
///     throw when the field's declaring type cannot be resolved (e.g. because no
///     <c>ReferencePaths</c> were supplied for the external assembly), and instead treats the
///     store defensively as not belonging to this type.
/// </summary>
public class ExternalProtectedFieldConstructorClass : ExternalBaseClass
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ExternalProtectedFieldConstructorClass"/>
    ///     class, storing into the inherited protected <c>BaseCounter</c> field.
    /// </summary>
    public ExternalProtectedFieldConstructorClass() => BaseCounter = 1;
}
