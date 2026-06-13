# VhdlAstParser

## Verification Approach

VhdlAstParser is verified with unit tests in `test/ApiMark.Vhdl.Tests/VhdlAstParserTests.cs`
that parse the `counter.vhd` fixture file and assert on the resulting `VhdlFileModel`.

## Test Scenarios

**Parser returns entity**: Tested by `VhdlAstParser_Parse_FixtureFile_ReturnsEntity`.

**Entity has generics**: Tested by `VhdlAstParser_Parse_FixtureFile_EntityHasGenerics`.

**Entity has ports**: Tested by `VhdlAstParser_Parse_FixtureFile_EntityHasPorts`.

**Entity doc comment parsed**: Tested by `VhdlAstParser_Parse_FixtureFile_EntityDocCommentParsed`.

**Ports have inline doc comments**: Tested by `VhdlAstParser_Parse_FixtureFile_PortsHaveInlineDocComments`.
