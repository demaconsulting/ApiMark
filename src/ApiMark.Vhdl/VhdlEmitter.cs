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

    private readonly VhdlGeneratorOptions _options;
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
    internal static string? GetSummary(VhdlDocComment? doc) =>
        string.IsNullOrEmpty(doc?.Summary) ? null : doc!.Summary;

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
        var classKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "SIGNAL", "VARIABLE", "CONSTANT", "FILE" };
        var direction = string.Join(
            " ",
            param.Mode.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => !classKeywords.Contains(t)));
        return string.IsNullOrEmpty(direction) ? param.TypeName : $"{direction} {param.TypeName}";
    }

    /// <summary>
    ///     Returns a copy of <paramref name="name"/> with every character that is invalid
    ///     in a file name replaced by <c>_</c>.
    /// </summary>
    /// <param name="name">The raw declaration name to sanitize.</param>
    /// <returns>A filesystem-safe file name string.</returns>
    internal static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalidChars, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }
}
