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
}
