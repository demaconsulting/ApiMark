namespace ApiMark.DotNet.Fixtures;

/// <summary>A sample interface for testing interface handling.</summary>
public interface ISampleInterface
{
    /// <summary>Gets the name.</summary>
    string Name { get; }

    /// <summary>Executes the specified input.</summary>
    /// <param name="input">The input to execute.</param>
    void Execute(string input);
}
