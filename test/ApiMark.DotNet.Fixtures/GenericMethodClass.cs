// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>A fixture class declaring generic methods with various constraints.</summary>
public class GenericMethodClass
{
    /// <summary>Converts an item.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="item">The item.</param>
    /// <returns>The item.</returns>
    public T Echo<T>(T item) => item;

    /// <summary>Maps one type to another.</summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The default output.</returns>
    public TOut Map<TIn, TOut>(TIn value)
        where TIn : class, IDisposable
        where TOut : struct => default;

    /// <summary>Creates an instance.</summary>
    /// <typeparam name="T">The created type.</typeparam>
    /// <returns>A new instance.</returns>
    public static T Create<T>()
        where T : class, new() => new();

    /// <summary>Takes an unmanaged value.</summary>
    /// <typeparam name="T">The unmanaged type.</typeparam>
    /// <param name="value">The value.</param>
    public void Take<T>(T value)
        where T : unmanaged
    {
    }

    /// <summary>Overload with an int.</summary>
    /// <param name="value">The value.</param>
    public void Over(int value)
    {
    }

    /// <summary>Overload with a generic parameter.</summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="value">The value.</param>
    public void Over<T>(T value)
        where T : IComparable<T>
    {
    }
}
