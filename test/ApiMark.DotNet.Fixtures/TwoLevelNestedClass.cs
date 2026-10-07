// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>An outer class containing a two-level-deep nested type, for testing nested type page generation at arbitrary nesting depth.</summary>
public class TwoLevelNestedClass
{
    /// <summary>A nested class one level inside <see cref="TwoLevelNestedClass"/>.</summary>
    public class Middle
    {
        /// <summary>A nested class two levels inside <see cref="TwoLevelNestedClass"/> (nested inside <see cref="Middle"/>).</summary>
        public class Inner
        {
            /// <summary>Gets the value held by this doubly-nested type.</summary>
            public int Value { get; }

            /// <summary>Initializes a new instance with the specified value.</summary>
            /// <param name="value">The value to hold.</param>
            public Inner(int value)
            {
                Value = value;
            }
        }
    }
}
