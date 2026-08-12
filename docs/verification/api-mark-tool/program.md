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
- `apimark dotnet --exclude <pattern>` is accepted and omits the matching type's page from
  generated output while still returning exit code zero.
- Invalid subcommands and missing required arguments return a non-zero exit code with descriptive
  error text.
- Invalid visibility option values are rejected with a non-zero exit code.
- Missing input files produce a non-zero exit code and a clear file-not-found diagnostic.
- `--help` before and after a subcommand both display usage information and return exit code zero.
- `--silent` suppresses console output; `--log <file>` captures output to a file.
- `--validate` runs self-validation tests and returns exit code zero when all pass.
- `--validate --results <file>` additionally writes the results to the specified file path.
- The `vhdl` subcommand rejects invocations without at least one non-exclusion `--source` pattern.
- `--format single-file --depth 4` returns a non-zero exit code with a diagnostic naming `--depth`
  because member headings in single-file output are at `depth+3` and depth 4 would produce H7.
- `--format gradual --depth 4` is accepted and returns exit code zero (the single-file depth
  constraint does not apply to the gradual-disclosure format).
- `apimark dotnet --enforce-docs <tier>` with `--enforce-docs-severity Warning` (the default)
  reports undocumented API items to the console but still returns exit code zero.
- `apimark dotnet --enforce-docs <tier> --enforce-docs-severity Error` returns a non-zero
  exit code when violations are found, and the error message reports the undocumented count.
- `apimark dotnet --enforce-docs <tier> --enforce-docs-severity Error` returns exit code zero
  when no violations are found, even though severity is `Error`.
- An invalid `--enforce-docs` value returns a non-zero exit code with a diagnostic naming
  the invalid value, for the `dotnet`, `cpp`, and `vhdl` subcommands alike.
- An invalid `--enforce-docs-severity` value returns a non-zero exit code with a diagnostic
  naming the invalid value.
- `apimark vhdl --enforce-docs <tier>` with `--enforce-docs-severity Warning` (the default)
  reports undocumented VHDL entities/ports/generics/packages to the console but still
  returns exit code zero.
- `apimark vhdl --enforce-docs <tier> --enforce-docs-severity Error` returns a non-zero exit
  code when violations are found, and returns exit code zero when no violations are found,
  mirroring the dotnet behavior via the same `IDocumentationCoverageCapable` dispatch path.

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

**Silent and log options produce log file**: Verifies that `--silent` suppresses all console output
(stdout and stderr are both empty) and `--log <file>` creates a non-empty log file containing the
captured output. This scenario is tested by `Program_Main_WithSilentAndLog_DotNetCommand_ExitsZero`.

**Validate flag runs self-validation and exits zero**: Verifies that `--validate` executes internal
self-validation tests and returns exit code zero when all tests pass. This scenario is tested by
`Program_Main_WithValidateFlag_ExitsZero`.

**Validate flag with results file writes results**: Verifies that `--validate --results <file>`
executes internal self-validation tests, writes a results file to the specified path, and returns
exit code zero. This scenario is tested by `Program_Main_WithValidateAndResultsFile_WritesResultsFile`.

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

**cpp subcommand with --api-headers flag is accepted**: Verifies that the `cpp`
subcommand accepts `--api-headers` without raising an unknown-flag error. The test
invokes `cpp --api-headers **/*.h --output out/` without `--includes`, expects a
non-zero exit code due to the missing `--includes` requirement, and confirms the
error message references `--includes` (not an unknown-flag error), proving the flag
was parsed successfully. This scenario is tested by
`Program_Main_CppWithApiHeadersFlag_FlagIsAccepted`.

**vhdl subcommand without --source returns non-zero exit code**: Verifies that invoking the `vhdl`
subcommand without any `--source` arguments returns a non-zero exit code and a descriptive error
message, confirming that the subcommand enforces the requirement for at least one non-exclusion
source pattern before attempting generation. This scenario is tested by
`Program_Main_WithVhdlSubcommand_MissingSourceFiles_ReturnsNonZeroExitCode`.

**Single-file format with depth 4 returns non-zero exit code**: Verifies that supplying
`--format single-file --depth 4` produces a non-zero exit code and a diagnostic naming
`--depth`, confirming that the cross-argument depth constraint is enforced in `RunToolLogic`
after both flags are known. The constraint exists because single-file emitters render member
headings at `depth+3`; depth 4 would produce H7+, which CommonMark does not support. This
scenario is tested by `Program_Main_WithSingleFileFormatAndDepth4_ReturnsNonZeroExitCode`.

**Gradual-disclosure format with depth 4 exits zero**: Verifies that supplying
`--format gradual --depth 4` with a valid dotnet subcommand exits with code zero, confirming
that the single-file depth constraint is not applied to the gradual-disclosure format. This
scenario is tested by `Program_Main_WithGradualFormatAndDepth4_ExitsZero`.

**Enforce-docs with Warning severity reports violations but exits zero**: Verifies that
`apimark dotnet --enforce-docs <tier>` with the default `Warning` severity prints the
documentation-coverage scan (including undocumented item lines and the summary counts) to
stdout but still returns exit code zero, confirming that warn-only enforcement never fails
an otherwise-successful build. This scenario is tested by
`Program_Main_EnforceDocsWarningSeverity_ReportsViolationsButExitsZero`.

**Enforce-docs with Error severity returns non-zero exit code**: Verifies that
`apimark dotnet --enforce-docs <tier> --enforce-docs-severity Error` returns a non-zero exit
code and writes an error message reporting the undocumented count when violations are found,
confirming that error-severity enforcement gates the build via the existing
`Context.WriteError`/`ExitCode` mechanism. This scenario is tested by
`Program_Main_EnforceDocsErrorSeverity_ReturnsNonZeroExitCode`.

**Enforce-docs with no violations exits zero and reports zero undocumented**: Verifies that
when the scoped scan finds no undocumented items, the tool exits zero and reports zero
undocumented items even when severity is `Error`, confirming that `WriteError` is only
invoked when `HasViolations` is `true`. This scenario is tested by
`Program_Main_EnforceDocsNoViolations_ExitsZeroAndReportsZeroUndocumented`.

**Invalid --enforce-docs value returns non-zero exit code**: Verifies that an unrecognized
`--enforce-docs` visibility value causes `CreateGenerator` to throw `ArgumentException`,
which is caught and routed to `context.WriteError`, and that the resulting error message
includes the invalid value supplied. This scenario is tested by
`Program_Main_WithInvalidEnforceDocsValue_ReturnsNonZeroExitCode`.

**Invalid --enforce-docs-severity value returns non-zero exit code**: Verifies that an
unrecognized `--enforce-docs-severity` value causes `ReportDocumentationCoverage` to throw
`ArgumentException`, which is caught and routed to `context.WriteError`, and that the
resulting error message includes the invalid value supplied. This scenario is tested by
`Program_Main_WithInvalidEnforceDocsSeverityValue_ReturnsNonZeroExitCode`.

**Invalid --enforce-docs value is rejected for the cpp subcommand**: Verifies that an
unrecognized `--enforce-docs` value supplied alongside the `cpp` subcommand is validated in
`CreateGenerator` before `Parse` runs (and therefore before clang is ever invoked), exiting
with a non-zero code and an error message naming the invalid value, confirming that
documentation-coverage enforcement dispatches through `IDocumentationCoverageCapable`
uniformly rather than being dotnet-only. This scenario is tested by
`Program_Main_Cpp_WithInvalidEnforceDocsValue_ReturnsNonZeroExitCode`.

**Enforce-docs with Warning severity reports VHDL violations but exits zero**: Verifies that
`apimark vhdl --enforce-docs <tier>` with the default `Warning` severity prints the
documentation-coverage scan for undocumented entities/ports/generics/packages to stdout but
still returns exit code zero, mirroring the dotnet behavior through the same
`IDocumentationCoverageCapable` dispatch path. This scenario is tested by
`Program_Main_Vhdl_EnforceDocsWarningSeverity_ReportsViolationsButExitsZero`.

**Enforce-docs with Error severity returns non-zero exit code for VHDL**: Verifies that
`apimark vhdl --enforce-docs <tier> --enforce-docs-severity Error` returns a non-zero exit
code when violations are found, confirming that error-severity enforcement gates the VHDL
build the same way it gates the dotnet build. This scenario is tested by
`Program_Main_Vhdl_EnforceDocsErrorSeverity_ReturnsNonZeroExitCode`.

**Enforce-docs with no VHDL violations exits zero and reports zero undocumented**: Verifies
that when the scoped VHDL scan finds no undocumented items, the tool exits zero and reports
zero undocumented items even when severity is `Error`. This scenario is tested by
`Program_Main_Vhdl_EnforceDocsNoViolations_ExitsZeroAndReportsZeroUndocumented`.

**Invalid --enforce-docs value returns non-zero exit code for VHDL**: Verifies that supplying
an unrecognized `--enforce-docs` value for the `vhdl` subcommand exits with a non-zero code
and writes an error message containing the invalid value, mirroring the dotnet and cpp
validation behavior. This scenario is tested by
`Program_Main_Vhdl_WithInvalidEnforceDocsValue_ReturnsNonZeroExitCode`.

**All three VHDL enforcement tiers are accepted and behave identically**: Verifies that
`Public`, `PublicAndProtected`, and `All` are all accepted for the `vhdl` subcommand and
report the same undocumented count, since VHDL has no visibility/accessibility concept and
accepts the shared three-word vocabulary purely for CLI consistency. This scenario is tested
by `Program_Main_Vhdl_AllThreeEnforceTierValues_AreAcceptedAndReportSameCount`.
