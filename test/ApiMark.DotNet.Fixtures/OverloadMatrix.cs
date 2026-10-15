namespace ApiMark.DotNet.Fixtures;

/// <summary>
///     A non-generic type whose overload sets cover the shapes that previously confused the
///     overload index and the per-overload example attribution: a non-generic and a generic
///     overload with the same name, overloads with differing return types, overloads with the
///     same return type, and a same-arity generic/non-generic pair.
/// </summary>
public class OverloadMatrix
{
    /// <summary>Runs the supplied non-generic action and returns its result.</summary>
    /// <param name="action">The action to run.</param>
    /// <returns>The result of the action.</returns>
    public int Transact(Func<int> action) => action();

    /// <summary>Runs the supplied generic action and returns its result.</summary>
    /// <typeparam name="T">The result type of the action.</typeparam>
    /// <param name="action">The action to run.</param>
    /// <returns>The result of the action.</returns>
    /// <example>
    /// <code>
    /// var text = matrix.Transact(() => "generic");
    /// </code>
    /// </example>
    public T Transact<T>(Func<T> action) => action();

    /// <summary>Converts a boolean to a decimal.</summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The converted value.</returns>
    public decimal ToAvalonia(bool value) => value ? 1m : 0m;

    /// <summary>Converts a double to a float.</summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The converted value.</returns>
    public float ToAvalonia(double value) => (float)value;

    /// <summary>Converts an integer to a long.</summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The converted value.</returns>
    public long ToAvalonia(int value) => value;

    /// <summary>Converts a string to a character array.</summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The converted value.</returns>
    public char[] ToAvalonia(string value) => value.ToCharArray();

    /// <summary>Scales an integer.</summary>
    /// <param name="value">The value to scale.</param>
    /// <returns>The scaled value.</returns>
    public double Scale(int value) => value * 2.0;

    /// <summary>Scales a double.</summary>
    /// <param name="value">The value to scale.</param>
    /// <returns>The scaled value.</returns>
    public double Scale(double value) => value * 2.0;

    /// <summary>Picks the integer.</summary>
    /// <param name="value">The value to pick.</param>
    /// <returns>The picked value.</returns>
    public int Pick(int value) => value;

    /// <summary>Picks the generic value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to pick.</param>
    /// <returns>The picked value.</returns>
    /// <example>
    /// Pass any value and the same value comes back.
    /// </example>
    public T Pick<T>(T value) => value;
}

/// <summary>
///     A generic type with the same overload shapes as <see cref="OverloadMatrix"/>, so the
///     member-ID of a generic method inside a generic type is exercised.
/// </summary>
/// <typeparam name="TItem">The item type held by the matrix.</typeparam>
public class OverloadMatrix<TItem>
{
    /// <summary>Runs the supplied item action.</summary>
    /// <param name="action">The action to run.</param>
    /// <returns>The item the action produced.</returns>
    public TItem Transact(Func<TItem> action) => action();

    /// <summary>Runs the supplied generic action.</summary>
    /// <typeparam name="T">The result type of the action.</typeparam>
    /// <param name="action">The action to run.</param>
    /// <returns>The result of the action.</returns>
    /// <example>
    /// <code>
    /// var count = matrix.Transact(() => 3);
    /// </code>
    /// </example>
    public T Transact<T>(Func<T> action) => action();
}
