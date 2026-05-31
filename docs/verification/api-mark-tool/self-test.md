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
- `Validation.Run` exits with code 0 when all self-tests pass.
- `.trx` results files are created and contain TRX XML content.
- `.xml` results files are created.
- Unsupported results file extensions set `ExitCode` to 1.
- Output mentions both `ApiMark_VersionDisplay` and `ApiMark_HelpDisplay`.

### Test Scenarios

**Self-validation with valid context exits zero**: Verifies that
`Validation.Run` completes with `ExitCode = 0` when both internal self-tests
pass. Tested by `Validation_Run_WithValidContext_ExitsZero`.

**TRX results file is created**: Verifies that when `--results` specifies a
`.trx` path, the file is created and contains `"TestRun"`. Tested by
`Validation_Run_WithResultsTrxFile_CreatesTrxFile`.

**XML results file is created**: Verifies that when `--results` specifies a
`.xml` path, the file is created. Tested by
`Validation_Run_WithResultsXmlFile_CreatesXmlFile`.

**Unsupported extension sets exit code to 1**: Verifies that a `.json`
extension causes `WriteError` to be called and `ExitCode` to be `1`. Tested
by `Validation_Run_WithUnsupportedResultsExtension_SetsExitCodeToOne`.

**Output mentions both self-test names**: Verifies that the log output
contains both `"ApiMark_VersionDisplay"` and `"ApiMark_HelpDisplay"`. Tested
by `Validation_Run_WritesVersionAndHelpTestResults`.
