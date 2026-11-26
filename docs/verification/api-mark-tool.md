# ApiMarkTool

## Verification Approach

ApiMark.Tool is verified with CLI integration tests in `test/ApiMark.Tool.Tests/` that invoke the
`ApiMark.Tool.dll` entry point against representative inputs and assert on exit code, console
diagnostics, and emitted Markdown files. Tests use real command-line parsing and real generator
dispatch so they confirm that the shipped CLI correctly selects the generation path, validates
arguments, and reports failures in a way that users and build pipelines can act on. No internal
components are mocked; the integration path runs end to end.

The built-in `help` command is verified out of process by `IntegrationTests.cs`, which runs the
compiled `ApiMark.Tool.dll` with `dotnet` through `Runner.Run` and asserts on the exit code and
the captured output. The command examples of the help cards are verified with the example runner
of `DemaConsulting.HelpCards`: it gives each card a fresh scratch folder and calls a test delegate
for each command example. The delegate copies the input files the examples read into that folder
— `test/ApiMark.Tool.Tests/TestData/HelpExamples/counter.vhd`, and the self-validation sample
assembly written by `Validation.WriteSampleAssembly`, copied as `MyLib.dll` and `MyLib.xml` —
unless the card saved a file of the same name from its own source example. It then runs the
compiled tool there with `Runner.RunInDirectory`, and the runner compares the exit code and the
output with the `exit` and `expect` values of the example. The C++ generation examples are
snippets that the runner does not execute, because they need clang.

## Test Environment

Tests require the .NET SDK, compiled sample assemblies with XML documentation files, and a writable
output directory. No external service, privileged machine configuration, or network access is
required. The help tests also need the `dotnet` host on the `PATH` to run the compiled tool, and
the example runner writes a scratch folder per card in the temporary directory; clang is not
needed.

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
  exit code, and is dispatched polymorphically for `dotnet`, `cpp`, and `vhdl` alike via
  `IDocumentationCoverageCapable` rather than being a dotnet-only behavior.
- The `help` command lists the topics with exit code 0, and exits with code 2 for an unknown topic.
- Every command example in the help cards gives the exit code and output that its card states.

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

**Documentation coverage enforcement dispatches polymorphically to cpp and vhdl**: Verifies
that `--enforce-docs` for the `cpp` subcommand is fully enforced (not a no-op) via
`IDocumentationCoverageCapable`, returning a non-zero exit code for an invalid value exactly
like `dotnet`. This scenario is tested at the Program unit level by
`Program_Main_Cpp_WithInvalidEnforceDocsValue_ReturnsNonZeroExitCode` and the full suite of
`Program_Main_Vhdl_*` tests; see the *ApiMarkTool Program* section for the complete list.

**Help command lists the topics**: Invokes the compiled tool with `help` as the sole argument and
asserts that the exit code is 0, that the output lists every topic with its command, and that no
banner is printed. This scenario is tested by `ApiMarkTool_HelpTopics_HelpVerb_ListsTopics`.

**Unknown help topic returns the usage-error exit code**: Invokes the compiled tool with
`help no-such-topic` and asserts that the exit code is 2 and that the output names the topic.
This scenario is tested by `ApiMarkTool_HelpTopics_UnknownTopic_ReturnsUsageError`.

**Every help example gives its stated result**: Builds the catalog that the tool ships and runs
every command example of every card against the compiled tool in a scratch folder that holds the
staged input files; asserts that at least one command example exists and that each gives the exit
code and output fragment its card states. This scenario is tested by
`ApiMarkTool_HelpExamples_AllCommandExamples_GiveStatedResults`.
