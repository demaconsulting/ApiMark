// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable CS8981 // Lower-case type names are intentional: they test case-insensitive collisions.
#pragma warning disable CS0067 // Fixture-only shapes.
#pragma warning disable IDE1006 // Lower-case names are intentional.

namespace ApiMark.DotNet.Fixtures.LegalNames;

/// <summary>A generic interface with a method, property, indexer, and event for explicit implementations.</summary>
/// <typeparam name="T">The element type.</typeparam>
public interface IGenericSource<T>
{
    /// <summary>Gets an item.</summary>
    /// <returns>The item.</returns>
    T GetItem();

    /// <summary>Gets or sets the current value.</summary>
    T Current { get; set; }

    /// <summary>Gets an item by index.</summary>
    /// <param name="index">The index.</param>
    T this[int index] { get; }

    /// <summary>Raised when the source changes.</summary>
    event EventHandler Changed;
}

/// <summary>Explicitly implements several closed generic interfaces, including array and nested-generic arguments.</summary>
public class ExplicitGenericImpl :
    IGenericSource<int>,
    IGenericSource<string>,
    IGenericSource<int[]>,
    IGenericSource<List<Dictionary<string, int>>>,
    IGenericSource<GenericOuter<int>.GenericInner<string>>
{
    /// <inheritdoc/>
    int IGenericSource<int>.GetItem() => 0;

    /// <inheritdoc/>
    int IGenericSource<int>.Current { get; set; }

    /// <inheritdoc/>
    int IGenericSource<int>.this[int index] => index;

    /// <inheritdoc/>
    event EventHandler IGenericSource<int>.Changed { add { } remove { } }

    /// <inheritdoc/>
    string IGenericSource<string>.GetItem() => string.Empty;

    /// <inheritdoc/>
    string IGenericSource<string>.Current { get; set; } = string.Empty;

    /// <inheritdoc/>
    string IGenericSource<string>.this[int index] => string.Empty;

    /// <inheritdoc/>
    event EventHandler IGenericSource<string>.Changed { add { } remove { } }

    /// <inheritdoc/>
    int[] IGenericSource<int[]>.GetItem() => [];

    /// <inheritdoc/>
    int[] IGenericSource<int[]>.Current { get; set; } = [];

    /// <inheritdoc/>
    int[] IGenericSource<int[]>.this[int index] => [];

    /// <inheritdoc/>
    event EventHandler IGenericSource<int[]>.Changed { add { } remove { } }

    /// <inheritdoc/>
    List<Dictionary<string, int>> IGenericSource<List<Dictionary<string, int>>>.GetItem() => [];

    /// <inheritdoc/>
    List<Dictionary<string, int>> IGenericSource<List<Dictionary<string, int>>>.Current { get; set; } = [];

    /// <inheritdoc/>
    List<Dictionary<string, int>> IGenericSource<List<Dictionary<string, int>>>.this[int index] => [];

    /// <inheritdoc/>
    event EventHandler IGenericSource<List<Dictionary<string, int>>>.Changed { add { } remove { } }

    /// <inheritdoc/>
    GenericOuter<int>.GenericInner<string> IGenericSource<GenericOuter<int>.GenericInner<string>>.GetItem() => new();

    /// <inheritdoc/>
    GenericOuter<int>.GenericInner<string> IGenericSource<GenericOuter<int>.GenericInner<string>>.Current { get; set; } = new();

    /// <inheritdoc/>
    GenericOuter<int>.GenericInner<string> IGenericSource<GenericOuter<int>.GenericInner<string>>.this[int index] => new();

    /// <inheritdoc/>
    event EventHandler IGenericSource<GenericOuter<int>.GenericInner<string>>.Changed { add { } remove { } }
}

/// <summary>A generic outer type.</summary>
/// <typeparam name="T">The outer type parameter.</typeparam>
public class GenericOuter<T>
{
    /// <summary>A generic type nested in a generic type.</summary>
    /// <typeparam name="U">The inner type parameter.</typeparam>
    public class GenericInner<U>
    {
        /// <summary>Gets a pair.</summary>
        /// <returns>The pair.</returns>
        public KeyValuePair<T, U>? GetPair() => null;
    }
}

/// <summary>A struct with every operator kind that C# can overload.</summary>
public readonly struct AllOperatorsStruct
{
    /// <summary>Less than.</summary>
    /// <param name="a">Left.</param>
    /// <param name="b">Right.</param>
    /// <returns>The result.</returns>
    public static bool operator <(AllOperatorsStruct a, AllOperatorsStruct b) => false;

    /// <summary>Greater than.</summary>
    /// <param name="a">Left.</param>
    /// <param name="b">Right.</param>
    /// <returns>The result.</returns>
    public static bool operator >(AllOperatorsStruct a, AllOperatorsStruct b) => false;

    /// <summary>Less than or equal.</summary>
    /// <param name="a">Left.</param>
    /// <param name="b">Right.</param>
    /// <returns>The result.</returns>
    public static bool operator <=(AllOperatorsStruct a, AllOperatorsStruct b) => false;

    /// <summary>Greater than or equal.</summary>
    /// <param name="a">Left.</param>
    /// <param name="b">Right.</param>
    /// <returns>The result.</returns>
    public static bool operator >=(AllOperatorsStruct a, AllOperatorsStruct b) => false;

    /// <summary>Equality.</summary>
    /// <param name="a">Left.</param>
    /// <param name="b">Right.</param>
    /// <returns>The result.</returns>
    public static bool operator ==(AllOperatorsStruct a, AllOperatorsStruct b) => true;

    /// <summary>Inequality.</summary>
    /// <param name="a">Left.</param>
    /// <param name="b">Right.</param>
    /// <returns>The result.</returns>
    public static bool operator !=(AllOperatorsStruct a, AllOperatorsStruct b) => false;

    /// <summary>Logical not.</summary>
    /// <param name="a">The operand.</param>
    /// <returns>The result.</returns>
    public static bool operator !(AllOperatorsStruct a) => false;

    /// <summary>True operator.</summary>
    /// <param name="a">The operand.</param>
    /// <returns>The result.</returns>
    public static bool operator true(AllOperatorsStruct a) => true;

    /// <summary>False operator.</summary>
    /// <param name="a">The operand.</param>
    /// <returns>The result.</returns>
    public static bool operator false(AllOperatorsStruct a) => false;

    /// <summary>Bitwise and.</summary>
    /// <param name="a">Left.</param>
    /// <param name="b">Right.</param>
    /// <returns>The result.</returns>
    public static AllOperatorsStruct operator &(AllOperatorsStruct a, AllOperatorsStruct b) => a;

    /// <summary>Bitwise or.</summary>
    /// <param name="a">Left.</param>
    /// <param name="b">Right.</param>
    /// <returns>The result.</returns>
    public static AllOperatorsStruct operator |(AllOperatorsStruct a, AllOperatorsStruct b) => a;

    /// <summary>Division.</summary>
    /// <param name="a">Left.</param>
    /// <param name="b">Right.</param>
    /// <returns>The result.</returns>
    public static AllOperatorsStruct operator /(AllOperatorsStruct a, AllOperatorsStruct b) => a;

    /// <summary>Multiplication.</summary>
    /// <param name="a">Left.</param>
    /// <param name="b">Right.</param>
    /// <returns>The result.</returns>
    public static AllOperatorsStruct operator *(AllOperatorsStruct a, AllOperatorsStruct b) => a;

    /// <summary>Modulus.</summary>
    /// <param name="a">Left.</param>
    /// <param name="b">Right.</param>
    /// <returns>The result.</returns>
    public static AllOperatorsStruct operator %(AllOperatorsStruct a, AllOperatorsStruct b) => a;

    /// <summary>Implicit conversion to int.</summary>
    /// <param name="a">The operand.</param>
    public static implicit operator int(AllOperatorsStruct a) => 0;

    /// <summary>Explicit conversion to a generic list.</summary>
    /// <param name="a">The operand.</param>
    public static explicit operator List<int>(AllOperatorsStruct a) => [];

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is AllOperatorsStruct;

    /// <inheritdoc/>
    public override int GetHashCode() => 0;
}

/// <summary>A type whose name is a reserved Windows device name.</summary>
public class CON
{
    /// <summary>A field named like a device.</summary>
    public int Nul;

    /// <summary>A property named like a device.</summary>
    public int con { get; set; }

    /// <summary>A method named like a device.</summary>
    /// <returns>Zero.</returns>
    public int PRN() => 0;

    /// <summary>A method named like a device (overloaded).</summary>
    /// <param name="value">A value.</param>
    /// <returns>The value.</returns>
    public int PRN(int value) => value;
}

/// <summary>A second reserved-name type with mixed case.</summary>
public class Aux
{
    /// <summary>A member named like the reserved <c>operators</c> page.</summary>
    public int operators;

    /// <summary>A nested type named like the reserved <c>operators</c> page.</summary>
    public class Operators
    {
    }
}

/// <summary>A type used to test case-insensitive type name collisions.</summary>
public class CaseTwin
{
    /// <summary>A nested type.</summary>
    public class Inner
    {
    }

    /// <summary>A nested type differing only in case.</summary>
    public class inner
    {
    }
}

/// <summary>A type whose name differs from <see cref="CaseTwin"/> only in case.</summary>
public class casetwin
{
}
