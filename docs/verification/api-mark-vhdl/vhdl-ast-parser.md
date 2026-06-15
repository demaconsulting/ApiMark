## VhdlAstParser

### Verification Approach

`VhdlAstParser` is verified with unit tests in `test/ApiMark.Vhdl.Tests/VhdlAstParserTests.cs`
that parse three fixture files (`counter.vhd`, `mux.vhd`, `common_types.vhd`) and assert on
the resulting `VhdlFileModel`. The ANTLR4 vhdl2008 grammar is exercised as a real dependency;
no mocking or stubbing is applied at the parser level.

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
- A file with two architecture bodies returns two `VhdlArchitectureDecl` records.
- A package declaration returns a `VhdlPackageDecl` with populated Types, Constants,
  Components, and Subprograms collections.

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

**Multiple architectures parsed**: Verifies that parsing `mux.vhd`, which contains two
architecture bodies, returns a `VhdlFileModel` with exactly two `VhdlArchitectureDecl`
records.
This scenario is tested by `VhdlAstParser_Parse_MuxFixture_ParsesTwoArchitectures`.

**Architecture links to entity**: Verifies that parsing `mux.vhd` also returns a mux entity
declaration, confirming that entity and architecture bodies in the same file are both
captured.
This scenario is tested by `VhdlAstParser_Parse_MuxFixture_HasMuxEntity`.

**Package is returned**: Verifies that `VhdlAstParser.Parse` applied to `common_types.vhd`
returns a `VhdlFileModel` with at least one package declaration.
This scenario is tested by `VhdlAstParser_Parse_CommonTypesFixture_ReturnsPackage`.

**Package has type declarations**: Verifies that the parsed package contains exactly two
type declarations, confirming that `full_type_declaration` items are extracted.
This scenario is tested by `VhdlAstParser_Parse_CommonTypesFixture_PackageHasTwoTypes`.

**Package has constant declarations**: Verifies that the parsed package contains exactly two
constant declarations.
This scenario is tested by `VhdlAstParser_Parse_CommonTypesFixture_PackageHasTwoConstants`.

**Constants have doc comments**: Verifies that at least one constant has a preceding `--!`
doc comment extracted into its `Doc` field.
This scenario is tested by `VhdlAstParser_Parse_CommonTypesFixture_ConstantsHaveDocComments`.

**Package has component declaration**: Verifies that the parsed package contains exactly one
component declaration.
This scenario is tested by `VhdlAstParser_Parse_CommonTypesFixture_PackageHasOneComponent`.

**Package has two subprograms**: Verifies that the parsed package contains exactly two
subprogram declarations.
This scenario is tested by `VhdlAstParser_Parse_CommonTypesFixture_PackageHasTwoSubprograms`.

**Function identified by kind**: Verifies that the `to_natural` subprogram has
`VhdlSubprogramKind.Function` as its kind.
This scenario is tested by `VhdlAstParser_Parse_CommonTypesFixture_ToNaturalIsFunction`.

**Procedure identified by kind**: Verifies that the `clear_vector` subprogram has
`VhdlSubprogramKind.Procedure` as its kind.
This scenario is tested by `VhdlAstParser_Parse_CommonTypesFixture_ClearVectorIsProcedure`.

**Function has one parameter**: Verifies that `to_natural` has exactly one formal parameter.
This scenario is tested by `VhdlAstParser_Parse_CommonTypesFixture_ToNaturalHasOneParameter`.

**Function return type extracted**: Verifies that `to_natural` has `ReturnType` equal to
`"NATURAL"`.
This scenario is tested by `VhdlAstParser_Parse_CommonTypesFixture_ToNaturalHasReturnTypeNatural`.

**Procedure has one parameter**: Verifies that `clear_vector` has exactly one formal
parameter.
This scenario is tested by `VhdlAstParser_Parse_CommonTypesFixture_ClearVectorHasOneParameter`.

**Procedure return type is null**: Verifies that `clear_vector` has a null `ReturnType`,
confirming that procedures do not carry a return type.
This scenario is tested by `VhdlAstParser_Parse_CommonTypesFixture_ClearVectorHasNullReturnType`.

**Doc @param entry extracted**: Verifies that the doc comment on `to_natural` contains a
`VhdlParamDoc` entry matching the `@param` tag in the source.
This scenario is tested by `VhdlAstParser_Parse_CommonTypesFixture_ToNaturalDocHasParamEntry`.

**Doc @return entry extracted**: Verifies that the doc comment on `to_natural` has a
non-null `Returns` field matching the `@return` tag in the source.
This scenario is tested by `VhdlAstParser_Parse_CommonTypesFixture_ToNaturalDocHasReturnEntry`.
