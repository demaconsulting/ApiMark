// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.DotNet.Fixtures;

/// <summary>A sample class for testing the API generator.</summary>
public class SampleClass
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; internal set; } = string.Empty;

    /// <summary>Gets the default name constant.</summary>
    public const string DefaultName = "default";

    /// <summary>Occurs when the name changes.</summary>
    public event EventHandler? NameChanged;

    /// <summary>Gets a greeting for the specified name.</summary>
    /// <param name="name">The name to greet.</param>
    /// <returns>A greeting string.</returns>
    public static string GetGreeting(string name) => $"Hello, {name}!";

    /// <summary>Resets this instance to its default state.</summary>
    public void Reset()
    {
        Name = string.Empty;
        Title = string.Empty;
    }

#pragma warning disable CS1591 // Missing XML comment — intentional fixture for testing the no-description placeholder (test 14)
    public void Refresh()
    {
        // Fixture: intentional empty implementation — exercises the no-description placeholder in generated docs
    }
#pragma warning restore CS1591

    /// <summary>Raises the <see cref="NameChanged"/> event.</summary>
    protected virtual void OnNameChanged() => NameChanged?.Invoke(this, EventArgs.Empty);
}
