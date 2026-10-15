// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>A positional record whose primary constructor receives the compiler-copied type summary.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Label">The label.</param>
public record PositionalRecord(int Id, string Label);

/// <summary>A positional record struct whose primary constructor receives the compiler-copied type summary.</summary>
/// <param name="X">The horizontal coordinate.</param>
/// <param name="Y">The vertical coordinate.</param>
public readonly record struct PositionalRecordStruct(int X, int Y);

/// <summary>A positional record with an additional hand-written constructor.</summary>
/// <param name="Name">The name.</param>
public record RecordWithExtraConstructor(string Name)
{
    /// <summary>Initializes a new instance of the record from a numeric id.</summary>
    /// <param name="id">The numeric id.</param>
    public RecordWithExtraConstructor(int id)
        : this(id.ToString())
    {
    }
}

/// <summary>A record class declaring its own constructor documentation.</summary>
public record RecordWithExplicitConstructor
{
    /// <summary>Creates the record with an explicit, differing constructor summary.</summary>
    /// <param name="value">The value.</param>
    public RecordWithExplicitConstructor(int value)
    {
        Value = value;
    }

    /// <summary>Gets the value.</summary>
    public int Value { get; }
}
