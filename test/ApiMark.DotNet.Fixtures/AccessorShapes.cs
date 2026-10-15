// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>A class whose properties have accessors of differing accessibility.</summary>
public class AccessorShapes
{
    /// <summary>Gets a value with a private setter.</summary>
    public int PrivateSet { get; private set; }

    /// <summary>Gets a value with an internal setter.</summary>
    public int InternalSet { get; internal set; }

    /// <summary>Gets a value with a protected setter.</summary>
    public int ProtectedSet { get; protected set; }

    /// <summary>Gets a value with a protected internal setter.</summary>
    public int ProtectedInternalSet { get; protected internal set; }

    /// <summary>Gets a value that can only be initialized.</summary>
    public int InitOnly { get; init; }

    /// <summary>Sets a value whose getter is private.</summary>
    public int PrivateGet { private get; set; }

    /// <summary>Gets and sets a plain value.</summary>
    public int Plain { get; set; }
}
