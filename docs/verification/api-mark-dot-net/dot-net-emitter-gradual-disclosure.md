## DotNetEmitterGradualDisclosure

### Verification Approach

`DotNetEmitterGradualDisclosure` is integration-tested by parsing a fixture
assembly and calling `Emit` with `OutputFormat.GradualDisclosure` and an
`InMemoryMarkdownWriterFactory`. Tests inspect the set of created writer keys
and the content written to specific pages. No internal production components
are mocked beyond the in-memory factory.

### Test Environment

Tests require the compiled fixture assembly, its XML documentation file, and
the `InMemoryMarkdownWriterFactory` from `ApiMark.Core.TestHelpers`. No external
service or network dependency is needed.

### Acceptance Criteria

- All `DotNetEmitterGradualDisclosure` tests pass with zero failures.
- The api index page is created with the expected assembly name heading.
- A namespace summary page is created for each namespace in the fixture assembly.
- A type page is created for each visible type in each namespace.

### Test Scenarios

**Api index page is created**: Verifies that the gradual-disclosure emitter
creates the `api` writer key, confirming that the top-level assembly entrypoint
is emitted as the first page in the output tree. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesApiIndexPage`.

**Api index heading contains the assembly name**: Verifies that the api index
page includes a heading containing the fixture assembly name. This scenario is
tested by `DotNetEmitterGradualDisclosure_Emit_ValidModel_ApiIndexContainsAssemblyNameHeading`.

**Namespace page is created for the fixture namespace**: Verifies that a writer
whose key contains the fixture namespace name is created. This scenario is tested
by `DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesNamespacePage`.

**Type page is created for SampleClass**: Verifies that a writer whose key
contains `SampleClass` is created, confirming that per-type pages are emitted for
all visible types. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesTypePage`.
