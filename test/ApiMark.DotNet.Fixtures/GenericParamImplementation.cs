namespace ApiMark.DotNet.Fixtures;

/// <summary>An implementation that inherits documentation for methods with generic parameter types.</summary>
public class GenericParamImplementation : IGenericParamInterface
{
    /// <inheritdoc />
    public int Process(IEnumerable<string> items) => items.Count();

    /// <inheritdoc />
    public void Transform(IReadOnlyDictionary<string, object> data, Action<string> callback)
    {
        foreach (var key in data.Keys)
        {
            callback(key);
        }
    }
}
