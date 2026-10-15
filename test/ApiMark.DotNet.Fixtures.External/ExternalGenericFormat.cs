namespace ApiMark.DotNet.Fixtures.External;

/// <summary>An external generic type used to verify the External Types table rendering.</summary>
/// <typeparam name="T">The formatted value type.</typeparam>
public class ExternalGenericFormat<T>
{
    /// <summary>Gets the formatted value.</summary>
    public T? Value { get; }
}
