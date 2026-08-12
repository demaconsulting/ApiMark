using ApiMark.Core;
using ApiMark.Vhdl.VhdlAst;

namespace ApiMark.Vhdl;

/// <summary>IApiGenerator implementation that generates API documentation from VHDL source files.</summary>
public sealed class VhdlGenerator : IApiGenerator, IDocumentationCoverageCapable
{
    /// <summary>The three CLI vocabulary words accepted for <c>--enforce-docs</c>, matching the .NET/C++ tier names.</summary>
    private static readonly string[] ValidEnforceTiers = ["Public", "PublicAndProtected", "All"];

    private readonly VhdlGeneratorOptions _options;

    /// <summary>
    ///     The file models parsed by <see cref="Parse"/>, cached so
    ///     <see cref="CheckDocumentationCoverage"/> can be called afterward without re-parsing.
    ///     <see langword="null"/> until <see cref="Parse"/> completes.
    /// </summary>
    private List<VhdlFileModel>? _fileModels;

    /// <summary>Initializes a new VhdlGenerator with the specified options.</summary>
    /// <param name="options">Generator options. Must not be null. LibraryName must not be null or whitespace.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    /// <exception cref="ArgumentException">Thrown when options.LibraryName is null or whitespace.</exception>
    /// <remarks>If <c>options.Sources</c> is <see langword="null"/>, it is normalized to an empty list in-place before use.</remarks>
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
            _fileModels = [];
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

        // Cache the parsed file models so CheckDocumentationCoverage can be called after
        // Parse returns.
        _fileModels = fileModels;

        return new VhdlEmitter(_options, fileModels);
    }

    /// <summary>
    ///     Scans the file models parsed by the most recent <see cref="Parse"/> call for entities,
    ///     ports, generics, packages, and package-level exports lacking a documentation summary.
    /// </summary>
    /// <remarks>
    ///     Must be called after <see cref="Parse"/> returns.
    ///     <para>
    ///     VHDL has no visibility/accessibility concept, so <paramref name="enforceTier"/> is
    ///     validated purely for CLI vocabulary consistency with the .NET and C++ subcommands —
    ///     <c>Public</c>, <c>PublicAndProtected</c>, and <c>All</c> are all accepted and behave
    ///     identically, enabling the same public-interface-only check
    ///     (see <see cref="DocumentationCoverageChecker"/>). When <see langword="null"/> or
    ///     empty, <see cref="VhdlGeneratorOptions.EnforceDocsVisibility"/> is used instead.
    ///     </para>
    /// </remarks>
    /// <param name="enforceTier">
    ///     The enforcement tier as a string (<c>"Public"</c>, <c>"PublicAndProtected"</c>, or
    ///     <c>"All"</c> — case-insensitive), or <see langword="null"/>/empty to fall back to
    ///     <see cref="VhdlGeneratorOptions.EnforceDocsVisibility"/>.
    /// </param>
    /// <returns>
    ///     A <see cref="DocumentationCoverageResult"/> describing every undocumented declaration found.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when called before <see cref="Parse"/> has completed, or when neither
    ///     <paramref name="enforceTier"/> nor <see cref="VhdlGeneratorOptions.EnforceDocsVisibility"/>
    ///     is set.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="enforceTier"/> is set but not one of <c>Public</c>,
    ///     <c>PublicAndProtected</c>, or <c>All</c> (case-insensitive).
    /// </exception>
    public DocumentationCoverageResult CheckDocumentationCoverage(string? enforceTier)
    {
        if (_fileModels is null)
        {
            throw new InvalidOperationException(
                $"{nameof(CheckDocumentationCoverage)} must be called after {nameof(Parse)} has completed successfully.");
        }

        var tier = !string.IsNullOrEmpty(enforceTier) ? enforceTier : _options.EnforceDocsVisibility;

        if (string.IsNullOrEmpty(tier))
        {
            throw new InvalidOperationException(
                $"{nameof(CheckDocumentationCoverage)} requires an enforcement tier, either via the " +
                $"{nameof(enforceTier)} parameter or {nameof(VhdlGeneratorOptions.EnforceDocsVisibility)}.");
        }

        if (!ValidEnforceTiers.Any(t => string.Equals(t, tier, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                $"Invalid --enforce-docs value '{tier}'. " +
                $"Valid values are: {string.Join(", ", ValidEnforceTiers)}.");
        }

        // The parsed tier value is intentionally discarded beyond validation — VHDL has no
        // visibility concept, so all three recognized values enable the identical
        // public-interface-only check.
        return DocumentationCoverageChecker.Check(_fileModels);
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
