namespace ApiMark.Core;

/// <summary>
///     Configuration shared across all language emitters when producing output.
/// </summary>
/// <remarks>
///     <see cref="EmitConfig"/> carries only the cross-language output concerns
///     (<see cref="Format"/> and <see cref="HeadingDepth"/>); language-specific
///     concerns such as visibility filtering remain on each generator's own options type.
/// </remarks>
public sealed class EmitConfig
{
    /// <summary>
    ///     Gets the output format to produce. Defaults to <see cref="OutputFormat.GradualDisclosure"/>.
    /// </summary>
    public OutputFormat Format { get; init; } = OutputFormat.GradualDisclosure;

    /// <summary>
    ///     Gets the heading depth offset for the top-level heading in
    ///     <see cref="OutputFormat.SingleFile"/> output. A value of 1 (the default)
    ///     produces an H1 top-level heading; a value of 2 produces H2, and so on.
    ///     Only used when <see cref="Format"/> is <see cref="OutputFormat.SingleFile"/>.
    /// </summary>
    public int HeadingDepth { get; init; } = 1;
}
