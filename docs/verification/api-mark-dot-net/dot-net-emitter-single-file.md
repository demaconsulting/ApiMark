## DotNetEmitterSingleFile

### Verification Approach

`DotNetEmitterSingleFile` is integration-tested by parsing a fixture assembly
and calling `Emit` with `OutputFormat.SingleFile` and an
`InMemoryMarkdownWriterFactory`. Tests verify that exactly one writer is created,
that it is keyed `api`, and that its content contains the expected assembly,
namespace, and type headings. No internal production components are mocked beyond
the in-memory factory.

### Test Environment

Tests require the compiled fixture assembly, its XML documentation file, and
the `InMemoryMarkdownWriterFactory` from `ApiMark.Core.TestHelpers`. No external
service or network dependency is needed.

### Acceptance Criteria

- All `DotNetEmitterSingleFile` tests pass with zero failures.
- Exactly one Markdown writer is created.
- The single writer is keyed `api`.
- The output file contains an assembly-level heading.
- The output file contains a namespace-level heading.
- The output file contains a type-level heading for the fixture type.

### Test Scenarios

**Creates exactly one writer**: Verifies that the single-file emitter produces
exactly one Markdown writer. This scenario is tested by
`DotNetEmitterSingleFile_Emit_ValidModel_CreatesExactlyOneWriter`.

**Creates only the api writer**: Verifies that the single writer produced is
keyed `api`. This scenario is tested by
`DotNetEmitterSingleFile_Emit_ValidModel_CreatesApiFileOnly`.

**Api file contains an assembly-level heading**: Verifies that the output file
includes a heading containing the fixture assembly name. This scenario is tested
by `DotNetEmitterSingleFile_Emit_ValidModel_ApiFileContainsAssemblyHeading`.

**Api file contains a namespace-level heading**: Verifies that the output file
includes a heading containing the fixture namespace name. This scenario is tested
by `DotNetEmitterSingleFile_Emit_ValidModel_ApiFileContainsNamespaceHeading`.

**Api file contains a type-level heading for SampleClass**: Verifies that the
output file includes a heading for `SampleClass`. This scenario is tested by
`DotNetEmitterSingleFile_Emit_ValidModel_ApiFileContainsTypeHeading`.
