## VhdlEmitterGradualDisclosure

### Verification Approach

`VhdlEmitterGradualDisclosure` is verified with unit tests in
`test/ApiMark.Vhdl.Tests/VhdlEmitterGradualDisclosureTests.cs` using in-memory test doubles.
A minimal `VhdlFileModel` is constructed directly without invoking the parser, and an
`InMemoryMarkdownWriterFactory` captures emitted output. Tests confirm that the correct files
are created and that the api index page contains expected content.

### Test Environment

Standard .NET test runner (`dotnet test`). No external tools, services, or privileged
configuration are required.

### Acceptance Criteria

- `VhdlEmitterGradualDisclosure.Emit` produces an api index page in the writer factory output.
- `VhdlEmitterGradualDisclosure.Emit` produces at least one entity detail page in addition to
  the api index page.
- The api index page content includes the library name as a heading.
- When an entity has architectures that implement it, the entity detail page includes an
  Architectures section rendered inline (not as separate files).

### Test Scenarios

**Creates api index page**: Verifies that emitting minimal VHDL model data results in an api
index page being created by the writer factory.
This scenario is tested by
`VhdlEmitterGradualDisclosure_Emit_MinimalData_CreatesApiIndexPage`.

**Creates entity page**: Verifies that emitting minimal VHDL model data results in at least
one entity detail page being created by the writer factory, confirming per-entity emission.
This scenario is tested by
`VhdlEmitterGradualDisclosure_Emit_MinimalData_CreatesEntityPage`.

**Api index contains library name heading**: Verifies that the api index page produced by the
emitter contains the library name as a heading, confirming that library metadata is rendered
correctly.
This scenario is tested by
`VhdlEmitterGradualDisclosure_Emit_MinimalData_ApiIndexContainsLibraryNameHeading`.

**Architecture rendered inline on entity page**: Verifies that when an entity has associated
architectures, those architectures appear as an inline section on the entity detail page rather
than as separate files, confirming that the gradual-disclosure emitter does not create
standalone architecture pages.
This scenario is tested by
`VhdlEmitterGradualDisclosure_Emit_EntityWithArchitecture_ArchitectureSectionAppearsInEntityPage`.
