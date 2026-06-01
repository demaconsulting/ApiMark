namespace ApiMark.DotNet.Fixtures;

/// <summary>A class for testing array, nullable, and generic return types.</summary>
public static class ArrayAndNullableClass
{
    /// <summary>Gets an array of names.</summary>
    /// <returns>An array of name strings.</returns>
    public static string[] GetNames() => Array.Empty<string>();

    /// <summary>Gets an optional count.</summary>
    /// <returns>The count, or null if unavailable.</returns>
    public static int? GetCount() => null;

    /// <summary>Gets a list of strings.</summary>
    /// <returns>A list of strings.</returns>
    public static List<string> GetList() => new();

    /// <summary>Gets a value asynchronously.</summary>
    /// <returns>A task that resolves to a boolean value.</returns>
    public static Task<bool> GetAsync() => Task.FromResult(false);
}
