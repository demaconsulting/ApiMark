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
    internal static string? GetSummary(VhdlDocComment? doc) => doc?.Summary;
}
