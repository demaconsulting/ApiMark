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
    ///     Valid range: 1–3. At depth 3, member headings reach H6 (the maximum in
    ///     Markdown); values above 3 would produce H7+ headings which are not defined
    ///     by CommonMark.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when the supplied value is less than 1 or greater than 3.
    /// </exception>
    public int HeadingDepth
    {
        get => _headingDepth;
        init
        {
            // Enforce the 1–3 range so that the effective member heading level
            // (HeadingDepth + 3) stays within the H1–H6 range supported by Markdown
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 3);
            _headingDepth = value;
        }
    }
}
