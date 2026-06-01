namespace ApiMark.DotNet.Fixtures;

/// <summary>A class for testing multi-line remarks documentation.</summary>
public class RemarksDocClass
{
    /// <summary>Computes the result.</summary>
    /// <remarks>
    /// This method uses an iterative algorithm.
    /// Performance is O(n).
    /// </remarks>
#pragma warning disable S3400 // Method returns a constant — intentional for fixture/test purposes
    public static int Compute() => 42;
#pragma warning restore S3400
}
