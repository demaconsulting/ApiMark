# ApiMarkVhdl

## Verification Approach

ApiMarkVhdl is verified with unit tests in `test/ApiMark.Vhdl.Tests/` that exercise
the VHDL generation pipeline using three VHDL fixture files with --! doc comments
located in `test/ApiMark.Vhdl.Tests/Fixtures/`: `counter.vhd` (entity with generics,
ports, and inline doc comments), `mux.vhd` (entity with two architecture bodies), and
`common_types.vhd` (package with types, constants, components, and subprograms). The
ANTLR4 vhdl2008 parser is used as-is so verification proves the interaction between
VHDL parsing, doc comment extraction, and Markdown emission. Emitter unit tests use
in-memory data (no file I/O) to verify output structure without invoking the parser.
Fixture files are located in the source tree via `[CallerFilePath]` resolution in
`FixturePaths`.

## Test Environment

Tests require a .NET runtime capable of running the `ApiMark.Vhdl` library. No
additional toolchain dependency is required — the ANTLR4 runtime is a NuGet package.

## Acceptance Criteria

- All ApiMarkVhdl tests pass with zero failures.
- The parser correctly extracts entity names, generics, and ports from the counter fixture file.
- Preceding --! block comments are associated with entity declarations.
- Inline --! trailing comments are associated with port and generic declarations.
- The parser correctly extracts both architecture bodies from the mux fixture file.
- The parser correctly extracts a package declaration including types, constants,
  components, and subprograms from the common_types fixture file.
- The gradual-disclosure emitter creates an api index page and at least one entity page.
- The single-file emitter creates exactly one file.

## Test Scenarios

**Parser returns entity from fixture file**: Verifies that `VhdlAstParser.Parse` returns
a non-empty entities list from the counter fixture file. Tested by
`VhdlAstParser_Parse_FixtureFile_ReturnsEntity`.

**Entity has generics**: Verifies that the counter entity has at least one generic parsed
correctly. Tested by `VhdlAstParser_Parse_FixtureFile_EntityHasGenerics`.

**Entity has ports**: Verifies that the counter entity has at least one port parsed
correctly. Tested by `VhdlAstParser_Parse_FixtureFile_EntityHasPorts`.

**Entity doc comment is parsed**: Verifies that the preceding --! block comment on the
counter entity is extracted and the Summary field is populated. Tested by
`VhdlAstParser_Parse_FixtureFile_EntityDocCommentParsed`.

**Ports have inline doc comments**: Verifies that at least one port has an inline --!
trailing comment parsed into a VhdlDocComment. Tested by
`VhdlAstParser_Parse_FixtureFile_PortsHaveInlineDocComments`.

**Mux fixture parses two architecture bodies**: Verifies that parsing `mux.vhd` returns
exactly two `VhdlArchitectureDecl` records. Tested by
`VhdlAstParser_Parse_MuxFixture_ParsesTwoArchitectures`.

**Package is returned from common_types fixture**: Verifies that parsing `common_types.vhd`
returns a `VhdlFileModel` with at least one package declaration. Tested by
`VhdlAstParser_Parse_CommonTypesFixture_ReturnsPackage`.

**Constructor null options throws**: Verifies that `VhdlGenerator(null)` throws
`ArgumentNullException`. Tested by
`VhdlGenerator_Constructor_NullOptions_ThrowsArgumentNullException`.

**Generator creates api entrypoint**: Verifies that the full pipeline produces an
`api.md` file. Tested by `VhdlGenerator_Generate_FixtureFile_CreatesApiEntrypoint`.

**Generator creates entity page**: Verifies that the full pipeline produces an entity
detail page. Tested by `VhdlGenerator_Generate_FixtureFile_CreatesEntityPage`.

**Emitter null factory throws**: Verifies that `VhdlEmitter.Emit(null!, ...)` throws
`ArgumentNullException`. Tested by `VhdlEmitter_Emit_NullFactory_ThrowsArgumentNullException`.

**Gradual emitter creates api index page**: Verifies that the gradual disclosure emitter
creates the api index page. Tested by
`VhdlEmitterGradualDisclosure_Emit_MinimalData_CreatesApiIndexPage`.

**Gradual emitter creates entity page**: Verifies that the gradual disclosure emitter
creates an entity detail page. Tested by
`VhdlEmitterGradualDisclosure_Emit_MinimalData_CreatesEntityPage`.

**Gradual emitter api index contains library heading**: Verifies that the api index page
heading contains the library name. Tested by
`VhdlEmitterGradualDisclosure_Emit_MinimalData_ApiIndexContainsLibraryNameHeading`.

**Single-file emitter creates exactly one writer**: Verifies that the single-file emitter
creates exactly one Markdown file. Tested by
`VhdlEmitterSingleFile_Emit_MinimalData_CreatesExactlyOneWriter`.

**Single-file emitter creates api file only**: Verifies that the single-file emitter
creates only the api.md file. Tested by
`VhdlEmitterSingleFile_Emit_MinimalData_CreatesApiFileOnly`.
