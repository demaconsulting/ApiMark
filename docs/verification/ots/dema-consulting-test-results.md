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

**Version and help self-test names appear in log output**: Verifies that when `Validation.Run`
executes, the log output contains the names of both self-test cases: `ApiMark_VersionDisplay`
and `ApiMark_HelpDisplay`. This scenario is tested by
`Validation_Run_WritesVersionAndHelpTestResults`.

**Results collection serializes to TRX when requested**: Verifies that when `context.ResultsFile`
has a `.trx` extension, `WriteResultsFile` calls `TrxSerializer.Serialize` and writes the result
to the specified path. This scenario is tested by
`Validation_Run_WithResultsTrxFile_CreatesTrxFile`.

**Results collection serializes to JUnit XML when requested**: Verifies that when
`context.ResultsFile` has a `.xml` extension, `WriteResultsFile` calls `JUnitSerializer.Serialize`
and writes the result to the specified path. This scenario is tested by
`Validation_Run_WithResultsXmlFile_CreatesXmlFile`.
