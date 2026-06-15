## VhdlGenerator

### Verification Approach

`VhdlGenerator` is verified with unit tests in `test/ApiMark.Vhdl.Tests/VhdlGeneratorTests.cs`.
Tests exercise the full VHDL generation pipeline using the `counter.vhd` fixture file and an
`InMemoryMarkdownWriterFactory` test double that captures emitted output without performing
file-system I/O.

### Test Environment

Standard .NET test runner (`dotnet test`). No external tools, services, or privileged
configuration are required.

### Acceptance Criteria

- `VhdlGenerator(null)` throws `ArgumentNullException` immediately, before any pipeline
  processing begins.
- Running `Generate` against the counter fixture file produces an `api.md` entrypoint file.
- Running `Generate` against the counter fixture file produces at least one entity detail page.

### Test Scenarios

**Constructor rejects null options**: Verifies that constructing `VhdlGenerator` with a
`null` options argument throws `ArgumentNullException`, providing a clear error rather than
a deferred null-reference failure.
This scenario is tested by `VhdlGenerator_Constructor_NullOptions_ThrowsArgumentNullException`.

**Generate fixture file creates api entrypoint**: Verifies that the full pipeline, when run
against the counter fixture file, produces an `api.md` output file via the writer factory.
This scenario is tested by `VhdlGenerator_Generate_FixtureFile_CreatesApiEntrypoint`.

**Generate fixture file creates entity page**: Verifies that the full pipeline produces at
least one entity detail page in addition to the api index, confirming that per-entity Markdown
is emitted.
This scenario is tested by `VhdlGenerator_Generate_FixtureFile_CreatesEntityPage`.
