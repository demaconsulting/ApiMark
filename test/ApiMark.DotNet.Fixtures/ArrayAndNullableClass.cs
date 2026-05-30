namespace ApiMark.DotNet.Fixtures;

/// <summary>A class for testing array, nullable, and generic return types.</summary>
public class ArrayAndNullableClass
{
    /// <summary>Gets an array of names.</summary>
    /// <returns>An array of name strings.</returns>
    public string[] GetNames() => Array.Empty<string>();

    /// <summary>Gets an optional count.</summary>
    /// <returns>The count, or null if unavailable.</returns>
    public int? GetCount() => null;

    /// <summary>Gets a list of strings.</summary>
    /// <returns>A list of strings.</returns>
    public List<string> GetList() => new();

    /// <summary>Gets a value asynchronously.</summary>
    /// <returns>A task that resolves to a boolean value.</returns>
    public Task<bool> GetAsync() => Task.FromResult(false);
}
