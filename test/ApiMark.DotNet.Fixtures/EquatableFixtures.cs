// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>A hand-written class that explicitly implements <c>IEquatable</c> over itself.</summary>
public sealed class ExplicitEquatableClass : IEquatable<ExplicitEquatableClass>
{
    /// <summary>Determines whether another instance is equal to this one.</summary>
    /// <param name="other">The other instance.</param>
    /// <returns>True when equal.</returns>
    public bool Equals(ExplicitEquatableClass? other) => ReferenceEquals(this, other);
}

/// <summary>A record that explicitly implements an additional interface besides the implicit <c>IEquatable</c>.</summary>
/// <param name="Value">The value.</param>
public record RecordWithInterface(int Value) : IComparable
{
    /// <summary>Compares this record to another object.</summary>
    /// <param name="obj">The other object.</param>
    /// <returns>Zero.</returns>
    public int CompareTo(object? obj) => 0;
}
