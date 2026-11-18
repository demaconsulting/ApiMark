using ApiMark.Cpp.CppAst;

namespace ApiMark.Tool.SelfTest;

/// <summary>
///     Provides the C++ generation self-test for ApiMark Tool.
/// </summary>
internal static partial class Validation
{
    /// <summary>A documented C++ struct with one documented field, used by the C++ generation self-test.</summary>
    private const string CppGenerationSample =
        "/// Represents a single point in 2D space.\n" +
        "struct ApiMarkSamplePoint\n" +
        "{\n" +
        "    /// The horizontal coordinate.\n" +
        "    int x;\n" +
        "};\n";

    /// <summary>
    ///     Returns the reason the C++ self-tests are skipped, or <see langword="null"/> when they can run.
    /// </summary>
    /// <remarks>
    ///     C++ parsing runs an external clang executable, which is not part of the tool package, so
    ///     the C++ self-tests run only when clang can be found with the same discovery logic the
    ///     <c>cpp</c> subcommand uses (<see cref="ClangDiscovery.IsAvailable"/>). Otherwise they are
    ///     recorded as skipped, never failed, so the exit code is unaffected.
    /// </remarks>
    /// <returns>The skip reason, or <see langword="null"/>.</returns>
    private static string? CppSkipReason() =>
        ClangDiscovery.IsAvailable()
            ? null
            : "clang not found; install LLVM clang or set APIMARK_CLANG_PATH to validate C++ support";

    /// <summary>
    ///     Checks that <c>apimark cpp ... --format single-file</c> documents a sample header with the
    ///     struct's summary, its include line, and its documented field.
    /// </summary>
    /// <returns><see langword="null"/> on success; otherwise the failure message.</returns>
    private static string? CppGenerationTest()
    {
        using var workspace = new TemporaryDirectory();
        var include = workspace.CreateSubdirectory("include");
        File.WriteAllText(Path.Join(include, "point.h"), CppGenerationSample);
        var output = workspace.PathOf("api");

        var run = RunApiMark(
            workspace,
            "cpp", "--includes", include, "--output", output, "--library-name", "ApiMarkSelfTest", "--format", "single-file");

        return CheckExitCode(run, 0)
               ?? CheckFiles(output, "api.md")
               ?? CheckContains(
                   "api.md",
                   File.ReadAllText(Path.Join(output, "api.md")),
                   "# ApiMarkSelfTest API Reference\n",
                   "\n### ApiMarkSamplePoint\n",
                   "#include \"point.h\"",
                   "Represents a single point in 2D space.",
                   "- **x**: The horizontal coordinate.",
                   "\n#### x\n",
                   "int x;");
    }
}
