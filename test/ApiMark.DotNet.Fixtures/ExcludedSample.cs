namespace ApiMark.DotNet.Fixtures.ExcludedSample;

/// <summary>
///     A class in an isolated namespace used to verify that an exclude pattern matching the
///     entire namespace removes the namespace (and all its types) from generated output,
///     including all indexes.
/// </summary>
public class ExcludedSampleClass
{
    /// <summary>Gets or sets a value.</summary>
    public int Value { get; set; }
}
