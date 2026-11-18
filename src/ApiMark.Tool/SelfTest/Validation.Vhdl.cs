namespace ApiMark.Tool.SelfTest;

/// <summary>
///     Provides the VHDL generation self-test for ApiMark Tool.
/// </summary>
internal static partial class Validation
{
    /// <summary>A documented VHDL entity with one generic and one port, used by the VHDL generation self-test.</summary>
    private const string VhdlGenerationSample =
        "--! @brief Self-test counter entity.\n" +
        "ENTITY apimark_sample_counter IS\n" +
        "    GENERIC (\n" +
        "        WIDTH : INTEGER := 8 --! Width of the counter data bus in bits\n" +
        "    );\n" +
        "    PORT (\n" +
        "        clk : IN STD_LOGIC --! Rising-edge clock input\n" +
        "    );\n" +
        "END ENTITY apimark_sample_counter;\n";

    /// <summary>
    ///     Checks that <c>apimark vhdl ... --format single-file</c> documents a sample entity with its
    ///     brief description, generic table, and port table.
    /// </summary>
    /// <remarks>
    ///     VHDL parsing is performed entirely in-process via an embedded ANTLR4 grammar, so unlike the
    ///     C++ tests no external tool is required and this test always runs.
    /// </remarks>
    /// <returns><see langword="null"/> on success; otherwise the failure message.</returns>
    private static string? VhdlGenerationTest()
    {
        using var workspace = new TemporaryDirectory();
        var source = workspace.PathOf("counter.vhd");
        File.WriteAllText(source, VhdlGenerationSample);
        var output = workspace.PathOf("api");

        var run = RunApiMark(
            workspace,
            "vhdl", "--source", source, "--output", output, "--library-name", "ApiMarkSelfTest", "--format", "single-file");

        return CheckExitCode(run, 0)
               ?? CheckFiles(output, "api.md")
               ?? CheckContains(
                   "api.md",
                   File.ReadAllText(Path.Join(output, "api.md")),
                   "# ApiMarkSelfTest API Reference\n",
                   "\n## Entities\n",
                   "\n### apimark_sample_counter\n",
                   "*Entity declared in `counter.vhd`*",
                   "Self-test counter entity.",
                   "| WIDTH | INTEGER | 8 | Width of the counter data bus in bits |",
                   "| clk | IN | STD_LOGIC | Rising-edge clock input |");
    }
}
