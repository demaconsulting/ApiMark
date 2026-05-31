namespace ApiMark.DotNet.Fixtures;

/// <summary>A sample generic class for testing generic type handling.</summary>
/// <typeparam name="T">The type of the value.</typeparam>
public class SampleGenericClass<T>
{
    /// <summary>Gets or sets the value.</summary>
    public T Value { get; set; } = default!;

    /// <summary>Gets all values as an enumerable sequence.</summary>
    /// <returns>An enumerable of values.</returns>
    public IEnumerable<T> GetValues() => new[] { Value };
}
