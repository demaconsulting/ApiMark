// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>An interface declaring a generic method, implemented explicitly by <see cref="GenericOverrideClass"/>.</summary>
public interface IGenericOverrideSource
{
    /// <summary>A generic method whose explicit-interface-implementation override ID must match this one.</summary>
    /// <typeparam name="T">The type of <paramref name="value"/>.</typeparam>
    /// <param name="value">The value to return unchanged.</param>
    /// <returns><paramref name="value"/>, unchanged.</returns>
    T Wrap<T>(T value);
}

/// <summary>
///     A fixture class exercising explicit interface implementation of a generic method, so the
///     override's XML-doc inheritance target ID (built from a <c>MethodReference</c>) can be
///     verified to carry the same <c>``N</c> generic-arity suffix as the interface method's own
///     ID (built from a <c>MethodDefinition</c>).
/// </summary>
public class GenericOverrideClass : IGenericOverrideSource
{
    /// <inheritdoc/>
    T IGenericOverrideSource.Wrap<T>(T value) => value;
}
