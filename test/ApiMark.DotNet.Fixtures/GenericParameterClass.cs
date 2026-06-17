namespace ApiMark.DotNet.Fixtures;

/// <summary>A class with methods that have generic type parameters in their signatures.</summary>
public class GenericParameterClass
{
    /// <summary>Processes a sequence of names and a metadata dictionary.</summary>
    /// <param name="names">The names to process.</param>
    /// <param name="metadata">Key-value metadata for the operation.</param>
    /// <returns>The number of items processed.</returns>
    public int Process(IEnumerable<string> names, IReadOnlyDictionary<string, object> metadata) =>
        names.Count();

    /// <summary>Applies an action to a configured value.</summary>
    /// <param name="configure">The configuration action.</param>
    public void Configure(Action<string> configure) => configure("value");
}
