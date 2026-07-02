namespace ApiMark.DotNet.Fixtures.Inner.RemarksOnly;

/// <remarks>
/// Remarks-only namespace fallback line one.
/// Remarks-only namespace fallback line two.
/// </remarks>
internal static class NamespaceDoc
{
}

/// <summary>A class ensuring the remarks-only namespace contains a documented type.</summary>
public class RemarksOnlyNamespaceClass
{
    /// <summary>Gets or sets a sample value.</summary>
    public int Value { get; set; }
}
