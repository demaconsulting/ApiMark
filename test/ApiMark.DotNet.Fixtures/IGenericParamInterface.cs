namespace ApiMark.DotNet.Fixtures;

/// <summary>An interface with methods that have generic parameter types.</summary>
public interface IGenericParamInterface
{
    /// <summary>Processes a sequence of items.</summary>
    /// <param name="items">The items to process.</param>
    /// <returns>The number of items processed.</returns>
    int Process(IEnumerable<string> items);

    /// <summary>Transforms data using the provided callback.</summary>
    /// <param name="data">The data dictionary.</param>
    /// <param name="callback">The transformation callback.</param>
    void Transform(IReadOnlyDictionary<string, object> data, Action<string> callback);
}
