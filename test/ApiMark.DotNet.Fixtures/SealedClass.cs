namespace ApiMark.DotNet.Fixtures;

/// <summary>A sealed class for testing the sealed modifier in generated signatures.</summary>
public sealed class SealedClass
{
    /// <summary>Gets the value.</summary>
    public int Value { get; set; }

    /// <summary>Returns the string representation of this instance.</summary>
    /// <returns>A string describing the value.</returns>
    public override string ToString() => $"SealedClass({Value})";
}
