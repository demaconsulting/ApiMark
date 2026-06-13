## VhdlAstParser

### Verification Approach

`VhdlAstParser` is verified with unit tests in `test/ApiMark.Vhdl.Tests/VhdlAstParserTests.cs`
that parse the `counter.vhd` fixture file and assert on the resulting `VhdlFileModel`. The
ANTLR4 vhdl2008 grammar is exercised as a real dependency; no mocking or stubbing is applied
at the parser level.

### Test Environment

Standard .NET test runner (`dotnet test`). No external tools, services, or privileged
configuration are required.

### Acceptance Criteria

- `VhdlAstParser.Parse` returns a non-empty entities list from the counter fixture file.
- The counter entity has at least one generic declared and parsed correctly.
- The counter entity has at least one port declared and parsed correctly.
- The preceding `--!` block comment on the counter entity is extracted and the Summary field
  is populated.
- At least one port has an inline `--!` trailing comment parsed into a `VhdlDocComment`.

### Test Scenarios

**Parser returns entity from fixture file**: Verifies that `VhdlAstParser.Parse` applied to
the `counter.vhd` fixture returns a `VhdlFileModel` with a non-empty entities list.
This scenario is tested by `VhdlAstParser_Parse_FixtureFile_ReturnsEntity`.

**Entity has generics**: Verifies that the counter entity in the parsed model has at least
one generic, confirming that generic declarations are extracted from the ANTLR4 parse tree.
This scenario is tested by `VhdlAstParser_Parse_FixtureFile_EntityHasGenerics`.

**Entity has ports**: Verifies that the counter entity in the parsed model has at least one
port, confirming that port declarations are extracted from the ANTLR4 parse tree.
This scenario is tested by `VhdlAstParser_Parse_FixtureFile_EntityHasPorts`.

**Entity doc comment is parsed**: Verifies that the preceding `--!` block comment on the
counter entity is extracted and associated with the entity's Summary field in the model.
This scenario is tested by `VhdlAstParser_Parse_FixtureFile_EntityDocCommentParsed`.

**Ports have inline doc comments**: Verifies that at least one port has an inline `--!`
trailing comment that is parsed into a `VhdlDocComment` and associated with the port.
This scenario is tested by `VhdlAstParser_Parse_FixtureFile_PortsHaveInlineDocComments`.
