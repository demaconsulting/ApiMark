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

    /// <summary>
    ///     Gets or sets the documentation-coverage enforcement tier string. Defaults to
    ///     <see langword="null"/>, meaning enforcement is disabled unless a tier is supplied
    ///     directly to <see cref="VhdlGenerator.CheckDocumentationCoverage"/>.
    /// </summary>
    /// <remarks>
    ///     VHDL has no accessibility/visibility concept, so any recognized value
    ///     (<c>Public</c>, <c>PublicAndProtected</c>, <c>All</c>) enables the same
    ///     public-interface-only check — the value itself is accepted purely for CLI vocabulary
    ///     consistency with the .NET and C++ subcommands. Stored as a raw string, unlike
    ///     <c>DotNetGeneratorOptions.EnforceDocsVisibility</c>/<c>CppGeneratorOptions.EnforceDocsVisibility</c>,
    ///     because there is no corresponding enum to parse into.
    /// </remarks>
    public string? EnforceDocsVisibility { get; set; }
}
