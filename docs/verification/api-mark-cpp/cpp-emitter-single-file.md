## CppEmitterSingleFile

### Verification Approach

`CppEmitterSingleFile` is unit-tested in
`test/ApiMark.Cpp.Tests/CppEmitterSingleFileTests.cs` using synthetic namespace data and an
`InMemoryMarkdownWriterFactory` to capture the single output document.

### Test Environment

No external services, network access, clang installation, or file system access are required.
Tests run with the standard xUnit.net test runner.

### Acceptance Criteria

- `CppEmitterSingleFile.Emit` creates exactly one writer keyed as `("", "api")`.
- The api file contains the library-name heading and namespace headings.
- The api file emits class, free-function, and enum sections when present.
- Member headings respect non-default heading-depth offsets.

### Test Scenarios

**Emit class section**: Verifies that class data is rendered into a dedicated class section.
This scenario is tested by `CppEmitterSingleFile_Emit_ClassData_ContainsClassSection`.

**Emit free-function section**: Verifies that namespace free functions are rendered into the
single-file output. This scenario is tested by
`CppEmitterSingleFile_Emit_FreeFunction_ContainsFreeFunctionSection`.

**Emit enum section**: Verifies that enum declarations are rendered into the single-file
output. This scenario is tested by `CppEmitterSingleFile_Emit_Enum_ContainsEnumSection`.

**Heading depth offset**: Verifies that all heading levels — library, namespace, class, and
member — shift when a non-default heading depth is configured. This scenario is tested by
`CppEmitterSingleFile_Emit_NonDefaultHeadingDepth_OffsetsHeadings`.

**Creates exactly one writer keyed as api**: Verifies that the single-file emitter creates
exactly one Markdown writer keyed as `api` at the output root. Tested by
`CppEmitterSingleFile_Emit_MinimalData_CreatesExactlyOneWriter` and
`CppEmitterSingleFile_Emit_MinimalData_CreatesApiFileOnly`.

**Library name heading**: Verifies that the generated `api.md` contains the configured library name
as a top-level heading. Tested by
`CppEmitterSingleFile_Emit_MinimalData_ApiFileContainsLibraryNameHeading`.

**Namespace heading**: Verifies that the generated `api.md` contains a namespace section heading for
each documented namespace. Tested by
`CppEmitterSingleFile_Emit_MinimalData_ApiFileContainsNamespaceHeading`.
