using ApiMark.DotNet.Fixtures.External;

namespace ApiMark.DotNet.Fixtures;

/// <summary>
///     A generic type whose base class, interface, constraint, members and events all mention types
///     from the external fixture assembly, used to verify short C# names in signature blocks.
/// </summary>
/// <typeparam name="T">A type constrained to an external base class.</typeparam>
public class ExternalSignatureShapes<T> : ExternalBaseClass, IExternalBaseInterface
    where T : ExternalBaseClass, new()
{
    /// <summary>Raised when the external value changes.</summary>
    public event EventHandler<ExternalGenericFormat<string>>? Changed;

    /// <summary>Gets or sets an external-typed property.</summary>
    public ExternalBaseClass? Partner { get; set; }

    /// <summary>An external-typed field.</summary>
    public ExternalGenericFormat<ExternalBaseClass>? Format;

    /// <inheritdoc/>
    public void ExternalInterfaceMethod() => Changed?.Invoke(this, new ExternalGenericFormat<string>());

    /// <summary>Echoes the arguments.</summary>
    /// <param name="value">The base value.</param>
    /// <param name="format">The format.</param>
    /// <typeparam name="TItem">An item type constrained to the external interface.</typeparam>
    /// <returns>The value.</returns>
    public ExternalBaseClass? Echo<TItem>(ExternalBaseClass? value, ExternalGenericFormat<TItem> format)
        where TItem : IExternalBaseInterface => value;
}
