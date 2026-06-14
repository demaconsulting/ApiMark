namespace ApiMark.Vhdl;

/// <summary>Configuration options for <see cref="VhdlGenerator"/>.</summary>
public sealed class VhdlGeneratorOptions
{
    /// <summary>Gets or sets the library name used as the top-level heading.</summary>
    public string LibraryName { get; set; } = string.Empty;

    /// <summary>Gets or sets the library description emitted as an introductory paragraph.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the ordered list of glob and exclusion pattern strings that select
    ///     which <c>.vhd</c> files are documented. Gitignore-style semantics apply: patterns
    ///     are evaluated in order; the last matching pattern wins. Entries with a <c>!</c>
    ///     prefix are exclusion patterns (the <c>!</c> is stripped before glob matching).
    ///     Patterns are evaluated relative to <see cref="WorkingDirectory"/> (or the process
    ///     working directory when <see cref="WorkingDirectory"/> is <see langword="null"/>).
    ///     An empty list or a list containing only exclusion patterns produces an
    ///     error; no output files are written.
    /// </summary>
    public IList<string> Sources { get; set; } = new List<string>();

    /// <summary>
    ///     Gets or sets the directory used as the root for glob pattern evaluation.
    ///     Defaults to <see langword="null"/>, which means <see cref="Directory.GetCurrentDirectory"/>
    ///     is used at parse time. Set this in tests to anchor patterns to the fixture directory.
    /// </summary>
    public string? WorkingDirectory { get; set; }
}
