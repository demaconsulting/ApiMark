// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>A struct with operator overloads for testing operator page generation.</summary>
public readonly struct OperatorsStruct
{
    /// <summary>Gets the scalar value.</summary>
    public double Value { get; }

    /// <summary>Initializes a new instance with the specified value.</summary>
    /// <param name="value">The scalar value.</param>
    public OperatorsStruct(double value)
    {
        Value = value;
    }

    /// <summary>Adds two instances.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The sum of the two instances.</returns>
    public static OperatorsStruct operator +(OperatorsStruct left, OperatorsStruct right) =>
        new(left.Value + right.Value);

    /// <summary>Subtracts one instance from another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The difference of the two instances.</returns>
    public static OperatorsStruct operator -(OperatorsStruct left, OperatorsStruct right) =>
        new(left.Value - right.Value);

    /// <summary>Implicitly converts an instance to a double.</summary>
    /// <param name="value">The instance to convert.</param>
    /// <returns>The scalar value as a double.</returns>
    public static implicit operator double(OperatorsStruct value) => value.Value;

    /// <summary>Explicitly converts an instance to an integer.</summary>
    /// <param name="value">The instance to convert.</param>
    /// <returns>The scalar value truncated to an integer.</returns>
    public static explicit operator int(OperatorsStruct value) => (int)value.Value;

    /// <summary>Represents a wrapped scalar value.</summary>
    public readonly struct Wrapped { }

    /// <summary>Wraps this instance as a Wrapped value.</summary>
    /// <param name="value">The value to wrap.</param>
    /// <returns>A wrapped representation of the value.</returns>
    public static implicit operator Wrapped(OperatorsStruct value) => new Wrapped();
}
