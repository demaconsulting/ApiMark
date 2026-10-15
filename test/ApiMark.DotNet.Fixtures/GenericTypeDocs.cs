namespace ApiMark.DotNet.Fixtures;

/// <summary>A registry of property editors keyed by name.</summary>
/// <typeparam name="TEditor">The editor type stored in the registry.</typeparam>
public class PropertyEditorRegistry<TEditor>
    where TEditor : class
{
    /// <summary>Registers an editor under the given name.</summary>
    /// <param name="name">The registration name.</param>
    /// <param name="editor">The editor to register.</param>
    public void Register(string name, TEditor editor)
    {
        _ = name;
        _ = editor;
    }

    /// <summary>Converts an editor into a value of the requested type.</summary>
    /// <typeparam name="TResult">The value type to produce.</typeparam>
    /// <param name="editor">The editor to convert.</param>
    /// <returns>The converted value.</returns>
    public TResult Convert<TResult>(TEditor editor)
        where TResult : struct
    {
        _ = editor;
        return default;
    }
}

#pragma warning disable CS1712 // Intentionally leaves TValue undocumented to exercise the placeholder row.

/// <summary>A pair of related values; only the key parameter is documented.</summary>
/// <typeparam name="TKey">The key type.</typeparam>
public class KeyedPair<TKey, TValue>
    where TKey : IComparable<TKey>
    where TValue : new()
{
    /// <summary>Gets the key.</summary>
    public TKey Key { get; } = default!;

    /// <summary>Gets the value.</summary>
    public TValue Value { get; } = new TValue();
}

#pragma warning restore CS1712

/// <summary>A generic container that declares a non-generic nested type.</summary>
/// <typeparam name="THost">The host item type.</typeparam>
public class GenericHost<THost>
{
    /// <summary>Gets or sets the hosted item.</summary>
    public THost? Item { get; set; }

    /// <summary>A nested type that inherits the host's type parameter but declares none.</summary>
    public class Nested
    {
        /// <summary>Gets the nested label.</summary>
        public string Label { get; } = string.Empty;
    }
}
