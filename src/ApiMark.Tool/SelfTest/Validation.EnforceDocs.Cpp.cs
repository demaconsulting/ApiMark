namespace ApiMark.Tool.SelfTest;

/// <summary>
///     Provides the C++ documentation-coverage enforcement (<c>--enforce-docs</c>) self-test for
///     ApiMark Tool.
/// </summary>
internal static partial class Validation
{
    /// <summary>A C++ struct with one documented field (<c>x</c>) and one undocumented field (<c>y</c>).</summary>
    private const string CppEnforceDocsSample =
        "/// Represents a single point in 2D space.\n" +
        "struct ApiMarkEnforceSamplePoint\n" +
        "{\n" +
        "    /// The horizontal coordinate.\n" +
        "    int x;\n" +
        "    int y;\n" +
        "};\n";

    /// <summary>
    ///     Checks that <c>apimark cpp ... --enforce-docs Public --enforce-docs-severity Error</c> on a
    ///     sample header reports exactly its undocumented field <c>y</c>, not its documented field
    ///     <c>x</c>, and fails the run. Skipped when clang is unavailable (see <see cref="CppSkipReason"/>).
    /// </summary>
    /// <returns><see langword="null"/> on success; otherwise the failure message.</returns>
    private static string? CppEnforceDocsTest()
    {
        using var workspace = new TemporaryDirectory();
        var include = workspace.CreateSubdirectory("include");
        File.WriteAllText(Path.Join(include, "point.h"), CppEnforceDocsSample);

        var run = RunApiMark(
            workspace,
            "cpp", "--includes", include, "--output", workspace.PathOf("api"), "--library-name", "ApiMarkSelfTest",
            "--enforce-docs", "Public", "--enforce-docs-severity", "Error");

        return CheckExitCode(run, 1)
               ?? CheckContains(
                   "output",
                   run.Log,
                   "  [Undocumented] Field: global::ApiMarkEnforceSamplePoint::y\n",
                   "Documentation coverage: 1 undocumented of 3 checked.",
                   "Error: 1 undocumented API item(s) found (--enforce-docs-severity Error).")
               ?? CheckDoesNotContain("output", run.Log, "ApiMarkEnforceSamplePoint::x");
    }
}
