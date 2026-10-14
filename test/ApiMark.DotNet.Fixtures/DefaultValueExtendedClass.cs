// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ApiMark.DotNet.Fixtures;

/// <summary>A class with additional optional-parameter default value shapes and a <c>notnull</c> constraint.</summary>
public class DefaultValueExtendedClass
{
    /// <summary>Accepts a date-time default carried by <c>DateTimeConstant</c>.</summary>
    /// <param name="when">The point in time.</param>
    /// <returns>The point in time.</returns>
    public DateTime When([Optional, DateTimeConstant(630822816000000000L)] DateTime when) => when;

    /// <summary>Accepts an enum default that has no matching named member.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The mode.</returns>
    public DefaultValueMode Unnamed(DefaultValueMode mode = (DefaultValueMode)5) => mode;

    /// <summary>Accepts a nullable enum default.</summary>
    /// <param name="mode">The optional mode.</param>
    /// <returns>The mode.</returns>
    public DefaultValueMode? NullableMode(DefaultValueMode? mode = DefaultValueMode.Slow) => mode;

    /// <summary>Accepts double special-value defaults.</summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <param name="c">The third value.</param>
    /// <returns>The sum.</returns>
    public double Special(
        double a = double.NaN,
        double b = double.PositiveInfinity,
        double c = double.NegativeInfinity) => a + b + c;

    /// <summary>Accepts float special-value defaults.</summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <param name="c">The third value.</param>
    /// <returns>The sum.</returns>
    public float SpecialFloat(
        float a = float.NaN,
        float b = float.PositiveInfinity,
        float c = float.NegativeInfinity) => a + b + c;

    /// <summary>Accepts a negative decimal default.</summary>
    /// <param name="rate">The rate.</param>
    /// <returns>The rate.</returns>
    public decimal Negative(decimal rate = -0.25m) => rate;

    /// <summary>Keeps a non-null value.</summary>
    /// <typeparam name="T">The value type, constrained to non-null.</typeparam>
    /// <param name="value">The value.</param>
    public void Keep<T>(T value)
        where T : notnull
    {
    }

    /// <summary>Creates a non-null value.</summary>
    /// <typeparam name="T">The value type, constrained to non-null with a default constructor.</typeparam>
    /// <returns>A new instance.</returns>
    public T Make<T>()
        where T : notnull, new() => new();

    /// <summary>Keeps a possibly-null value.</summary>
    /// <typeparam name="T">An unconstrained type.</typeparam>
    /// <param name="value">The value.</param>
    public void Allow<T>(T value)
    {
    }
}
