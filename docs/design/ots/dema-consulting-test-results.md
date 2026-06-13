## DemaConsulting.TestResults

DemaConsulting.TestResults is a .NET library that provides a lightweight model for recording
structured test outcomes and serializing them to standard CI interchange formats (TRX and JUnit XML).
In ApiMark, it is used exclusively by the SelfTest subsystem to collect pass/fail evidence from
the `--validate` self-validation run and optionally write the results to a file.

### Purpose

DemaConsulting.TestResults was chosen because it provides a minimal, dependency-free model for
building a test results collection programmatically and writing it to TRX or JUnit XML — the two
formats most widely consumed by CI pipelines and test dashboards. The library eliminates the need
to hand-write XML serialization in the SelfTest subsystem while keeping the dependency surface small.

### Features Used

- **`TestResults` container** — holds the test suite name (`Name`) and an ordered list of
  `TestResult` records in its `Results` collection.
- **`TestResult` record** — captures per-test identity (`Name`, `ClassName`, `CodeBase`),
  outcome (`Outcome`), failure detail (`ErrorMessage`), and elapsed time (`Duration`).
- **`TestOutcome` enum** — `Passed` and `Failed` values used by `Validation.cs` to mark
  each self-validation scenario.
- **`TrxSerializer.Serialize(testResults)`** — serializes a `TestResults` collection to a TRX
  string for writing to files with a `.trx` extension.
- **`JUnitSerializer.Serialize(testResults)`** — serializes a `TestResults` collection to a
  JUnit XML string for writing to files with a `.xml` extension.

### Integration Pattern

DemaConsulting.TestResults is consumed via direct API calls in `Validation.cs`. No wrapper class
is introduced. The integration follows this sequence:

1. At the start of `Validation.Run`, a `TestResults` instance is created and given the suite name
   `"ApiMark Tool Self-Validation"`.
2. For each self-validation scenario (`RunVersionTest`, `RunHelpTest`), a `TestResult` is created
   via `CreateTestResult(testName)`, populated with `Outcome`, `ErrorMessage`, and `Duration`,
   then appended to `testResults.Results`.
3. After all scenarios complete, totals are computed from `testResults.Results` by filtering on
   `TestOutcome.Passed` and `TestOutcome.Failed`.
4. If the caller supplied a results file path (`context.ResultsFile != null`), `WriteResultsFile`
   selects `TrxSerializer` or `JUnitSerializer` based on the file extension and writes the
   serialized content with `File.WriteAllText`.

The consuming unit is `ApiMarkTool-SelfTest-Validation`.
