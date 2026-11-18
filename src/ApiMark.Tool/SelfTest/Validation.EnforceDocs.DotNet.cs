namespace ApiMark.Tool.SelfTest;

/// <summary>
///     Provides the .NET documentation-coverage enforcement (<c>--enforce-docs</c>) self-test for
///     ApiMark Tool.
/// </summary>
internal static partial class Validation
{
    /// <summary>
    ///     Checks <c>--enforce-docs Public</c> on the sample assembly (see
    ///     <see cref="WriteSampleAssembly"/>): with complete XML documentation nothing is reported and
    ///     the run succeeds even at <c>Error</c> severity; with the <c>Thermostat.Read</c> entry removed
    ///     exactly that method is reported, the run fails at <c>Error</c> severity, and it succeeds at
    ///     the default <c>Warning</c> severity.
    /// </summary>
    /// <returns><see langword="null"/> on success; otherwise the failure message.</returns>
    private static string? DotNetEnforceDocsTest()
    {
        using var workspace = new TemporaryDirectory();
        var documented = WriteSampleAssembly(workspace.CreateSubdirectory("documented"));
        var gap = WriteSampleAssembly(workspace.CreateSubdirectory("gap"), documentRead: false);
        const string Undocumented = "  [Undocumented] Method: ApiMarkSample.Thermostat.Read()";

        // Fully documented: no finding, success at Error severity
        var clean = RunApiMark(
            workspace,
            "dotnet", "--assembly", documented.AssemblyPath, "--xml-doc", documented.XmlDocPath,
            "--output", workspace.PathOf("out-documented"), "--enforce-docs", "Public", "--enforce-docs-severity", "Error");
        var failure = CheckExitCode(clean, 0)
                      ?? CheckContains("output of the documented run", clean.Log, "Documentation coverage: 0 undocumented of 8 checked.")
                      ?? CheckDoesNotContain("output of the documented run", clean.Log, "[Undocumented]");
        if (failure != null)
        {
            return failure;
        }

        // Missing documentation at Error severity: the method is reported and the run fails
        var error = RunApiMark(
            workspace,
            "dotnet", "--assembly", gap.AssemblyPath, "--xml-doc", gap.XmlDocPath,
            "--output", workspace.PathOf("out-error"), "--enforce-docs", "Public", "--enforce-docs-severity", "Error");
        failure = CheckExitCode(error, 1)
                  ?? CheckContains(
                      "output of the Error-severity run",
                      error.Log,
                      $"{Undocumented}\n",
                      "Documentation coverage: 1 undocumented of 8 checked.",
                      "Error: 1 undocumented API item(s) found (--enforce-docs-severity Error).");
        if (failure != null)
        {
            return failure;
        }

        // Missing documentation at the default Warning severity: reported, but the run succeeds
        var warning = RunApiMark(
            workspace,
            "dotnet", "--assembly", gap.AssemblyPath, "--xml-doc", gap.XmlDocPath,
            "--output", workspace.PathOf("out-warning"), "--enforce-docs", "Public");
        return CheckExitCode(warning, 0)
               ?? CheckContains(
                   "output of the Warning-severity run",
                   warning.Log,
                   $"{Undocumented}\n",
                   "Documentation coverage: 1 undocumented of 8 checked.");
    }
}
