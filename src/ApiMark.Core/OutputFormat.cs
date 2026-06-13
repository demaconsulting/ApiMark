namespace ApiMark.Core;

/// <summary>
///     Specifies the output structure produced by an <see cref="IApiEmitter"/>.
/// </summary>
public enum OutputFormat
{
    /// <summary>
    ///     Gradual-disclosure tree: one file per namespace, type, and member, enabling
    ///     AI consumers to read only as much context as the task requires.
    ///     This is the default format and is backward-compatible with the original output.
    /// </summary>
    GradualDisclosure,

    /// <summary>
    ///     All API content written into a single <c>api.md</c> file, using heading-level
    ///     offsets controlled by <see cref="EmitConfig.HeadingDepth"/> so the output can
    ///     be embedded as a section inside a larger document.
    /// </summary>
    SingleFile,
}
