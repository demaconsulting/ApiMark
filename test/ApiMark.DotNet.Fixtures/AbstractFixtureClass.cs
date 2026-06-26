// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>Abstract fixture class for testing the <c>abstract</c> modifier in type signatures.</summary>
/// <remarks>
///     Used by <c>DotNetEmitter_BuildTypeSignature_AbstractClass_ContainsAbstractModifier</c> to verify
///     that <c>BuildTypeSignature</c> emits the <c>abstract</c> keyword for abstract (non-sealed) classes.
/// </remarks>
public abstract class AbstractFixtureClass
{
    /// <summary>Abstract method that derived classes must implement.</summary>
    /// <param name="value">The input value to process.</param>
    /// <returns>A processed result string.</returns>
    public abstract string Process(int value);

    /// <summary>Gets or sets a protected value used to verify property accessor signature rendering.</summary>
    protected int ProtectedProperty { get; set; }
}
