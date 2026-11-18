namespace ApiMark.Tool.SelfTest;

/// <summary>
///     Provides the .NET generation self-tests (single-file and gradual-disclosure output) for
///     ApiMark Tool.
/// </summary>
internal static partial class Validation
{
    /// <summary>
    ///     Checks that <c>apimark dotnet ... --format single-file</c> documents the sample assembly
    ///     (see <see cref="WriteSampleAssembly"/>) in a single <c>api.md</c> with every type, member
    ///     signature, summary, parameter, and return description.
    /// </summary>
    /// <returns><see langword="null"/> on success; otherwise the failure message.</returns>
    private static string? DotNetGenerationTest()
    {
        using var workspace = new TemporaryDirectory();
        var (assemblyPath, xmlDocPath) = WriteSampleAssembly(workspace.DirectoryPath);
        var output = workspace.PathOf("api");

        var run = RunApiMark(
            workspace,
            "dotnet", "--assembly", assemblyPath, "--xml-doc", xmlDocPath, "--output", output, "--format", "single-file");

        return CheckExitCode(run, 0)
               ?? CheckContains("output", run.Log, "Found 2 types across 1 namespace(s).")
               ?? CheckFiles(output, "api.md")
               ?? CheckContains(
                   "api.md",
                   File.ReadAllText(Path.Join(output, "api.md")),
                   "# ApiMarkSample API Reference\n",
                   "\n## ApiMarkSample\n",
                   "\n### Reading\n",
                   "public sealed class Reading",
                   "A single temperature reading.",
                   "\n#### Celsius\n",
                   "public double Celsius { get; }",
                   "\n### Thermostat\n",
                   "public sealed class Thermostat",
                   "Regulates a heater to hold a target temperature.",
                   "- **SetTarget(double celsius)**: Sets the target temperature.",
                   "\n#### Thermostat(double)\n",
                   "| initialTarget | double | The initial target temperature in degrees Celsius. |",
                   "\n#### SetTarget(double)\n",
                   "public void SetTarget(double celsius)",
                   "| celsius | double | The new target temperature in degrees Celsius. |",
                   "\n#### Read()\n",
                   "public Reading Read()",
                   "**Returns:** The current `Reading`.");
    }

    /// <summary>
    ///     Checks that <c>apimark dotnet ...</c> with the default gradual-disclosure format writes the
    ///     index, namespace, type, and member pages of the sample assembly, with the expected tables
    ///     and cross-links, and that every link between the pages resolves.
    /// </summary>
    /// <returns><see langword="null"/> on success; otherwise the failure message.</returns>
    private static string? GradualDisclosureOutputTest()
    {
        using var workspace = new TemporaryDirectory();
        var (assemblyPath, xmlDocPath) = WriteSampleAssembly(workspace.DirectoryPath);
        var output = workspace.PathOf("api");

        var run = RunApiMark(workspace, "dotnet", "--assembly", assemblyPath, "--xml-doc", xmlDocPath, "--output", output);

        return CheckExitCode(run, 0)
               ?? CheckFiles(
                   output,
                   "api.md",
                   "ApiMarkSample.md",
                   "ApiMarkSample/Reading.md",
                   "ApiMarkSample/Reading/Reading.md",
                   "ApiMarkSample/Reading/Celsius.md",
                   "ApiMarkSample/Thermostat.md",
                   "ApiMarkSample/Thermostat/Thermostat.md",
                   "ApiMarkSample/Thermostat/SetTarget.md",
                   "ApiMarkSample/Thermostat/Read.md")
               ?? CheckContains(
                   "api.md",
                   File.ReadAllText(Path.Join(output, "api.md")),
                   "# ApiMarkSample API Reference\n",
                   "| [ApiMarkSample](ApiMarkSample.md) | 2 | [Reading](ApiMarkSample/Reading.md), [Thermostat](ApiMarkSample/Thermostat.md) |")
               ?? CheckContains(
                   "ApiMarkSample.md",
                   File.ReadAllText(Path.Join(output, "ApiMarkSample.md")),
                   "| [Reading](ApiMarkSample/Reading.md) | A single temperature reading. |",
                   "| [Thermostat](ApiMarkSample/Thermostat.md) | Regulates a heater to hold a target temperature. |")
               ?? CheckContains(
                   "ApiMarkSample/Thermostat.md",
                   File.ReadAllText(Path.Join(output, "ApiMarkSample", "Thermostat.md")),
                   "# Thermostat\n",
                   "[API Reference](../api.md) › [ApiMarkSample](../ApiMarkSample.md)",
                   "| [Thermostat(double initialTarget)](Thermostat/Thermostat.md) | Creates a thermostat. |",
                   "| [Read()](Thermostat/Read.md) | [Reading](Reading.md) | Takes a reading from the sensor. |",
                   "| [SetTarget(double celsius)](Thermostat/SetTarget.md) | void | Sets the target temperature. |")
               ?? CheckContains(
                   "ApiMarkSample/Thermostat/Read.md",
                   File.ReadAllText(Path.Join(output, "ApiMarkSample", "Thermostat", "Read.md")),
                   "# Read()\n",
                   "[API Reference](../../api.md) › [ApiMarkSample](../../ApiMarkSample.md) › [Thermostat](../Thermostat.md)",
                   "public Reading Read()",
                   "**Returns:** The current [`Reading`](../Reading.md).")
               ?? CheckLinksResolve(output);
    }
}
