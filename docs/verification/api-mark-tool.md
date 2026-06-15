# ApiMarkTool

## Verification Approach

ApiMark.Tool is verified with CLI integration tests in `test/ApiMark.Tool.Tests/` that invoke the
`ApiMark.Tool.dll` entry point against representative inputs and assert on exit code, console
diagnostics, and emitted Markdown files. Tests use real command-line parsing and real generator
dispatch so they confirm that the shipped CLI correctly selects the generation path, validates
arguments, and reports failures in a way that users and build pipelines can act on. No internal
components are mocked; the integration path runs end to end.

## Test Environment

Tests require the .NET SDK, compiled sample assemblies with XML documentation files, and a writable
output directory. No external service, privileged machine configuration, or network access is
required.

## Acceptance Criteria

- All ApiMark.Tool integration tests pass with zero failures.
- Valid arguments dispatch to the correct language generator and produce documentation.
- Invalid arguments and missing inputs return non-zero exit codes with actionable error text.
- Visibility option values are forwarded to the generator; invalid values are rejected with a non-zero exit
  code and an actionable error message.
- The `apimark dotnet` subcommand generates the expected Markdown tree for a sample assembly.
- The `vhdl` subcommand is verified at the Program unit level; see the *ApiMarkTool Program* section.

## Test Scenarios

**cpp subcommand dispatch is verified at the unit level via `ApiMarkTool-Program-SupportCppOptions` tests.**

**vhdl subcommand validation is verified at the Program unit level via `ApiMarkTool-Program` tests.**

**DotNet command generates documentation successfully**: Verifies that invoking `apimark dotnet`
with valid assembly, XML documentation, and output arguments produces the expected Markdown tree
for a sample assembly, confirming that CLI argument parsing, generator dispatch, and file emission
are all wired correctly. This scenario is tested by
`Program_Main_DotNetCommand_GeneratesExpectedOutput`.

**Invalid visibility values are rejected**: Verifies that unsupported visibility arguments fail fast
with a non-zero exit code and a clear diagnostic so users can correct the command line quickly
without needing to inspect generated output. This scenario is tested by
`Program_Main_WithInvalidVisibility_ReturnsNonZeroExitCode`.

**Missing assembly paths fail with actionable diagnostics**: Verifies that the CLI does not
dispatch into generation when required input files are missing and instead reports the problem
clearly with a non-zero exit code. This scenario is tested by
`Program_Main_WithMissingAssembly_PrintsErrorAndFails`.
