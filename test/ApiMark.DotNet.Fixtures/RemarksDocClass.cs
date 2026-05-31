namespace ApiMark.DotNet.Fixtures;

/// <summary>A class for testing multi-line remarks documentation.</summary>
public class RemarksDocClass
{
    /// <summary>Computes the result.</summary>
    /// <remarks>
    /// This method uses an iterative algorithm.
    /// Performance is O(n).
    /// </remarks>
    public int Compute() => 42;
}
