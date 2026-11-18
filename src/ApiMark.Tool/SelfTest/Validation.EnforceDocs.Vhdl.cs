namespace ApiMark.Tool.SelfTest;

/// <summary>
///     Provides the VHDL documentation-coverage enforcement (<c>--enforce-docs</c>) self-test for
///     ApiMark Tool.
/// </summary>
internal static partial class Validation
{
    /// <summary>A VHDL entity with one documented port (<c>clk</c>) and one undocumented port (<c>rst</c>).</summary>
    private const string VhdlEnforceDocsSample =
        "--! @brief Self-test enforcement entity.\n" +
        "ENTITY apimark_enforce_sample IS\n" +
        "    PORT (\n" +
        "        clk : IN STD_LOGIC; --! Rising-edge clock input\n" +
        "        rst : IN STD_LOGIC\n" +
        "    );\n" +
        "END ENTITY apimark_enforce_sample;\n";

    /// <summary>
    ///     Checks that <c>apimark vhdl ... --enforce-docs Public --enforce-docs-severity Error</c> on a
    ///     sample entity reports exactly its undocumented <c>rst</c> port, not its documented
    ///     <c>clk</c> port, and fails the run.
    /// </summary>
    /// <returns><see langword="null"/> on success; otherwise the failure message.</returns>
    private static string? VhdlEnforceDocsTest()
    {
        using var workspace = new TemporaryDirectory();
        var source = workspace.PathOf("enforce.vhd");
        File.WriteAllText(source, VhdlEnforceDocsSample);

        var run = RunApiMark(
            workspace,
            "vhdl", "--source", source, "--output", workspace.PathOf("api"), "--library-name", "ApiMarkSelfTest",
            "--enforce-docs", "Public", "--enforce-docs-severity", "Error");

        return CheckExitCode(run, 1)
               ?? CheckContains(
                   "output",
                   run.Log,
                   "  [Undocumented] Port: apimark_enforce_sample.rst\n",
                   "Documentation coverage: 1 undocumented of 3 checked.",
                   "Error: 1 undocumented API item(s) found (--enforce-docs-severity Error).")
               ?? CheckDoesNotContain("output", run.Log, "apimark_enforce_sample.clk");
    }
}
