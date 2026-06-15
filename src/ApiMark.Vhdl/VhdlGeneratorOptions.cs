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
    ///     which VHDL files are documented.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Both absolute and relative glob patterns are supported. Relative patterns are
    ///         resolved against <see cref="WorkingDirectory"/> (or the process working directory
    ///         when <see cref="WorkingDirectory"/> is <see langword="null"/>).
    ///         Absolute patterns determine their own root from the non-glob path prefix.
    ///     </para>
    ///     <para>
    ///         Patterns whose final segment is a bare <c>*</c> (e.g. <c>**/*</c>,
    ///         <c>src/*</c>) automatically discover <c>.vhd</c> and <c>.vhdl</c> files.
    ///         Patterns with an explicit extension (e.g. <c>**/*.vhd</c>) select only files
    ///         matching that extension.
    ///     </para>
    ///     <para>
    ///         Entries prefixed with <c>!</c> are exclusion patterns (the <c>!</c> is stripped
    ///         before glob matching). Inclusion patterns build the result set; exclusion patterns
    ///         subtract from it. An empty list or a list containing only exclusion patterns
    ///         produces an error; no output files are written.
    ///     </para>
    /// </remarks>
    public IList<string> Sources { get; set; } = new List<string>();

    /// <summary>
    ///     Gets or sets the directory used as the root for glob pattern evaluation.
    ///     Defaults to <see langword="null"/>, which means <see cref="Directory.GetCurrentDirectory"/>
    ///     is used at parse time. Set this in tests to anchor patterns to the fixture directory.
    /// </summary>
    public string? WorkingDirectory { get; set; }
}
