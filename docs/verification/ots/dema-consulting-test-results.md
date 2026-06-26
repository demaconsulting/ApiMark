## DemaConsulting.TestResults

### Verification Approach

DemaConsulting.TestResults is verified in ApiMark through the existing self-validation tests in
`test/ApiMark.Tool.Tests/SelfTest/ValidationTests.cs` that exercise the pass/fail recording and
results-file writing paths of `Validation.cs`. The verification focus is the subset of capabilities
needed by the product: collecting test outcomes into a reportable collection and serializing that
collection to TRX and JUnit XML. Evidence is collected from automated tests that confirm test names
appear in log output and that results files are created at the specified paths with format-specific
content.

### Test Scenarios

**Version and help self-test names appear in log output**: Verifies that when `Validation.Run`
executes, the log output contains the names of both self-test cases: `ApiMark_VersionDisplay`
and `ApiMark_HelpDisplay`. This scenario is tested by
`Validation_Run_WritesVersionAndHelpTestResults`.

**Results collection serializes to TRX when requested**: Verifies that when `context.ResultsFile`
has a `.trx` extension, a `.trx` file is written to the specified path containing TRX XML content
(identified by the `TestRun` root element). This scenario is tested by
`Validation_Run_WithResultsTrxFile_CreatesTrxFile`.

**Results collection serializes to JUnit XML when requested**: Verifies that when
`context.ResultsFile` has a `.xml` extension, a `.xml` file is written to the specified path
containing JUnit XML content (identified by the `testsuites` root element). This scenario is tested
by `Validation_Run_WithResultsXmlFile_CreatesXmlFile`.
