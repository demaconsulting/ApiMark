## VhdlGenerator

### Verification Approach

`VhdlGenerator` is verified with unit tests in `test/ApiMark.Vhdl.Tests/VhdlGeneratorTests.cs`.
Tests exercise the full VHDL generation pipeline using the fixture files in
`test/ApiMark.Vhdl.Tests/Fixtures/` and an `InMemoryMarkdownWriterFactory` test double that
captures emitted output without performing file-system I/O.

### Test Environment

Standard .NET test runner (`dotnet test`). No external tools, services, or privileged
configuration are required.

### Acceptance Criteria

- `VhdlGenerator(null)` throws `ArgumentNullException` immediately, before any pipeline
  processing begins.
- `VhdlGenerator` with an empty or whitespace `LibraryName` throws `ArgumentException`.
- Running `Parse` with a source pattern that matches no files calls `context.WriteError`
  and returns an emitter that produces no output.
- Running `Generate` against the fixture files produces an `api.md` entrypoint file.
- Running `Generate` against the fixture files produces at least one entity detail page.
- Running `Generate` against all fixture files produces the expected output structure
  including entity pages, a package page, and no standalone architecture pages.
- When one source file contains invalid VHDL, `Parse` reports an error via `context.WriteError`
  and continues processing remaining valid files, which still produce output.

### Test Scenarios

**Constructor rejects null options**: Verifies that constructing `VhdlGenerator` with a
`null` options argument throws `ArgumentNullException`, providing a clear error rather than
a deferred null-reference failure.
This scenario is tested by `VhdlGenerator_Constructor_NullOptions_ThrowsArgumentNullException`.

**Constructor rejects empty LibraryName**: Verifies that constructing `VhdlGenerator` with
an empty `LibraryName` throws `ArgumentException`.
This scenario is tested by `VhdlGenerator_Constructor_EmptyLibraryName_ThrowsArgumentException`.

**Constructor rejects whitespace LibraryName**: Verifies that constructing `VhdlGenerator`
with a whitespace-only `LibraryName` throws `ArgumentException`.
This scenario is tested by `VhdlGenerator_Constructor_WhitespaceLibraryName_ThrowsArgumentException`.

**No files matched emits error and produces no output**: Verifies that when the source glob
pattern matches no files, `context.WriteError` is called and the returned emitter produces
no output writers.
This scenario is tested by
`VhdlGenerator_Parse_NoFilesMatched_EmitsErrorAndReturnsEmptyEmitter`.

**Generate fixture file creates api entrypoint**: Verifies that the full pipeline, when run
against the counter fixture file, produces an `api.md` output file via the writer factory.
This scenario is tested by `VhdlGenerator_Generate_FixtureFile_CreatesApiEntrypoint`.

**Generate fixture file creates entity page**: Verifies that the full pipeline produces at
least one entity detail page in addition to the api index, confirming that per-entity Markdown
is emitted.
This scenario is tested by `VhdlGenerator_Generate_FixtureFile_CreatesEntityPage`.

**Generate all fixtures produces expected output structure**: Verifies that running the
generator against all fixture files (counter.vhd, mux.vhd, common_types.vhd) produces
entity pages, a package page, and no standalone architecture pages.
This scenario is tested by `VhdlGenerator_Generate_AllFixtures_ProducesExpectedOutputStructure`.

**Invalid file emits error and valid files still produce output**: Verifies that when
the source file set contains one invalid VHDL file and one valid VHDL file, the generator
reports an error via `context.WriteError` for the invalid file and still emits output for
the valid file.
This scenario is tested by `VhdlGenerator_Parse_InvalidVhdlFile_EmitsErrorAndSkipsFile`.
