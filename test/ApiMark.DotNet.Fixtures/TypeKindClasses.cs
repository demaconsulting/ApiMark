// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>A readonly struct used to verify that the readonly modifier is shown.</summary>
public readonly struct ReadOnlyStructSample
{
    /// <summary>Gets the value.</summary>
    public int Value { get; }

    /// <summary>Initializes a new instance of the <see cref="ReadOnlyStructSample"/> struct.</summary>
    /// <param name="value">The value.</param>
    public ReadOnlyStructSample(int value)
    {
        Value = value;
    }
}

/// <summary>A ref struct used to verify that the ref modifier is shown.</summary>
public ref struct RefStructSample
{
    /// <summary>The length.</summary>
    public int Length;
}

/// <summary>A readonly ref struct used to verify that both modifiers are shown.</summary>
public readonly ref struct ReadOnlyRefStructSample
{
    /// <summary>Gets the length.</summary>
    public int Length { get; }
}

/// <summary>A record struct used to verify that the record struct keyword is shown.</summary>
/// <param name="X">The x coordinate.</param>
/// <param name="Y">The y coordinate.</param>
public record struct RecordStructSample(int X, int Y);

/// <summary>A readonly record struct used to verify that both keywords are shown.</summary>
/// <param name="X">The x coordinate.</param>
public readonly record struct ReadOnlyRecordStructSample(int X);

/// <summary>A generic record used to verify the implicit equality interface is hidden.</summary>
/// <typeparam name="T">The payload type.</typeparam>
/// <param name="Payload">The payload.</param>
public record GenericRecordSample<T>(T Payload);
