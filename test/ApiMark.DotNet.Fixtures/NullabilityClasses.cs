// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>A reference type used in nullability fixtures.</summary>
public class NullWire
{
}

/// <summary>A reference type used as a nullable generic argument.</summary>
public class NullWireStyle
{
}

/// <summary>A value type used in nullability fixtures.</summary>
public struct NullPoint
{
    /// <summary>Gets or sets the X coordinate.</summary>
    public int X { get; set; }
}

/// <summary>A class with nullable annotations inside generic arguments, arrays, and value types.</summary>
public class NullabilityDiagramElement
{
    /// <summary>Gets a nullable function delegate whose result is nullable.</summary>
    public Func<NullWire, NullWireStyle?>? StyleFunc { get; set; }

    /// <summary>Gets a non-nullable function delegate whose result is nullable.</summary>
    public Func<NullWire, NullWireStyle?> NonNullStyleFunc { get; set; } = _ => null;

    /// <summary>Gets a dictionary with a nullable value.</summary>
    public Dictionary<string, NullWireStyle?> Styles { get; set; } = new();

    /// <summary>Gets a nullable dictionary with a nullable value and a value-type argument.</summary>
    public Dictionary<string, List<int>?>? Nested { get; set; }

    /// <summary>Gets an array of nullable references.</summary>
    public string?[] NullableElements { get; set; } = Array.Empty<string?>();

    /// <summary>Gets a nullable array of non-nullable references.</summary>
    public string[]? NullableArray { get; set; }

    /// <summary>Gets a nullable array of nullable references.</summary>
    public string?[]? NullableArrayOfNullable { get; set; }

    /// <summary>Gets a nullable value type.</summary>
    public NullPoint? OptionalPoint { get; set; }

    /// <summary>Gets a list of nullable value types.</summary>
    public List<NullPoint?> Points { get; set; } = new();

    /// <summary>Gets a non-nullable string.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets a nullable string.</summary>
    public string? Subtitle { get; set; }

    /// <summary>Applies a style and returns a nullable function.</summary>
    /// <param name="wire">The wire.</param>
    /// <param name="fallback">An optional nullable dictionary.</param>
    /// <returns>The style function.</returns>
    public Func<NullWire, NullWireStyle?>? Style(NullWire wire, Dictionary<string, NullWire?>? fallback)
    {
        return wire == null || fallback == null ? null : StyleFunc;
    }

    /// <summary>Pastes a nullable point asynchronously.</summary>
    /// <param name="p">The point to paste.</param>
    /// <returns>A task that completes when pasting is done.</returns>
    public Task PasteAsync(NullPoint? p) => p == null ? Task.CompletedTask : Task.CompletedTask;

    /// <summary>Tries to find a style.</summary>
    /// <param name="name">The style name.</param>
    /// <param name="style">The found style, or null.</param>
    /// <returns>True when found.</returns>
    public bool TryGetStyle(string name, out NullWireStyle? style)
    {
        style = name.Length > 0 ? null : null;
        return false;
    }

    /// <summary>Occurs when the style changes.</summary>
    public event EventHandler<NullWireStyle?>? StyleChanged;

    /// <summary>A nullable field.</summary>
    public List<string?>? Cache;

    /// <summary>Raises the change event.</summary>
    protected void OnStyleChanged() => StyleChanged?.Invoke(this, null);
}

/// <summary>A generic class whose type parameters appear in nullable positions.</summary>
/// <typeparam name="T">An unconstrained type parameter.</typeparam>
public class NullabilityGenericElement<T>
{
    /// <summary>Gets a nullable value of the type parameter.</summary>
    public T? Value { get; set; }

    /// <summary>Gets a list of the type parameter.</summary>
    public List<T> Items { get; set; } = new();

    /// <summary>Gets a nullable list of nullable items.</summary>
    public List<T?>? MaybeItems { get; set; }
}

/// <summary>A class that mixes nullable and non-nullable members so the compiler picks context 2.</summary>
public class NullabilityMostlyNullableElement
{
    /// <summary>Gets a nullable first name.</summary>
    public string? First { get; set; }

    /// <summary>Gets a nullable second name.</summary>
    public string? Second { get; set; }

    /// <summary>Gets a non-nullable third name.</summary>
    public string Third { get; set; } = string.Empty;

    /// <summary>Combines two nullable values.</summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <param name="c">The third value.</param>
    /// <returns>A nullable result.</returns>
    public string? Combine(string? a, string? b, string c) => a ?? b ?? c;

    /// <summary>Echoes nullable values.</summary>
    /// <param name="values">The values.</param>
    /// <returns>A nullable list.</returns>
    public List<string?>? Echo(List<string?>? values) => values;
}

#nullable disable

/// <summary>A class compiled with nullable annotations disabled (oblivious).</summary>
public class NullabilityObliviousElement
{
    /// <summary>Gets an oblivious string.</summary>
    public string Name { get; set; }

    /// <summary>Gets an oblivious list.</summary>
    public List<string> Names { get; set; }

    /// <summary>Echoes an oblivious value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value.</returns>
    public string Echo(string value) => value;
}

#nullable restore
