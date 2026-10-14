// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace ApiMark.DotNet.Fixtures;

/// <summary>A value type used as a default parameter value.</summary>
public struct DefaultValuePoint
{
    /// <summary>Gets or sets the X coordinate.</summary>
    public int X { get; set; }
}

/// <summary>An enumeration used as a default parameter value.</summary>
public enum DefaultValueMode
{
    /// <summary>The fast mode.</summary>
    Fast = 0,

    /// <summary>The slow mode.</summary>
    Slow = 1,
}

/// <summary>A class with methods that declare optional parameters with default values.</summary>
public class DefaultValueClass
{
    /// <summary>Serializes the instance with a boolean default.</summary>
    /// <param name="indented">Whether to indent the output.</param>
    /// <returns>The JSON text.</returns>
    public string ToJson(bool indented = true) => indented ? "{ }" : "{}";

    /// <summary>Formats text with a string default.</summary>
    /// <param name="format">The format string.</param>
    /// <returns>The formatted text.</returns>
    public string Format(string format = "N0") => format;

    /// <summary>Accepts a null default.</summary>
    /// <param name="name">The optional name.</param>
    /// <returns>The name.</returns>
    public string Name(string name = null!) => name;

    /// <summary>Accepts a default struct value.</summary>
    /// <param name="point">The point.</param>
    /// <returns>The X coordinate.</returns>
    public int Origin(DefaultValuePoint point = default) => point.X;

    /// <summary>Accepts an enum default.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The mode.</returns>
    public DefaultValueMode Mode(DefaultValueMode mode = DefaultValueMode.Slow) => mode;

    /// <summary>Accepts an optional parameter without a default value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value.</returns>
    public int Optional([Optional] int value) => value;

    /// <summary>Accepts a parameter with an explicit default-parameter-value attribute.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value.</returns>
    public int Explicit([Optional, DefaultParameterValue(7)] int value) => value;

    /// <summary>Accepts a decimal default.</summary>
    /// <param name="rate">The rate.</param>
    /// <returns>The rate.</returns>
    public decimal Rate(decimal rate = 1.5m) => rate;

    /// <summary>Accepts a nullable integer default.</summary>
    /// <param name="count">The count.</param>
    /// <returns>The count.</returns>
    public int? Count(int? count = null) => count;

    /// <summary>Accepts a character default and a long default.</summary>
    /// <param name="separator">The separator.</param>
    /// <param name="limit">The limit.</param>
    /// <returns>The separator.</returns>
    public char Separate(char separator = ',', long limit = 42L) => limit > 0 ? separator : ' ';
}
