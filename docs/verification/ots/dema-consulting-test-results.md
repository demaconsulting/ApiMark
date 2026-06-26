## DemaConsulting.TestResults

### Verification Approach

DemaConsulting.TestResults is verified in ApiMark through the existing self-validation tests in
`test/ApiMark.Tool.Tests/SelfTest/ValidationTests.cs` that exercise the pass/fail recording and
results-file writing paths of `Validation.cs`. The verification focus is the subset of capabilities
needed by the product: collecting test outcomes into a reportable collection and serializing that
collection to TRX and JUnit XML. The collection behavior is evidenced by three tests: the log test
confirms test names are recorded and emitted, while the TRX and JUnit serialization tests perform
a full round-trip — deserializing the written file back into a `TestResults` collection and
asserting both test names and `Passed` outcomes are preserved.

### Test Scenarios

**Version and help self-test names appear in log output**: Verifies that when `Validation.Run`
executes, the log output contains the names of both self-test cases: `ApiMark_VersionDisplay`
and `ApiMark_HelpDisplay`. This scenario is tested by
`Validation_Run_WritesVersionAndHelpTestResults`.

**Results collection serializes to TRX when requested**: Verifies that when `context.ResultsFile`
has a `.trx` extension, a `.trx` file is written containing TRX XML that round-trips correctly:
the deserialized collection contains exactly two results (`ApiMark_VersionDisplay` and
`ApiMark_HelpDisplay`, both `Passed`). This scenario is tested by
`Validation_Run_WithResultsTrxFile_CreatesTrxFile`.

**Results collection serializes to JUnit XML when requested**: Verifies that when
`context.ResultsFile` has a `.xml` extension, a `.xml` file is written containing JUnit XML that
round-trips correctly: the deserialized collection contains exactly two results
(`ApiMark_VersionDisplay` and `ApiMark_HelpDisplay`, both `Passed`). This scenario is tested by
`Validation_Run_WithResultsXmlFile_CreatesXmlFile`.
