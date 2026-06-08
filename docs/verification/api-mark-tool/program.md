## Program

### Verification Approach

`Program` is the CLI entry point for the `apimark` tool. It is verified with integration tests in
`test/ApiMark.Tool.Tests/` that invoke the entry point through the .NET process API or test host,
passing representative command-line argument arrays and asserting on exit code, standard output,
standard error, and the presence of generated output files. The real argument parser and real
generator dispatch are used; no components are mocked. This confirms that argument parsing,
subcommand routing, option forwarding, and error reporting are all correct for the shipped binary.

### Test Environment

Tests require the .NET SDK, compiled sample assemblies with XML documentation files, and a writable
output directory. No external service, privileged configuration, or network access is required.

### Acceptance Criteria

- All `Program` integration tests pass with zero failures.
- `apimark dotnet` with valid arguments generates the expected output and returns exit code zero.
- Invalid subcommands and missing required arguments return a non-zero exit code with descriptive
  error text.
- Invalid visibility option values are rejected with a non-zero exit code.
- Missing input files produce a non-zero exit code and a clear file-not-found diagnostic.
- `--help` before and after a subcommand both display usage information and return exit code zero.
- `--silent` suppresses console output; `--log <file>` captures output to a file.
- `--validate` runs self-validation tests and returns exit code zero when all pass.

### Test Scenarios

**DotNet subcommand with valid arguments generates expected output**: Verifies that invoking
`apimark dotnet --assembly <path> --output <dir>` produces the expected Markdown tree and returns
exit code zero, confirming that the full CLI path from argument parsing through generator dispatch
to file emission is operational. This scenario is tested by
`Program_Main_DotNetCommand_GeneratesExpectedOutput`.

**Invalid visibility value returns non-zero exit code**: Verifies that passing an unrecognized value
to `--visibility` causes the CLI to report an error and exit with a non-zero code before attempting
generation, so users receive immediate actionable feedback. This scenario is tested by
`Program_Main_WithInvalidVisibility_ReturnsNonZeroExitCode`.

**Missing assembly path returns non-zero exit code with diagnostic**: Verifies that the CLI does
not attempt generation when the specified assembly file does not exist and instead prints a clear
file-not-found message and exits with a non-zero code. This scenario is tested by
`Program_Main_WithMissingAssembly_PrintsErrorAndFails`.

**Help flag displays usage and exits zero**: Verifies that `--help` prints usage information
including "Usage:" and "Options:" sections and returns exit code zero. This scenario is tested by
`Program_Main_WithHelpFlag_PrintsHelpAndExitsZero`.

**Help flag after subcommand displays usage and exits zero**: Verifies that the single-pass parser
accepts `--help` after the language token and still dispatches to help display. This scenario is
tested by `Program_Main_WithHelpAfterSubcommand_PrintsHelpAndExitsZero`.

**Silent and log options produce log file**: Verifies that `--silent` suppresses console output and
`--log <file>` creates a non-empty log file containing the captured output. This scenario is tested
by `Program_Main_WithSilentAndLog_DotNetCommand_ExitsZero`.

**Validate flag runs self-validation and exits zero**: Verifies that `--validate` executes internal
self-validation tests and returns exit code zero when all tests pass. This scenario is tested by
`Program_Main_WithValidateFlag_ExitsZero`.

**Version flag prints version and exits zero**: Verifies that `--version` prints a non-empty version
string to stdout and returns exit code zero without printing the application banner. This scenario is
tested by `Program_Main_WithVersionFlag_PrintsVersionAndExitsZero`.

**No arguments returns non-zero exit code**: Verifies that invoking the tool with no arguments
returns a non-zero exit code because no language subcommand was specified, confirming the tool
enforces the required subcommand positional argument. This scenario is tested by
`Program_Main_WithNoArguments_ReturnsNonZeroExitCode`.

**cpp subcommand with missing --includes returns non-zero exit code**: Verifies that invoking the
`cpp` subcommand without the required `--includes` option returns a non-zero exit code and a
descriptive error message, confirming that required option validation is enforced for the cpp
language. This scenario is tested by
`Program_Main_WithCppSubcommand_MissingIncludes_ReturnsNonZeroExitCode`.
