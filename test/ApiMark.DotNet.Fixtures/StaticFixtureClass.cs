namespace ApiMark.DotNet.Fixtures;

/// <summary>Static fixture class for testing the static modifier in generated signatures.</summary>
/// <remarks>
///     Used by <c>DotNetEmitter_BuildTypeSignature_StaticClass_ContainsStaticModifier</c> to verify
///     that <c>BuildTypeSignature</c> emits the <c>static</c> keyword for static classes, and by
///     <c>DotNetEmitter_BuildPropertySignature_StaticProperty_ContainsStaticModifier</c> to verify
///     that <c>BuildPropertySignature</c> emits the <c>static</c> keyword for static properties.
/// </remarks>
public static class StaticFixtureClass
{
    /// <summary>Returns the formatted value as a string.</summary>
    /// <param name="value">The value to format.</param>
    /// <returns>A formatted string representation of the value.</returns>
    public static string Format(int value) => $"StaticFixtureClass({value})";

    /// <summary>Gets the fixture label.</summary>
    public static string Label { get; } = "StaticFixtureClass";
}
