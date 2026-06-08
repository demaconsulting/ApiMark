namespace ApiMark.DotNet.Fixtures;

/// <summary>A sample implementation of ISampleInterface.</summary>
public class SampleImplementation : ISampleInterface
{
    /// <inheritdoc/>
    public string Name => "Sample";

    /// <inheritdoc/>
    public void Execute(string input) { }
}
