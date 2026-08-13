### Validation

![Validation Structure](SelfTestView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

#### Purpose

Validation runs in-process self-tests that confirm core ApiMarkTool
functionality works correctly in the deployment environment. Two
functionality tests invoke the tool's own dispatch path through a child
`Context` instance, capture output to a temporary log file, and check the
log content against expected patterns. Three functional generation tests
invoke each language generator's real `Parse`/`Emit` pipeline
(`DotNetGenerator`, `CppGenerator`, `VhdlGenerator`) directly against a tiny
embedded sample source, and verify the generated Markdown contains expected
content — mirroring `Program.RunToolLogic`. Three functional enforcement tests
invoke each language generator's real `CheckDocumentationCoverage` pipeline
(`DotNetGenerator`, `CppGenerator`, `VhdlGenerator`) — the C++ and VHDL tests
run against a sample source containing one documented and one deliberately
undocumented declaration and verify enforcement correctly detects the
violation; the DotNet test reuses the tool's own already-built assembly (see
`RunDotNetGenerationTest`) and verifies the checker runs to completion with
internally consistent counts — mirroring the `--enforce-docs` wiring in
`Program.RunToolLogic`. Every generator call is routed through a silent,
log-capturing child `Context` (not the outer validation context), so the
generators' own informational output (e.g. "Parsing assembly: ..." or "Found
N types...") never interleaves with the pass/fail transcript. The C++
generation and C++ enforcement tests are each gated on clang availability
(via `ApiMark.Cpp.CppAst.ClangDiscovery`) and are recorded as skipped, not
failed, when clang cannot be located. Results are accumulated in a
`TestResults` collection and optionally written to a `.trx` or `.xml` file.

#### Data Model

N/A - Validation is a static class with no instance state. It uses local
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
  5. Call `RunDotNetGenerationTest(context, testResults)`.
  6. Call `RunCppGenerationTest(context, testResults)`.
  7. Call `RunVhdlGenerationTest(context, testResults)`.
  8. Call `RunDotNetEnforceDocsTest(context, testResults)`.
  9. Call `RunCppEnforceDocsTest(context, testResults)`.
  10. Call `RunVhdlEnforceDocsTest(context, testResults)`.
  11. Print summary (total, passed, skipped, failed counts).
  12. If `context.ResultsFile` is set, call `WriteResultsFile`.
- *Preconditions*: `context` must be non-null.
- *Postconditions*: All self-tests have run; results are written to context;
  `context.ExitCode` is `1` if any test failed. Skipped tests (recorded with
  `TestOutcome.NotExecuted`, e.g. the C++ tests when clang is unavailable)
  never set `context.ExitCode` to `1`.

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

**RunDotNetGenerationTest(Context context, TestResults testResults)** — Private static.

- Constructs `DotNetGeneratorOptions` pointing at the currently-executing
  `ApiMark.Tool` assembly (`typeof(Program).Assembly.Location`) and its
  sibling `.xml` doc file (present because `GenerateDocumentationFile=true`),
  with `Visibility = ApiVisibility.All` since the tool's own types are internal.
- Creates a silent, log-capturing child `Context` (via
  `Context.Create(["--silent", "--log", logFile])`) so the generator's own
  informational output does not interleave with the validation transcript.
- Calls `new DotNetGenerator(options).Parse(genContext)` then
  `emitter.Emit(...)` with `OutputFormat.SingleFile` into a `TemporaryDirectory`.
- Verifies the generated Markdown contains `"Program"` and `"ApiMark.Tool"`.
- Appends a `TestResult` named `"ApiMark_DotNetGeneration"` to `testResults`.

**RunCppGenerationTest(Context context, TestResults testResults)** — Private static.

- First calls `ApiMark.Cpp.CppAst.ClangDiscovery.IsAvailable()`. When
  `false`, sets the test outcome to `TestOutcome.NotExecuted`, writes a
  skip message via `context.WriteLine` (never `WriteError`, so the skip
  never affects `context.ExitCode`), and returns early.
- When clang is available, writes a tiny embedded sample header (a
  documented struct with one documented member) to a `TemporaryDirectory`,
  constructs `CppGeneratorOptions` pointing at it. Creates a silent,
  log-capturing child `Context` so the generator's own informational output
  does not interleave with the validation transcript. Calls
  `new CppGenerator(options).Parse(genContext)` then `emitter.Emit(...)`.
- Verifies the generated Markdown contains the sample type name and its
  documentation summary text.
- Appends a `TestResult` named `"ApiMark_CppGeneration"` to `testResults`.

**RunVhdlGenerationTest(Context context, TestResults testResults)** — Private static.

- Writes a minimal valid VHDL entity (with `--!` doc comments on the entity
  and one port/generic) to a `TemporaryDirectory`, constructs
  `VhdlGeneratorOptions` referencing it. Creates a silent, log-capturing
  child `Context` so the generator's own informational output does not
  interleave with the validation transcript. Calls
  `new VhdlGenerator(options).Parse(genContext)` then `emitter.Emit(...)`.
- Verifies the generated Markdown contains the entity name and its
  documented port/generic text.
- No clang-style gating: VHDL parsing is entirely in-process via an
  embedded ANTLR4 grammar, with no external tool dependency.
- Appends a `TestResult` named `"ApiMark_VhdlGeneration"` to `testResults`.

**RunDotNetEnforceDocsTest(Context context, TestResults testResults)** — Private static.

- Constructs `DotNetGeneratorOptions` pointing at the currently-executing
  `ApiMark.Tool` assembly and its sibling `.xml` doc file, with
  `Visibility = ApiVisibility.All` (same self-referencing approach as
  `RunDotNetGenerationTest`; see its remarks for rationale). Creates a silent,
  log-capturing child `Context` so the generator's own informational output
  does not interleave with the validation transcript.
- Calls `new DotNetGenerator(options).Parse(genContext)` then
  `generator.CheckDocumentationCoverage("All")` — the same
  `IDocumentationCoverageCapable` API `Program.RunToolLogic` calls when
  `--enforce-docs` is supplied on the command line.
- Verifies the checker completes and reports internally consistent counts
  (`CheckedCount > 0` and `UndocumentedCount <= CheckedCount`), rather than
  asserting an exact violation count, since the tool's own already-built
  assembly's undocumented-item count varies by build configuration and
  target framework.
- Appends a `TestResult` named `"ApiMark_DotNetEnforceDocs"` to `testResults`.

**RunCppEnforceDocsTest(Context context, TestResults testResults)** — Private static.

- First calls `ApiMark.Cpp.CppAst.ClangDiscovery.IsAvailable()`. When
  `false`, sets the test outcome to `TestOutcome.NotExecuted`, writes a
  skip message via `context.WriteLine` (never `WriteError`), and returns early.
- When clang is available, writes a tiny embedded sample header (a struct
  with one documented member and one deliberately undocumented member) to a
  `TemporaryDirectory`, constructs `CppGeneratorOptions` pointing at it.
  Creates a silent, log-capturing child `Context` so the generator's own
  informational output does not interleave with the validation transcript.
- Calls `new CppGenerator(options).Parse(genContext)` then
  `generator.CheckDocumentationCoverage("Public")`.
- Verifies the result reports a violation for the undocumented member and
  does *not* report a violation for the documented member.
- Appends a `TestResult` named `"ApiMark_CppEnforceDocs"` to `testResults`.

**RunVhdlEnforceDocsTest(Context context, TestResults testResults)** — Private static.

- Writes a tiny embedded VHDL entity with one documented port (`clk`) and
  one deliberately undocumented port (`rst`) to a `TemporaryDirectory`,
  constructs `VhdlGeneratorOptions` referencing it. VHDL is used because it
  needs no external tool and its sample source is entirely self-contained.
- Creates a silent, log-capturing child `Context` so the generator's own
  informational output does not interleave with the validation transcript.
- Calls `new VhdlGenerator(options).Parse(genContext)` then
  `generator.CheckDocumentationCoverage("Public")` — the same
  `IDocumentationCoverageCapable` API `Program.RunToolLogic` calls when
  `--enforce-docs` is supplied on the command line.
- Verifies the result reports a violation for the undocumented `rst` port
  and does *not* report a violation for the documented `clk` port.
- Appends a `TestResult` named `"ApiMark_VhdlEnforceDocs"` to `testResults`.

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
- **DotNetGenerator / CppGenerator / VhdlGenerator** (`ApiMark.DotNet`,
  `ApiMark.Cpp`, `ApiMark.Vhdl`) — invoked directly via `IApiGenerator.Parse`
  and `IApiEmitter.Emit` in the three functional generation tests, and via
  `IDocumentationCoverageCapable.CheckDocumentationCoverage` in the three
  functional enforcement tests.
- **ApiMark.Cpp.CppAst.ClangDiscovery** — public pre-flight helper used to
  detect clang availability before running the C++ generation and C++
  enforcement functional tests, sharing the same discovery logic as
  `ClangAstParser`.
- **DemaConsulting.TestResults** — `TestResults`, `TestResult`, `TestOutcome`,
  `TrxSerializer`, and `JUnitSerializer` are used to build, accumulate, and
  serialize test results. `TestOutcome` has no dedicated "skipped" value;
  `TestOutcome.NotExecuted` is used to represent a skipped test.

#### Callers

- **Program.Run** — calls `Validation.Run(context)` when `context.Validate`
  is `true` (priority 3 in the dispatch chain).
