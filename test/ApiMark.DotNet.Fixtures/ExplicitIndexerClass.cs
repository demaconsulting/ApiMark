// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>An interface declaring an indexer, implemented explicitly by <see cref="ExplicitIndexerClass"/>.</summary>
public interface IIndexerSource
{
    /// <summary>An indexer whose explicit-interface-implementation override ID must carry the index parameter.</summary>
    /// <param name="index">The index to retrieve or set.</param>
    /// <returns>The value at <paramref name="index"/>.</returns>
    int this[int index] { get; set; }
}

/// <summary>
///     A fixture class exercising explicit interface implementation of an indexer, so the
///     override's XML-doc inheritance target ID (built from the accessors' <c>MethodReference</c>
///     overrides) can be verified to carry the index parameter type list, excluding the setter's
///     trailing <c>value</c> parameter.
/// </summary>
public class ExplicitIndexerClass : IIndexerSource
{
    private int _value;

    /// <inheritdoc/>
    int IIndexerSource.this[int index]
    {
        get => _value;
        set => _value = value;
    }
}
