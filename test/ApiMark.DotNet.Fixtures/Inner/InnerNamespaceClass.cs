namespace ApiMark.DotNet.Fixtures.Inner;

/// <summary>A class in a child namespace used to verify hierarchical namespace output.</summary>
public class InnerNamespaceClass
{
    /// <summary>Gets or sets a value.</summary>
    public int Value { get; set; }

    /// <summary>Computes a result from the given input.</summary>
    /// <param name="input">The input value.</param>
    /// <returns>The computed result.</returns>
    public int Compute(int input) => input + Value;
}
