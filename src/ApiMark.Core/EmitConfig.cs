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

    /// <summary>The backing field for <see cref="HeadingDepth"/>.</summary>
    private int _headingDepth = 1;

    /// <summary>
    ///     Gets or inits the heading depth offset for the top-level heading in
    ///     <see cref="OutputFormat.SingleFile"/> output. A value of 1 (the default)
    ///     produces an H1 top-level heading; a value of 2 produces H2, and so on.
    ///     Only used when <see cref="Format"/> is <see cref="OutputFormat.SingleFile"/>.
    ///     Valid range: 1–6. The caller is responsible for ensuring that the chosen
    ///     depth is compatible with the number of heading levels the emitter will
    ///     nest below the top-level heading (e.g. single-file emitters that add up
    ///     to three additional levels must restrict the caller-supplied value to 1–3
    ///     to stay within the H1–H6 range supported by CommonMark).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when the supplied value is less than 1 or greater than 6.
    /// </exception>
    public int HeadingDepth
    {
        get => _headingDepth;
        init
        {
            // Enforce the 1–6 range: values outside this range cannot produce valid
            // ATX Markdown headings (H1–H6), regardless of downstream nesting depth
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 6);
            _headingDepth = value;
        }
    }
}
