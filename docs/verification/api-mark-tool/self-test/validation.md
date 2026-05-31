### Validation

#### Verification Approach

`Validation` is verified through unit tests in
`test/ApiMark.Tool.Tests/SelfTest/ValidationTests.cs` that invoke
`Validation.Run(context)` with a real `Context` instance. `--silent` suppresses
console output and `--log <tempFile>` captures output for inspection. No mocking
is used; tests exercise the full `Validation.Run` path including child context
creation and `Program.Run` dispatch.

#### Test Environment

Standard .NET test runner. Tests write temporary files to `Path.GetTempPath()` and
delete them after assertions. No external services, network access, or privileged
configuration is required.

#### Acceptance Criteria

- All `ValidationTests` tests pass with zero failures.
- `Validation.Run` returns with `ExitCode = 0` when all self-tests pass.
- A `.trx` results file is created and contains TRX XML when `--results *.trx` is used.
- A `.xml` results file is created when `--results *.xml` is used.
- An unsupported results extension causes `ExitCode = 1`.
- Output log always mentions both `"ApiMark_VersionDisplay"` and `"ApiMark_HelpDisplay"`.

#### Test Scenarios

**`Validation_Run_WithValidContext_ExitsZero`**: `Context.Create(["--validate", "--silent"])` +
`Validation.Run(context)` → `ExitCode = 0`.

**`Validation_Run_WithResultsTrxFile_CreatesTrxFile`**: `--results *.trx` →
file created, contains `"TestRun"`.

**`Validation_Run_WithResultsXmlFile_CreatesXmlFile`**: `--results *.xml` →
file created.

**`Validation_Run_WithUnsupportedResultsExtension_SetsExitCodeToOne`**: `--results *.json`
→ `ExitCode = 1`.

**`Validation_Run_WritesVersionAndHelpTestResults`**: log output contains both
`"ApiMark_VersionDisplay"` and `"ApiMark_HelpDisplay"`.

**`Validation_Run_NullContext_ThrowsArgumentNullException`**: `Validation.Run(null)` →
`ArgumentNullException` thrown.
