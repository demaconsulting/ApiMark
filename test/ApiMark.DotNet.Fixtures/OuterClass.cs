// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>An outer class containing a public nested type for testing nested type page generation.</summary>
public class OuterClass
{
    /// <summary>Gets the outer value.</summary>
    public int Value { get; }

    /// <summary>Initializes a new instance with the specified value.</summary>
    /// <param name="value">The outer value.</param>
    public OuterClass(int value)
    {
        Value = value;
    }

    /// <summary>A public nested class inside OuterClass.</summary>
    public class Inner
    {
        /// <summary>Gets the inner value.</summary>
        public int InnerValue { get; }

        /// <summary>Initializes a new instance with the specified inner value.</summary>
        /// <param name="innerValue">The inner value.</param>
        public Inner(int innerValue)
        {
            InnerValue = innerValue;
        }
    }
}
