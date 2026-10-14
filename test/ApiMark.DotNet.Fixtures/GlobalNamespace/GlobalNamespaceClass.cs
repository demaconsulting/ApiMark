// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

// NOTE: this file intentionally declares types WITHOUT a namespace. It is excluded from the
// ApiMark.DotNet.Fixtures project and compiled only into ApiMark.DotNet.Fixtures.Global so the
// shared fixture assembly keeps its existing output unchanged.

/// <summary>A public class declared in the global namespace. See <see cref="GlobalNamespaceClass"/>.</summary>
public class GlobalNamespaceClass
{
    /// <summary>Gets or sets a sample value.</summary>
    public int Value { get; set; }

    /// <summary>Performs a sample operation.</summary>
    /// <param name="input">The input value.</param>
    /// <returns>The input value.</returns>
    public string Echo(string input) => input;

    /// <summary>A nested class inside a global-namespace type.</summary>
    public class NestedGlobalClass
    {
        /// <summary>Gets or sets a sample name.</summary>
        public string Name { get; set; } = string.Empty;
    }
}

/// <summary>A second global-namespace type whose signatures reference <see cref="GlobalNamespaceClass"/>.</summary>
public class GlobalNamespaceConsumer
{
    /// <summary>Gets or sets the referenced instance.</summary>
    public GlobalNamespaceClass? Target { get; set; }

    /// <summary>Creates an instance of <see cref="GlobalNamespaceClass"/>.</summary>
    /// <returns>A new instance.</returns>
    public GlobalNamespaceClass Create() => new GlobalNamespaceClass();
}
