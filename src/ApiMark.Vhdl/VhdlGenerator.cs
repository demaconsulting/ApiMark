using ApiMark.Core;
using ApiMark.Vhdl.VhdlAst;

namespace ApiMark.Vhdl;

/// <summary>IApiGenerator implementation that generates API documentation from VHDL source files.</summary>
public sealed class VhdlGenerator : IApiGenerator
{
    private readonly VhdlGeneratorOptions _options;

    /// <summary>Initializes a new VhdlGenerator with the specified options.</summary>
    /// <param name="options">Generator options. Must not be null. LibraryName must not be null or whitespace.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    /// <exception cref="ArgumentException">Thrown when options.LibraryName is null or whitespace.</exception>
    public VhdlGenerator(VhdlGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.LibraryName))
        {
            throw new ArgumentException("LibraryName must not be null or whitespace.", nameof(options));
        }

        // Normalize null Sources to empty list to prevent NullReferenceException
        options.Sources ??= new List<string>();
        _options = options;
    }

    /// <inheritdoc/>
    public IApiEmitter Parse(IContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var allFiles = CollectSourceFiles();

        if (allFiles.Count == 0)
        {
            context.WriteError("Error: no .vhd or .vhdl files matched the --source patterns.");
            return new VhdlEmitter(_options, []);
        }

        var fileModels = new List<VhdlFileModel>();
        foreach (var file in allFiles)
        {
            context.WriteLine($"Parsing {file}");
            try
            {
                fileModels.Add(VhdlAstParser.Parse(file));
            }
            catch (Exception ex)
            {
                context.WriteError($"Error: failed to parse {file} — {ex.Message}");
            }
        }

        return new VhdlEmitter(_options, fileModels);
    }

    // =========================================================================
    // Source file collection
    // =========================================================================

    /// <summary>
    ///     Enumerates <c>.vhd</c> and <c>.vhdl</c> files using <see cref="GlobFileCollector"/>
    ///     and returns a sorted, deduplicated list of absolute file paths.
    /// </summary>
    /// <returns>Sorted, deduplicated list of absolute file paths selected for documentation.</returns>
    private List<string> CollectSourceFiles()
    {
        var vhdlExtensions = new[] { ".vhd", ".vhdl" };
        var cwd = Path.GetFullPath(_options.WorkingDirectory ?? Directory.GetCurrentDirectory());

        return GlobFileCollector.Collect(_options.Sources, vhdlExtensions, cwd).ToList();
    }
}
