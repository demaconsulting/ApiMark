namespace ApiMark.DotNet.Fixtures;

/// <summary>A class for testing protected member visibility filtering.</summary>
public class ProtectedMembersClass
{
    /// <summary>Gets or sets the public property.</summary>
    public string PublicProp { get; set; } = string.Empty;

    /// <summary>Gets or sets the protected property.</summary>
    protected string ProtectedProp { get; set; } = string.Empty;

    /// <summary>Executes a protected operation with the specified value.</summary>
    /// <param name="x">The integer value to process.</param>
    protected virtual void ProtectedMethod(int x) { _ = x; }

    /// <summary>Executes a private operation with the specified value.</summary>
    /// <param name="x">The integer value to process.</param>
    private static void PrivateMethod(int x) { _ = x; }
}
