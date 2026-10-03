// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>
///     A class whose members trigger compiler-generated nested types (a cached-lambda class and
///     a closure/display class), used to verify ApiMark excludes them at every visibility.
/// </summary>
public class CompilerGeneratedNestedClass
{
    /// <summary>Doubles every value using a lambda that captures nothing, synthesizing a cached-lambda class.</summary>
    /// <param name="values">The values to double.</param>
    /// <returns>An array containing each value doubled.</returns>
    public int[] DoubleAll(int[] values) => Array.ConvertAll(values, v => v * 2);

    /// <summary>Adds <paramref name="offset"/> to every value using a lambda that captures it, synthesizing a closure class.</summary>
    /// <param name="values">The values to offset.</param>
    /// <param name="offset">The amount to add to each value.</param>
    /// <returns>An array containing each value plus <paramref name="offset"/>.</returns>
    public int[] AddOffsetToAll(int[] values, int offset) => Array.ConvertAll(values, v => v + offset);
}
