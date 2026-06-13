using ApiMark.Core;
using ApiMark.Vhdl.VhdlAst;
using Microsoft.Extensions.FileSystemGlobbing;

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

        _options = options;
    }

    /// <inheritdoc/>
    public IApiEmitter Parse(IContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var allFiles = CollectSourceFiles();

        var fileModels = new List<VhdlFileModel>();
        foreach (var file in allFiles)
        {
            context.WriteLine($"Parsing {file}");
            fileModels.Add(VhdlAstParser.Parse(file));
        }

        return new VhdlEmitter(_options, fileModels);
    }

    // =========================================================================
    // Source file collection
    // =========================================================================

    /// <summary>
    ///     Enumerates <c>.vhd</c> and <c>.vhdl</c> files from the current working directory
    ///     using <see cref="VhdlGeneratorOptions.Sources"/> with gitignore-style last-match-wins
    ///     semantics to determine which files are documented.
    /// </summary>
    /// <returns>Sorted, deduplicated list of absolute file paths selected for documentation.</returns>
    private List<string> CollectSourceFiles()
    {
        var vhdlExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".vhd", ".vhdl",
        };

        var cwdAbsolute = Path.GetFullPath(_options.WorkingDirectory ?? Directory.GetCurrentDirectory());
        var compiledPatterns = CompileSourcePatterns();

        var allFiles = Directory.GetFiles(cwdAbsolute, "*", SearchOption.AllDirectories)
            .Where(f => vhdlExtensions.Contains(Path.GetExtension(f)))
            .Select(Path.GetFullPath)
            .ToList();

        if (compiledPatterns.Count == 0)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var absoluteFile in allFiles)
        {
            var relFromCwd = Path.GetRelativePath(cwdAbsolute, absoluteFile).Replace('\\', '/');

            var included = false;
            foreach (var (isExclusion, matcher) in compiledPatterns)
            {
                if (matcher.Match(relFromCwd).HasMatches)
                {
                    included = !isExclusion;
                }
            }

            if (included)
            {
                result.Add(absoluteFile);
            }
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    ///     Compiles <see cref="VhdlGeneratorOptions.Sources"/> patterns into precompiled
    ///     (isExclusion, matcher) pairs for reuse across every file evaluation.
    /// </summary>
    private List<(bool IsExclusion, Matcher Matcher)> CompileSourcePatterns()
    {
        var compiled = new List<(bool IsExclusion, Matcher Matcher)>();
        foreach (var pattern in _options.Sources)
        {
            if (pattern.StartsWith('!'))
            {
                var exclusionGlob = pattern.Substring(1).Trim();
                if (exclusionGlob.Length > 0)
                {
                    var m = new Matcher();
                    m.AddInclude(exclusionGlob);
                    compiled.Add((true, m));
                }
            }
            else
            {
                var m = new Matcher();
                m.AddInclude(pattern);
                compiled.Add((false, m));
            }
        }

        return compiled;
    }
}
