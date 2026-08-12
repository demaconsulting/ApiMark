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
- Documentation coverage enforcement (`--enforce-docs` / `--enforce-docs-severity`) reports
  undocumented items and exits zero at `Warning` severity, fails the build at `Error` severity
  when violations are found, exits zero with zero reported violations for a fully documented
  scope, rejects invalid `--enforce-docs` / `--enforce-docs-severity` values with a non-zero
  exit code, and is a graceful no-op (informational note, no build failure) for `cpp` and `vhdl`.

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

**Documentation coverage enforcement reports violations at Warning severity without failing the
build**: Verifies that `--enforce-docs` with the default `Warning` severity writes each
undocumented item and a summary count to standard output but still exits 0. This scenario is
tested by `Program_Main_EnforceDocsWarningSeverity_ReportsViolationsButExitsZero`.

**Documentation coverage enforcement fails the build at Error severity**: Verifies that
`--enforce-docs-severity Error` causes a non-zero exit code and an error message on standard
error when undocumented items are found. This scenario is tested by
`Program_Main_EnforceDocsErrorSeverity_ReturnsNonZeroExitCode`.

**Documentation coverage enforcement exits zero for a fully documented scope**: Verifies that
`--enforce-docs` with `--enforce-docs-severity Error` still exits 0 and reports zero undocumented
items when the scanned scope is fully documented, confirming the build only fails when
violations actually exist. This scenario is tested by
`Program_Main_EnforceDocsNoViolations_ExitsZeroAndReportsZeroUndocumented`.

**Invalid `--enforce-docs` value is rejected**: Verifies that an unrecognized `--enforce-docs`
visibility-tier value exits with a non-zero code and an error message naming the invalid value.
This scenario is tested by `Program_Main_WithInvalidEnforceDocsValue_ReturnsNonZeroExitCode`.

**Invalid `--enforce-docs-severity` value is rejected**: Verifies that an unrecognized
`--enforce-docs-severity` value exits with a non-zero code and an error message naming the
invalid value. This scenario is tested by
`Program_Main_WithInvalidEnforceDocsSeverityValue_ReturnsNonZeroExitCode`.

**Documentation coverage enforcement is a graceful no-op for cpp**: Verifies that supplying
`--enforce-docs` for the `cpp` subcommand prints an informational note rather than failing an
otherwise-valid build. This scenario is tested by
`Program_Main_EnforceDocsWithCppSubcommand_PrintsInformationalNote`.

**Invalid `--enforce-docs` value does not affect cpp/vhdl builds**: Verifies that an unrecognized
`--enforce-docs` value is never parsed or validated for the `cpp` (or `vhdl`) subcommand — the
informational note is printed and the invalid value never appears in error output, confirming
the flag is inert outside `dotnet`. This scenario is tested by
`Program_Main_InvalidEnforceDocsValueWithCppSubcommand_DoesNotThrowForEnforceDocs`.
