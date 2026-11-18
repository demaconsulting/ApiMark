### Validation

![Validation Structure](SelfTestView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

#### Purpose

Validation runs in-process self-tests that show the installed ApiMarkTool
works in the deployment environment: not only that it starts, but that it
generates and checks API documentation correctly. Downstream projects treat
the tool as off-the-shelf software and use the self-validation results file
as evidence that the pinned version does its job, so every test asserts
specific observable outcomes — exact exit codes, exact report lines, the exact
set of generated files, and exact Markdown fragments — rather than "ran
without an exception".

Every test runs the tool end to end through `Program.Run` with a silent,
log-capturing child `Context` (via `RunApiMark`), so the command-line parsing,
dispatch, generator, emitter, and coverage-reporting paths are all exercised
exactly as a user invocation would exercise them, and the tool's own output
(e.g. "Parsing assembly: ...") never interleaves with the pass/fail
transcript. Each test writes its sample sources into its own
`TemporaryDirectory`, passes absolute paths, never changes the current
directory, and deletes the directory afterwards, so results do not depend on
the machine's files or configuration.

The tests are:

- `ApiMark_VersionDisplay` — `--version` prints exactly the tool version.
- `ApiMark_HelpDisplay` — `--help` prints the usage line, the common options,
  and the options of every language subcommand.
- `ApiMark_DotNetGeneration` — `dotnet --format single-file` on the sample
  assembly writes only `api.md`, containing every sample type and member with
  its signature, summary, parameter, and return description.
- `ApiMark_GradualDisclosureOutput` — `dotnet` with the default format on the
  sample assembly writes exactly the expected index, namespace, type, and
  member pages, with the expected tables, breadcrumbs, and cross-links, and
  every relative link between the pages resolves to an existing page and,
  when it has a `#fragment`, to a heading anchor of that page.
- `ApiMark_CppGeneration` — `cpp --format single-file` on an embedded header
  documents the struct, its include line, and its documented field.
- `ApiMark_VhdlGeneration` — `vhdl --format single-file` on an embedded entity
  documents its description, generic table row, and port table row.
- `ApiMark_DotNetEnforceDocs` — `dotnet --enforce-docs Public` on the sample
  assembly reports nothing and exits 0 at `Error` severity when it is fully
  documented; with the `Thermostat.Read` documentation removed, reports exactly
  that method, exits 1 at `Error` severity, and exits 0 at the default
  `Warning` severity.
- `ApiMark_CppEnforceDocs` — `cpp --enforce-docs Public --enforce-docs-severity
  Error` on an embedded header reports exactly the undocumented field and
  exits 1.
- `ApiMark_VhdlEnforceDocs` — `vhdl --enforce-docs Public
  --enforce-docs-severity Error` on an embedded entity reports exactly the
  undocumented port and exits 1.

The .NET sample is the `ApiMarkSample` assembly, built in-process by
`WriteSampleAssembly` with `System.Reflection.Emit.PersistedAssemblyBuilder`
together with a matching XML documentation file, so the .NET tests need no
compiler, SDK, or fixture file and their expected output is exact. It contains
two public sealed classes — `Reading` (constructor and `Celsius` property) and
`Thermostat` (constructor, the overloads `SetTarget(double)` and
`SetTarget(double, double)`, and `Read()` returning `Reading`, whose remarks
reference the second overload) — giving eight documentable items, one
cross-type reference, and one overload page linked by heading anchor.

The C++ tests need an external clang executable, which is not part of the tool
package. They are gated on clang availability (via
`ApiMark.Cpp.CppAst.ClangDiscovery`, the discovery logic the `cpp` subcommand
uses) and are recorded as skipped, not failed, when clang cannot be located.
The VHDL parser is embedded, so the VHDL tests always run. Results are
accumulated in a `TestResults` collection and optionally written to a `.trx`
or `.xml` file.

#### Data Model

N/A - Validation is a static class with no instance state. It uses local
variables within each method for test results and temporary file paths.

All test result objects are `DemaConsulting.TestResults.TestResult` instances
created via `CreateTestResult(testName)` and collected in a
`DemaConsulting.TestResults.TestResults` object named
`"ApiMark Tool Self-Validation"`. Each in-process tool run is returned as a
private `ApiMarkRun(int ExitCode, string Log)` record.

#### Key Methods

**Validation.Run(Context context)** — Public static entry point.

- *Parameters*: `Context context` — the active program context.
- *Returns*: `void`
- *Algorithm*:
  1. Print validation header (tool version, machine name, OS, runtime, timestamp).
  2. Create a `TestResults` collection.
  3. Call `RunTest` for each self-test in the order listed under Purpose,
     passing `CppSkipReason` as the skip check for the two C++ tests.
  4. Print summary (total, passed, skipped, failed counts).
  5. If `context.ResultsFile` is set, call `WriteResultsFile`.
- *Preconditions*: `context` must be non-null.
- *Postconditions*: All self-tests have run; results are written to context;
  `context.ExitCode` is `1` if any test failed. Skipped tests (recorded with
  `TestOutcome.NotExecuted`, e.g. the C++ tests when clang is unavailable)
  never set `context.ExitCode` to `1`.

**RunTest(Context context, TestResults testResults, string testName, test, skipReason)** — Private static.

- `test` is a `Func<string?>` test body and `skipReason` an optional
  `Func<string?>` skip check.

- Calls `skipReason` (when given); a non-null reason records the test as
  `NotExecuted` and writes `⊘ {name} - Skipped: {reason}` via
  `context.WriteLine`, so a skip never affects the exit code.
- Otherwise calls `test`; `null` records `Passed` and writes
  `✓ {name} - Passed`, and a message records `Failed` with that message as the
  error message and writes `✗ {name} - Failed: {message}` via
  `context.WriteError`.
- Any exception is caught and recorded as a failure in the same way.
- Sets the duration and appends the result to `testResults`.

**Test bodies** (`VersionDisplayTest`, `HelpDisplayTest`,
`DotNetGenerationTest`, `GradualDisclosureOutputTest`, `CppGenerationTest`,
`VhdlGenerationTest`, `DotNetEnforceDocsTest`, `CppEnforceDocsTest`,
`VhdlEnforceDocsTest`) — Private static, each returning `null` on success or
the first failure message.

- Create a `TemporaryDirectory`, write the sample sources into it, run the tool
  with `RunApiMark`, and chain the checks listed under Purpose with
  `CheckExitCode`, `CheckContains`, `CheckDoesNotContain`, `CheckFiles`, and
  `CheckLinksResolve`.

**RunApiMark(TemporaryDirectory workspace, params string[] args)** — Private static.

- Creates a child context with `["--silent", "--log", logFile, ..args]`, where
  `logFile` is a unique file in the workspace; `args` never contains
  `--validate`, so there is no recursion.
- Calls `Program.Run` inside an explicit `using` block and captures the exit
  code, so the context — and with it the log file — is closed before the log
  is read and deleted (an open file cannot be read or deleted on Windows).
- Returns the exit code and the log content as an `ApiMarkRun`.

**Check helpers** — Private static, each returning `null` or a failure message.

- `CheckExitCode(run, expected)` — compares the exit code; the message includes
  the run's output.
- `CheckContains(what, text, expected...)` / `CheckDoesNotContain(what, text,
  unexpected...)` — ordinal fragment checks after normalizing line endings to
  `\n`; the message names the first offending fragment.
- `CheckFiles(directory, expected...)` — compares the complete, sorted set of
  files under a directory with the expected relative paths.
- `CheckLinksResolve(directory)` — checks that every relative `.md` link in
  every page under a directory points to an existing file and that every
  `#fragment` (on a page link or a same-page link) matches a heading anchor of
  the target page. Anchors are computed by `BuildHeadingAnchor` with the
  emitters' GitHub-style slug rules (lowercase; only letters, digits, spaces,
  hyphens, and underscores kept; spaces become hyphens), implemented
  independently so that an emitter defect is detected rather than reproduced;
  headings inside fenced code blocks are ignored.

**WriteSampleAssembly(string directory, bool documentRead = true)** — Private static.

- Builds the `ApiMarkSample` assembly with `PersistedAssemblyBuilder` and saves
  it as `ApiMarkSample.dll`, then writes `ApiMarkSample.xml` from the
  `SampleDocumentation` entries, omitting the `Thermostat.Read` entry when
  `documentRead` is `false`.
- Returns the paths of both files.

**CppSkipReason()** — Private static.

- Returns `null` when `ClangDiscovery.IsAvailable()` is `true`; otherwise the
  skip reason, which tells the user to install LLVM clang or set
  `APIMARK_CLANG_PATH`.

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

**TemporaryDirectory** — Private sealed nested class implementing `IDisposable`.

- On construction, creates a uniquely named directory under
  `Path.GetTempPath()`.
- `PathOf(relativePath)` returns a path inside it and
  `CreateSubdirectory(relativePath)` creates a subdirectory.
- On `Dispose`, deletes the directory and its entire subtree; I/O and access
  errors during deletion are silently swallowed (best-effort cleanup).
- Every test body holds one via `using var`, guaranteeing cleanup even when
  the test throws.

#### Error Handling

- Throws `ArgumentNullException` when `context` is null.
- Failed self-tests call `context.WriteError` for each failure, setting
  `context.ExitCode` to `1`.
- Unsupported results file extension calls `context.WriteError` and returns
  without writing a file.
- I/O errors during results file write call `context.WriteError`.
- Any exception within a test is caught by `RunTest`, recorded as a failed
  test, and reported via `context.WriteError`. This generic catch is intentional
  to ensure the test framework remains operational even if an individual test
  throws unexpectedly.

#### Dependencies

- **Context** (Cli subsystem) — used for output routing and to create
  child contexts for each in-process tool run.
- **Program** — `Program.Run` is called with child contexts, so every test
  exercises the real command-line dispatch and, through it, the
  `DotNetGenerator`, `CppGenerator`, and `VhdlGenerator` pipelines and the
  `--enforce-docs` coverage reporting.
- **System.Reflection.Emit.PersistedAssemblyBuilder** (.NET runtime) — builds
  the `ApiMarkSample` assembly used by the .NET tests.
- **ApiMark.Cpp.CppAst.ClangDiscovery** — public pre-flight helper used to
  detect clang availability before running the C++ generation and C++
  enforcement tests, sharing the same discovery logic as `ClangAstParser`.
- **DemaConsulting.TestResults** — `TestResults`, `TestResult`, `TestOutcome`,
  `TrxSerializer`, and `JUnitSerializer` are used to build, accumulate, and
  serialize test results. `TestOutcome` has no dedicated "skipped" value;
  `TestOutcome.NotExecuted` is used to represent a skipped test.

#### Callers

- **Program.Run** — calls `Validation.Run(context)` when `context.Validate`
  is `true` (priority 3 in the dispatch chain).
