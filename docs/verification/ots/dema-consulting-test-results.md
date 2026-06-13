## DemaConsulting.TestResults

### Verification Approach

DemaConsulting.TestResults is verified in ApiMark through the existing self-validation tests in
`test/ApiMark.Tool.Tests/SelfTest/ValidationTests.cs` that exercise the pass/fail recording and
results-file writing paths of `Validation.cs`. The verification focus is the subset of capabilities
needed by the product: creating a `TestResults` collection, recording `Passed` and `Failed`
outcomes on individual `TestResult` objects, and serializing the collection to TRX and JUnit XML
via `TrxSerializer` and `JUnitSerializer`. Evidence is collected from automated tests that confirm
the expected file content and outcome counts.

### Test Scenarios

**Version-display test records a Passed outcome**: Verifies that when the `--version` child
invocation succeeds and returns a semantic version string, `Validation.Run` creates a `TestResult`
with `Outcome = TestOutcome.Passed` and adds it to the `TestResults` collection. This scenario is
tested by `Validation_Run_VersionDisplayPasses_WritesPassedResult`.

**Help-display test records a Passed outcome**: Verifies that when the `--help` child invocation
succeeds and returns text containing "Usage:" and "Options:", `Validation.Run` creates a
`TestResult` with `Outcome = TestOutcome.Passed` and adds it to the `TestResults` collection.
This scenario is tested by `Validation_Run_HelpDisplayPasses_WritesPassedResult`.

**Results collection serializes to TRX when requested**: Verifies that when `context.ResultsFile`
has a `.trx` extension, `WriteResultsFile` calls `TrxSerializer.Serialize` and writes the result
to the specified path. This scenario is tested by
`Validation_Run_WithResultsFileTrx_WritesResultsFile`.

**Results collection serializes to JUnit XML when requested**: Verifies that when
`context.ResultsFile` has a `.xml` extension, `WriteResultsFile` calls `JUnitSerializer.Serialize`
and writes the result to the specified path. This scenario is tested by
`Validation_Run_WithResultsFileXml_WritesResultsFile`.
