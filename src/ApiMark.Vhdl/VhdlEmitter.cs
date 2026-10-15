using ApiMark.Core;
using ApiMark.Vhdl.VhdlAst;

namespace ApiMark.Vhdl;

/// <summary>IApiEmitter implementation that dispatches to format-specific VHDL emitters.</summary>
internal sealed class VhdlEmitter : IApiEmitter
{
    /// <summary>Column header for description columns in tables.</summary>
    internal const string DescriptionColumnHeader = "Description";

    /// <summary>Placeholder text for members without documentation.</summary>
    internal const string NoDescriptionPlaceholder = "*No description provided.*";

    /// <summary>Placeholder text for sections that have no items.</summary>
    internal const string NoItemsPlaceholder = "*None.*";

    /// <summary>VHDL object-class keywords (SIGNAL, VARIABLE, CONSTANT, FILE) stripped from parameter mode strings by FormatParamType.</summary>
    private static readonly HashSet<string> ObjectClassKeywords =
        new(StringComparer.OrdinalIgnoreCase) { "SIGNAL", "VARIABLE", "CONSTANT", "FILE" };

    /// <summary>Generator options forwarded from VhdlGenerator.</summary>
    private readonly VhdlGeneratorOptions _options;

    /// <summary>Parsed file models to emit.</summary>
    private readonly IReadOnlyList<VhdlFileModel> _fileModels;

    /// <summary>Initializes a new VhdlEmitter with the specified options and file models.</summary>
    /// <param name="options">Generator options.</param>
    /// <param name="fileModels">Parsed file models to emit.</param>
    internal VhdlEmitter(VhdlGeneratorOptions options, IReadOnlyList<VhdlFileModel> fileModels)
    {
        _options = options;
        _fileModels = fileModels;
    }

    /// <summary>Gets the generator options.</summary>
    internal VhdlGeneratorOptions Options => _options;

    /// <inheritdoc/>
    public void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (_fileModels.Count == 0)
        {
            return;
        }

        if (config.Format == OutputFormat.SingleFile)
        {
            new VhdlEmitterSingleFile(this, _fileModels).Emit(factory, config, context);
        }
        else
        {
            new VhdlEmitterGradualDisclosure(this, _fileModels).Emit(factory, config, context);
        }
    }

    /// <summary>Gets the summary from a VhdlDocComment, or null.</summary>
    /// <param name="doc">The doc comment to extract from, or null.</param>
    /// <returns>The summary text, or null if doc is null.</returns>
    internal static string? GetSummary(VhdlDocComment? doc)
    {
        var summary = doc?.Summary;
        return string.IsNullOrEmpty(summary) ? null : summary;
    }

    /// <summary>
    ///     Formats a subprogram parameter type for display, combining direction and type name
    ///     while stripping object class keywords (SIGNAL, VARIABLE, CONSTANT, FILE) that are
    ///     implementation details not relevant to API consumers.
    /// </summary>
    /// <param name="param">The parameter declaration to format.</param>
    /// <returns>
    ///     The direction-prefixed type string (e.g. <c>OUT STD_LOGIC_VECTOR</c>), or just the
    ///     type name when no explicit direction is present.
    /// </returns>
    internal static string FormatParamType(VhdlParamDecl param)
    {
        var direction = string.Join(
            " ",
            param.Mode.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => !ObjectClassKeywords.Contains(t)));
        return string.IsNullOrEmpty(direction) ? param.TypeName : $"{direction} {param.TypeName}";
    }

    /// <summary>
    ///     Converts <paramref name="name"/> into a file name segment that is legal on Windows and
    ///     Linux by delegating to <see cref="FileNameSanitizer.SanitizeSegment"/> (which also moves
    ///     reserved device names such as <c>aux</c> or <c>con</c> aside).
    /// </summary>
    /// <param name="name">The raw declaration name to sanitize.</param>
    /// <returns>A filesystem-safe file name string.</returns>
    internal static string SanitizeFileName(string name) => FileNameSanitizer.SanitizeSegment(name);

    /// <summary>
    ///     Converts the name of a root-level entity or package into its page file name; a name equal to
    ///     the reserved <c>api</c> index page is moved aside so it can never overwrite <c>api.md</c>.
    /// </summary>
    /// <param name="name">The raw entity or package name.</param>
    /// <returns>A legal root-level page file name without extension.</returns>
    internal static string SanitizePageName(string name)
    {
        var sanitized = SanitizeFileName(name);
        return string.Equals(sanitized, "api", StringComparison.OrdinalIgnoreCase) ? sanitized + "-" : sanitized;
    }
}
