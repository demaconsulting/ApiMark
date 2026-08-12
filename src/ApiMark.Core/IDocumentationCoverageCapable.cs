namespace ApiMark.Core;

/// <summary>
///     Optional capability implemented by language generators that support documentation-coverage
///     enforcement (scanning a parsed API surface for declarations missing a summary).
/// </summary>
/// <remarks>
///     <para>
///         Implemented by <c>DotNetGenerator</c>, <c>CppGenerator</c>, and <c>VhdlGenerator</c>.
///         <c>Program.cs</c> tests for this interface via <c>generator is IDocumentationCoverageCapable</c>
///         instead of downcasting to a specific language generator type, so enforcement wiring in
///         <c>ApiMark.Tool</c> stays language-agnostic.
///     </para>
///     <para>
///         Each implementation owns parsing and validating its own <c>enforceTier</c> vocabulary:
///         .NET and C++ interpret it as a three-tier visibility (<c>Public</c>,
///         <c>PublicAndProtected</c>, <c>All</c>) mapped onto their respective accessibility models;
///         VHDL has no visibility concept at all, so it accepts the same three-word vocabulary
///         purely for CLI consistency but treats all three values identically (see
///         <c>ApiMark.Vhdl.DocumentationCoverageChecker</c>).
///     </para>
/// </remarks>
public interface IDocumentationCoverageCapable
{
    /// <summary>
    ///     Scans the API surface parsed by the most recent <c>Parse</c> call for declarations
    ///     missing a documentation summary.
    /// </summary>
    /// <param name="enforceTier">
    ///     The raw enforcement tier string supplied by the caller (typically the CLI
    ///     <c>--enforce-docs</c> value), e.g. <c>"Public"</c>. May be <see langword="null"/> or
    ///     empty, in which case the implementing generator falls back to its own
    ///     options-configured default enforcement tier instead of requiring the caller to
    ///     supply one explicitly.
    /// </param>
    /// <returns>
    ///     A <see cref="DocumentationCoverageResult"/> describing every undocumented declaration found.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="enforceTier"/> is not a value recognized by the implementing
    ///     generator's enforcement vocabulary.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when called before <c>Parse</c> has completed successfully.
    /// </exception>
    DocumentationCoverageResult CheckDocumentationCoverage(string? enforceTier);
}
