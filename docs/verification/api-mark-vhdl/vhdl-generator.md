# VhdlGenerator

## Verification Approach

VhdlGenerator is verified with unit tests in `test/ApiMark.Vhdl.Tests/VhdlGeneratorTests.cs`.

## Test Scenarios

**Constructor null options throws ArgumentNullException**: Tested by
`VhdlGenerator_Constructor_NullOptions_ThrowsArgumentNullException`.

**Generate fixture file creates api entrypoint**: Tested by
`VhdlGenerator_Generate_FixtureFile_CreatesApiEntrypoint`.

**Generate fixture file creates entity page**: Tested by
`VhdlGenerator_Generate_FixtureFile_CreatesEntityPage`.
