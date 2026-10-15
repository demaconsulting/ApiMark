namespace ApiMark.DotNet.Fixtures;

/// <summary>A two-dimensional point used as one of the distinct return types of the conversion overloads.</summary>
/// <param name="X">The horizontal coordinate.</param>
/// <param name="Y">The vertical coordinate.</param>
public readonly record struct Point(double X, double Y);

/// <summary>A two-dimensional size used as one of the distinct return types of the conversion overloads.</summary>
/// <param name="Width">The horizontal extent.</param>
/// <param name="Height">The vertical extent.</param>
public readonly record struct Size(double Width, double Height);

/// <summary>A rectangle used as one of the distinct return types of the conversion overloads.</summary>
/// <param name="X">The left coordinate.</param>
/// <param name="Y">The top coordinate.</param>
/// <param name="Width">The horizontal extent.</param>
/// <param name="Height">The vertical extent.</param>
public readonly record struct Rect(double X, double Y, double Width, double Height);

/// <summary>A color used as one of the distinct return types of the conversion overloads.</summary>
/// <param name="Value">The packed color value.</param>
public readonly record struct Color(int Value);

/// <summary>
///     Extension methods whose overloads each return a different type and carry a different
///     summary, so the overload index cannot describe the whole group with one overload's text.
/// </summary>
public static class ConversionExtensions
{
    /// <summary>Converts an integer to a point.</summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The converted point.</returns>
    public static Point ToAvalonia(this int value) => new(value, value);

    /// <summary>Converts a long integer to a size.</summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The converted size.</returns>
    public static Size ToAvalonia(this long value) => new(value, value);

    /// <summary>Converts a float to a rectangle.</summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The converted rectangle.</returns>
    public static Rect ToAvalonia(this float value) => new(value, value, value, value);

    /// <summary>Converts a byte to a color.</summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The converted color.</returns>
    public static Color ToAvalonia(this byte value) => new(value);
}

/// <summary>
///     A history type with a void overload and a generic overload of the same name, where only the
///     generic overload documents an example, plus cross-references to specific overloads.
/// </summary>
public class UndoHistory
{
    /// <summary>Runs <paramref name="action"/> as one undoable step named <paramref name="name"/>.</summary>
    /// <param name="name">The step name; may be <see langword="null"/> for an anonymous step.</param>
    /// <param name="action">The action to run.</param>
    public void Transact(string name, Action action) => action();

    /// <summary>
    ///     Runs <paramref name="action"/> as one undoable step and returns its result; use
    ///     <see cref="Transact(string, Action)"/> when no result is needed.
    /// </summary>
    /// <typeparam name="T">The result type; <typeparamref name="T"/> may be a reference type.</typeparam>
    /// <param name="name">The step name; may be <see langword="null"/> for an anonymous step.</param>
    /// <param name="action">The action producing the result.</param>
    /// <returns>The action result, or <see langword="null"/> when <paramref name="action"/> returns nothing.</returns>
    /// <example>
    /// <code>
    /// var count = history.Transact("count", () => 3);
    /// </code>
    /// </example>
    public T Transact<T>(string name, Func<T> action) => action();

    /// <summary>Clears the history.</summary>
    public void Clear()
    {
    }

    /// <summary>Clears the history.</summary>
    /// <param name="depth">The number of steps to keep.</param>
    public void Clear(int depth)
    {
        _ = depth;
    }

    /// <summary>
    ///     Converts using <see cref="ConversionExtensions.ToAvalonia(int)"/> and then calls
    ///     <see cref="Transact{T}(string, Func{T})"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The converted point.</returns>
    public Point Convert(int value) => Transact("convert", () => value.ToAvalonia());
}

/// <summary>A type whose member documents every prose location with parameter, type-parameter, and keyword references.</summary>
public class InlineCodeDocs
{
    /// <summary>Formats <paramref name="value"/> of type <typeparamref name="T"/>, or <see langword="null"/>.</summary>
    /// <typeparam name="T">The value type; <paramref name="value"/> has type <typeparamref name="T"/>.</typeparam>
    /// <param name="name">The label; <see langword="null"/> is not allowed.</param>
    /// <param name="value">The value of type <typeparamref name="T"/> for <paramref name="name"/>.</param>
    /// <returns>The text, or <see langword="null"/> when <paramref name="value"/> is empty.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <remarks>Passing <see langword="null"/> for <paramref name="name"/> throws.</remarks>
    /// <example>Call with <paramref name="name"/> set to <see langword="null"/> to see the exception.</example>
    public string? Format<T>(string name, T value) => name == null ? throw new ArgumentNullException(nameof(name)) : value?.ToString();
}
