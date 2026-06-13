## CppEmitterSingleFile

### Verification Approach

`CppEmitterSingleFile` is unit-tested in
`test/ApiMark.Cpp.Tests/CppEmitterSingleFileTests.cs` without invoking clang. A
`BuildMinimalData` helper constructs a `CppEmitter` and a controlled namespace declaration
containing one class (`Widget`), along with a `CppTypeLinkResolver`. An
`InMemoryMarkdownWriterFactory` test double captures the single created writer and its
content. Tests verify that exactly one writer is created, that its key is `("", "api")`,
that the api file contains the library-name heading, and that the api file contains a
namespace heading.

### Test Environment

No external services, network access, clang installation, or file system access are
required. Tests run with the standard xUnit.net test runner.

### Acceptance Criteria

- `CppEmitterSingleFile.Emit` creates exactly one writer in the factory.
- The single writer is keyed as `("", "api")`.
- The api file contains a heading whose text includes the library name (`"TestLib"`).
- The api file contains a heading whose text includes the namespace name (`"testlib"`).

### Test Scenarios

**Emit creates exactly one writer**: Verifies that `CppEmitterSingleFile.Emit`
produces exactly one writer in the factory, confirming that all content is written
to a single file.
This scenario is tested by `CppEmitterSingleFile_Emit_MinimalData_CreatesExactlyOneWriter`.

**Emit creates the api writer only**: Verifies that the single writer created by the
emitter is keyed as `("", "api")`, confirming that the output uses the expected file
key convention.
This scenario is tested by `CppEmitterSingleFile_Emit_MinimalData_CreatesApiFileOnly`.

**Api file contains library name heading**: Verifies that the api file produced by
the emitter contains a heading whose text includes `"TestLib"`, confirming that the
library name from `CppGeneratorOptions` is correctly used in the top-level heading.
This scenario is tested by
`CppEmitterSingleFile_Emit_MinimalData_ApiFileContainsLibraryNameHeading`.

**Api file contains namespace heading**: Verifies that the api file contains a heading
whose text includes `"testlib"`, confirming that per-namespace headings are correctly
emitted in the single-file layout.
This scenario is tested by
`CppEmitterSingleFile_Emit_MinimalData_ApiFileContainsNamespaceHeading`.
