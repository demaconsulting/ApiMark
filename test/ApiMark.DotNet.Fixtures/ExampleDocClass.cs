namespace ApiMark.DotNet.Fixtures;

/// <summary>A fixture class for testing example code block rendering.</summary>
/// <example>
/// Here is how to create an instance:
/// <code>
/// var obj = new ExampleDocClass(42);
/// </code>
/// </example>
public class ExampleDocClass
{
    /// <summary>Gets the stored value.</summary>
    public int Value { get; }

    /// <summary>Initializes a new instance with the given value.</summary>
    /// <param name="value">The value to store.</param>
    public ExampleDocClass(int value)
    {
        Value = value;
    }

    /// <summary>Returns a greeting string for the specified name.</summary>
    /// <param name="name">The name to greet.</param>
    /// <returns>A greeting string.</returns>
    /// <example>
    /// Use this method to produce a greeting:
    /// <code>
    /// var obj = new ExampleDocClass(1);
    /// string g = obj.GetGreeting("Alice");
    /// </code>
    /// </example>
    public string GetGreeting(string name) => $"Hello, {name}! Value={Value}";
}
