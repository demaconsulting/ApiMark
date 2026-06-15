### Validation

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

#### Purpose

Validation runs in-process self-tests that confirm core ApiMarkTool
functionality works correctly in the deployment environment. Each test
invokes the tool's own dispatch path through a child `Context` instance,
captures output to a temporary log file, and checks the log content against
expected patterns. Results are accumulated in a `TestResults` collection and
optionally written to a `.trx` or `.xml` file.

#### Data Model

N/A — Validation is a static class with no instance state. It uses local
variables within each method for test results and temporary file paths.

All test result objects are `DemaConsulting.TestResults.TestResult` instances
created via `CreateTestResult(testName)` and collected in a
`DemaConsulting.TestResults.TestResults` object named
`"ApiMark Tool Self-Validation"`.

#### Key Methods

**Validation.Run(Context context)** — Public static entry point.

- *Parameters*: `Context context` — the active program context.
- *Returns*: `void`
- *Algorithm*:
  1. Print validation header (tool version, machine name, OS, runtime, timestamp).
  2. Create a `TestResults` collection.
  3. Call `RunVersionTest(context, testResults)`.
  4. Call `RunHelpTest(context, testResults)`.
  5. Print summary (total, passed, failed counts).
  6. If `context.ResultsFile` is set, call `WriteResultsFile`.
- *Preconditions*: `context` must be non-null.
- *Postconditions*: All self-tests have run; results are written to context;
  `context.ExitCode` is `1` if any test failed.

**RunVersionTest(Context context, TestResults testResults)** — Private static.

- Creates a `TemporaryDirectory` to hold the log file; the directory is
  disposed automatically on exit from the method.
- Creates a child context with `["--silent", "--log", logFile, "--version"]`
  where `logFile` is a path inside the temporary directory.
- Calls `Program.Run(testContext)` and checks exit code.
- Reads the log file and verifies it contains a version number pattern
  (`\b\d+\.\d+\.\d+`).
- Appends a `TestResult` named `"ApiMark_VersionDisplay"` to `testResults`.

**RunHelpTest(Context context, TestResults testResults)** — Private static.

- Creates a `TemporaryDirectory` to hold the log file; the directory is
  disposed automatically on exit from the method.
- Creates a child context with `["--silent", "--log", logFile, "--help"]`
  where `logFile` is a path inside the temporary directory.
- Calls `Program.Run(testContext)` and checks exit code.
- Reads the log file and verifies it contains both `"Usage:"` and
  `"Options:"`.
- Appends a `TestResult` named `"ApiMark_HelpDisplay"` to `testResults`.

**WriteResultsFile(Context context, TestResults testResults)** — Private static.

- Checks `context.ResultsFile` extension (case-insensitive).
- `.trx` → serializes using `TrxSerializer.Serialize`.
- `.xml` → serializes using `JUnitSerializer.Serialize`.
- Any other extension → calls `context.WriteError` and returns.
- Writes the serialized content to the file; on I/O failure calls
  `context.WriteError`.

**PrintValidationHeader(Context context)** — Private static helper.

- Writes a markdown heading (level driven by `context.HeadingDepth`) followed
  by a table showing tool version, machine name, OS description, .NET runtime
  description, and UTC timestamp.

**CreateTestResult(string testName)** — Private static helper.

- Returns a new `TestResult` pre-populated with `Name = testName`,
  `ClassName = "Validation"`, and `CodeBase = "ApiMark.Tool"`.

**FinalizeTestResult(TestResult test, DateTime startTime, TestResults testResults)** — Private static helper.

- Sets `test.Duration` to `DateTime.UtcNow − startTime` and appends `test`
  to `testResults.Results`.

**HandleTestException(TestResult test, Context context, string testName, Exception ex)** — Private static helper.

- Sets `test.Outcome` to `Failed`, records `ex.Message` in
  `test.ErrorMessage`, and calls `context.WriteError` with a diagnostic line.
  Called from the generic `catch` blocks in `RunVersionTest` and `RunHelpTest`.

**TemporaryDirectory** — Private sealed nested class implementing `IDisposable`.

- On construction, creates a uniquely named directory under
  `Path.GetTempPath()`.
- On `Dispose`, deletes the directory and its entire subtree; I/O and access
  errors during deletion are silently swallowed (best-effort cleanup).
- Used by `RunVersionTest` and `RunHelpTest` via `using var`, guaranteeing
  log-file cleanup even when those methods throw.

#### Error Handling

- Throws `ArgumentNullException` when `context` is null.
- Failed self-tests call `context.WriteError` for each failure, setting
  `context.ExitCode` to `1`.
- Unsupported results file extension calls `context.WriteError` and returns
  without writing a file.
- I/O errors during results file write call `context.WriteError`.
- Any exception within a test method is caught, recorded as a failed test,
  and reported via `context.WriteError`. This generic catch is intentional
  to ensure the test framework remains operational even if an individual test
  throws unexpectedly.

#### Dependencies

- **Context** (Cli subsystem) — used for output routing and to create
  child contexts for each self-test.
- **Program** — called with child contexts to exercise the dispatch path.
- **DemaConsulting.TestResults** — `TestResults`, `TestResult`, `TestOutcome`,
  `TrxSerializer`, and `JUnitSerializer` are used to build, accumulate, and
  serialize test results.

#### Callers

- **Program.Run** — calls `Validation.Run(context)` when `context.Validate`
  is `true` (priority 3 in the dispatch chain).
