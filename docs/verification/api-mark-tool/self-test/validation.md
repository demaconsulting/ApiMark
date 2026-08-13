### Validation

#### Verification Approach

`Validation` is verified through unit tests in
`test/ApiMark.Tool.Tests/SelfTest/ValidationTests.cs` that invoke
`Validation.Run(context)` with a real `Context` instance. `--silent` suppresses
console output and `--log <tempFile>` captures output for inspection. No mocking
is used except for one scenario that overrides the `APIMARK_CLANG_PATH`
environment variable with a nonexistent path (restored in a `finally` block) to
force the clang-unavailable skip path deterministically. Tests exercise the full
`Validation.Run` path, including the real `DotNetGenerator`, `CppGenerator`, and
`VhdlGenerator` pipelines invoked by the functional generation self-tests, and
the `CheckDocumentationCoverage` call on each of those three generators invoked
by the three functional enforcement self-tests.

#### Test Environment

Standard .NET test runner. Tests write temporary files to `Path.GetTempPath()` and
delete them after assertions. No external services, network access, or privileged
configuration is required.

#### Acceptance Criteria

- All `ValidationTests` tests pass with zero failures.
- `Validation.Run` returns with `ExitCode = 0` when all self-tests pass or are skipped.
- A `.trx` results file is created and contains TRX XML when `--results *.trx` is used,
  and round-trips all eight test names and outcomes.
- A `.xml` results file is created when `--results *.xml` is used, and round-trips all
  eight test names and outcomes.
- An unsupported results extension causes `ExitCode = 1`.
- Output log always mentions `"ApiMark_VersionDisplay"`, `"ApiMark_HelpDisplay"`,
  `"ApiMark_DotNetGeneration"`, `"ApiMark_CppGeneration"`, `"ApiMark_VhdlGeneration"`,
  `"ApiMark_DotNetEnforceDocs"`, `"ApiMark_CppEnforceDocs"`, and
  `"ApiMark_VhdlEnforceDocs"`.
- Output summary always includes a `"Skipped: N"` line.
- The `ApiMark_CppGeneration` and `ApiMark_CppEnforceDocs` tests are recorded as
  `NotExecuted` (skipped), not `Failed`, and do not affect `ExitCode`, when clang
  cannot be located.
- The `ApiMark_DotNetEnforceDocs` test runs the checker to completion against the
  tool's own assembly and reports internally consistent counts, and passes.
- The `ApiMark_CppEnforceDocs` and `ApiMark_VhdlEnforceDocs` tests detect the
  intentionally undocumented declaration in their embedded sample sources and pass.

#### Test Scenarios

**`Validation_Run_WithValidContext_ExitsZero`**: `Context.Create(["--validate", "--silent"])` +
`Validation.Run(context)` → `ExitCode = 0`.

**`Validation_Run_WithResultsTrxFile_CreatesTrxFile`**: `--results *.trx` →
file created; deserializes to 8 results including `ApiMark_DotNetGeneration`,
`ApiMark_VhdlGeneration`, `ApiMark_DotNetEnforceDocs`, and `ApiMark_VhdlEnforceDocs`
as `Passed`, and `ApiMark_CppGeneration`/`ApiMark_CppEnforceDocs` as `Passed` or
`NotExecuted` depending on clang availability.

**`Validation_Run_WithResultsXmlFile_CreatesXmlFile`**: `--results *.xml` →
file created; deserializes with the same 8-result content as the TRX scenario above.

**`Validation_Run_WithUnsupportedResultsExtension_SetsExitCodeToOne`**: `--results *.json`
→ `ExitCode = 1`.

**`Validation_Run_WritesVersionAndHelpTestResults`**: log output contains both
`"ApiMark_VersionDisplay"` and `"ApiMark_HelpDisplay"`.

**`Validation_Run_WritesFunctionalGenerationTestResults`**: log output contains all
three functional generation test names (`ApiMark_DotNetGeneration`,
`ApiMark_CppGeneration`, `ApiMark_VhdlGeneration`) and all three functional
enforcement test names (`ApiMark_DotNetEnforceDocs`, `ApiMark_CppEnforceDocs`,
`ApiMark_VhdlEnforceDocs`).

**`Validation_Run_WritesSkippedSummaryLine`**: summary output matches `Skipped: \d+`
regardless of clang availability.

**`Validation_Run_CppTestsSkippedWhenClangUnavailable`**: forces clang discovery to
fail by overriding `APIMARK_CLANG_PATH` with a nonexistent path →
`ApiMark_CppGeneration` and `ApiMark_CppEnforceDocs` both recorded as
`NotExecuted` and `ExitCode` remains `0`.

**`Validation_Run_DotNetEnforceDocsTestPasses`**: `--results *.trx` →
`ApiMark_DotNetEnforceDocs` recorded as `Passed` and `ExitCode` remains `0`.

**`Validation_Run_VhdlEnforceDocsTestPasses`**: `--results *.trx` →
`ApiMark_VhdlEnforceDocs` recorded as `Passed` and `ExitCode` remains `0`.

**`Validation_Run_NullContext_ThrowsArgumentNullException`**: `Validation.Run(null)` →
`ArgumentNullException` thrown.
