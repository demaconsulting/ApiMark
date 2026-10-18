// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures.External.Nested;

/// <summary>An external non-generic host with nested types, in a child namespace of the external root.</summary>
public class NestedHost
{
    /// <summary>A nested type.</summary>
    public class Inner
    {
        /// <summary>Runs the nested type.</summary>
        public void Run()
        {
            // Intentionally empty: only the declaration shape matters to the tests
        }
    }

    /// <summary>A nested type that itself hosts a nested type.</summary>
    public class Deep
    {
        /// <summary>A doubly nested type.</summary>
        public class Leaf
        {
        }
    }
}

/// <summary>An external generic host with a generic nested type.</summary>
/// <typeparam name="T">The host type parameter.</typeparam>
public class NestedGenericHost<T>
{
    /// <summary>A generic nested type.</summary>
    /// <typeparam name="U">The nested type parameter.</typeparam>
    public class Inner<U>
    {
        /// <summary>Gets a value.</summary>
        public U? Value { get; }
    }
}
