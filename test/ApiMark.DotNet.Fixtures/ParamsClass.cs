// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>A class exposing <c>params</c> parameter arrays on a constructor, methods, and a delegate.</summary>
public class ParamsClass
{
    /// <summary>Initializes a new instance of the <see cref="ParamsClass"/> class.</summary>
    /// <param name="name">The name.</param>
    /// <param name="values">The values.</param>
    public ParamsClass(string name, params int[] values)
    {
    }

    /// <summary>Sums the values.</summary>
    /// <param name="values">The values.</param>
    /// <returns>The sum.</returns>
    public int Sum(params int[] values) => values.Length;

    /// <summary>Joins the parts.</summary>
    /// <param name="separator">The separator.</param>
    /// <param name="parts">The parts.</param>
    /// <returns>The joined text.</returns>
    public string Join(string separator, params string?[] parts) => separator + parts.Length;

    /// <summary>Logs a formatted message.</summary>
    /// <param name="format">The format.</param>
    /// <param name="args">The arguments.</param>
    public void Log(string format, params object?[] args)
    {
    }
}

/// <summary>A delegate with a <c>params</c> array.</summary>
/// <param name="items">The items.</param>
public delegate void ParamsHandler(params string[] items);
