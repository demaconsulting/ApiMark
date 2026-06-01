namespace ApiMark.DotNet.Fixtures;

/// <summary>A class for testing exception documentation handling.</summary>
public static class ExceptionDocClass
{
    /// <summary>Opens the connection.</summary>
    /// <param name="host">The host to connect to.</param>
    /// <exception cref="InvalidOperationException">Already connected.</exception>
    public static void Connect(string host) { _ = host; }
}
