namespace ApiMark.DotNet.Fixtures;

/// <summary>Fixture reference type used as the target of <c>ref</c>/<c>out</c>/<c>in</c> parameters in <see cref="ByRefParameterClass"/>.</summary>
public class ByRefTargetClass
{
    /// <summary>Gets or sets a sample value.</summary>
    public int Value { get; set; }
}

/// <summary>Fixture class for testing <c>ref</c>/<c>out</c>/<c>in</c> parameter rendering in signatures and links.</summary>
/// <remarks>
///     Used to verify that byref parameters render the correct C# keyword (<c>out</c>/<c>in</c>/<c>ref</c>)
///     and that the underlying type name — not Cecil's byref-suffixed name — is used for both the
///     displayed type and any generated documentation link.
/// </remarks>
public class ByRefParameterClass
{
    /// <summary>Attempts to resolve a value by name.</summary>
    /// <param name="name">The name to resolve.</param>
    /// <param name="value">The resolved value.</param>
    /// <returns><c>true</c> if resolved; otherwise, <c>false</c>.</returns>
    public bool TryResolve(string name, out ByRefTargetClass value)
    {
        value = new ByRefTargetClass();
        return name.Length > 0;
    }

    /// <summary>Increments the given value in place.</summary>
    /// <param name="value">The value to increment.</param>
    public void Increment(ref ByRefTargetClass value)
    {
        _ = value;
    }

    /// <summary>Inspects the given value without modifying it.</summary>
    /// <param name="value">The value to inspect.</param>
    public void Inspect(in ByRefTargetClass value)
    {
        _ = value;
    }
}
