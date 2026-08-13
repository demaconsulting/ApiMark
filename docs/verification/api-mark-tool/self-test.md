## SelfTest

### Verification Approach

The SelfTest subsystem is verified through unit tests in
`test/ApiMark.Tool.Tests/SelfTest/ValidationTests.cs` that call `Validation.Run(context)`
directly with a real `Context` instance and assert on exit code, results file
creation, and output content. All tests use `--silent` to suppress console output; selected tests also use
`--log <tempFile>` to capture log output for inspection. No mocking is used;
tests exercise the real dispatch path end to end.

### Test Environment

Standard .NET test runner. Tests write temporary log and results files to
`Path.GetTempPath()` and delete them after assertions. No external services,
privileged configuration, or network access is required.

### Acceptance Criteria

- All `ValidationTests` tests pass with zero failures.
- `Validation.Run` exits with code 0 when all self-tests pass or are skipped.
- `.trx` results files are created and contain TRX XML content.
- `.xml` results files are created.
- Unsupported results file extensions set `ExitCode` to 1.
- Output mentions `ApiMark_VersionDisplay`, `ApiMark_HelpDisplay`,
  `ApiMark_DotNetGeneration`, `ApiMark_CppGeneration`, and
  `ApiMark_VhdlGeneration`.
- The C++ functional test is recorded as skipped (`TestOutcome.NotExecuted`),
  not failed, when clang cannot be located, and does not set `ExitCode` to 1.
- The summary output includes a `Skipped: N` line.

### Test Scenarios

**Self-validation with valid context exits zero**: Verifies that
`Validation.Run` completes with `ExitCode = 0` when both internal self-tests
pass. Tested by `Validation_Run_WithValidContext_ExitsZero`.

**TRX results file is created**: Verifies that when `--results` specifies a
`.trx` path, the file is created and contains `"TestRun"` and all five
self-test results, including the C++ test's `Passed` or `NotExecuted`
outcome. Tested by `Validation_Run_WithResultsTrxFile_CreatesTrxFile`.

**XML results file is created**: Verifies that when `--results` specifies a
`.xml` path, the file is created and contains all five self-test results.
Tested by `Validation_Run_WithResultsXmlFile_CreatesXmlFile`.

**Unsupported extension sets exit code to 1**: Verifies that a `.json`
extension causes `WriteError` to be called and `ExitCode` to be `1`. Tested
by `Validation_Run_WithUnsupportedResultsExtension_SetsExitCodeToOne`.

**Output mentions both self-test names**: Verifies that the log output
contains both `"ApiMark_VersionDisplay"` and `"ApiMark_HelpDisplay"`. Tested
by `Validation_Run_WritesVersionAndHelpTestResults`.

**Output mentions all three functional generation test names**: Verifies
that the log output contains `"ApiMark_DotNetGeneration"`,
`"ApiMark_CppGeneration"`, and `"ApiMark_VhdlGeneration"`. Tested by
`Validation_Run_WritesFunctionalGenerationTestResults`.

**Summary includes a Skipped count line**: Verifies that the summary output
always includes a line matching `Skipped: \d+`, regardless of clang
availability. Tested by `Validation_Run_WritesSkippedSummaryLine`.

**C++ generation test is skipped, not failed, when clang is unavailable**:
Verifies that forcing clang discovery to fail (by pointing
`APIMARK_CLANG_PATH` at a nonexistent path) records `ApiMark_CppGeneration`
with `TestOutcome.NotExecuted` and leaves `context.ExitCode` at `0`. Tested
by `Validation_Run_CppGenerationSkippedWhenClangUnavailable`.
