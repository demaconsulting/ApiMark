using ApiMark.DotNet.Fixtures.External;

namespace ApiMark.DotNet.Fixtures;

/// <summary>Consumes an external generic type in members so the External Types table lists it.</summary>
public class ExternalGenericConsumer
{
    /// <summary>Gets a format whose argument is a nested generic of aliases.</summary>
    public ExternalGenericFormat<Dictionary<string, List<int>>>? Mapped { get; }

    /// <summary>Gets a format whose argument is an array.</summary>
    public ExternalGenericFormat<int[]>? Arrayed { get; }

    /// <summary>Gets a format of a format.</summary>
    public ExternalGenericFormat<ExternalGenericFormat<string>>? Nested { get; }
}
