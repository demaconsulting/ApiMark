namespace ApiMark.DotNet.Fixtures;

/// <summary>A class that reuses generic-param method documentation via cref inheritdoc.</summary>
public class CrefGenericDocClass
{
    /// <inheritdoc cref="IGenericParamInterface.Process(IEnumerable{string})" />
    public int RunProcessShortForm(IEnumerable<string> items) => items.Count();

    /// <inheritdoc cref="IGenericParamInterface.Process(System.Collections.Generic.IEnumerable{System.String})" />
    public int RunProcessFullyQualified(IEnumerable<string> items) => items.Count();

    /// <inheritdoc cref="IGenericParamInterface.Transform(IReadOnlyDictionary{string, object}, Action{string})" />
    public void RunTransformShortForm(IReadOnlyDictionary<string, object> data, Action<string> callback)
    {
        foreach (var key in data.Keys)
        {
            callback(key);
        }
    }

    /// <inheritdoc cref="IGenericParamInterface.Transform(System.Collections.Generic.IReadOnlyDictionary{System.String,System.Object}, System.Action{System.String})" />
    public void RunTransformFullyQualified(IReadOnlyDictionary<string, object> data, Action<string> callback)
    {
        foreach (var key in data.Keys)
        {
            callback(key);
        }
    }
}
