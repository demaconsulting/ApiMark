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
- The api file emits class, free-function, enum, and namespace-level type-alias sections when present.
- Class-scoped type aliases appear as H{depth+3} sub-entries within their owning class section.
- Member headings respect non-default heading-depth offsets.

### Test Scenarios

**Emit class section**: Verifies that class data is rendered into a dedicated class section.
Tested by `CppEmitterSingleFile_Emit_ClassData_ContainsClassSection`.

**Emit free-function section**: Verifies that namespace free functions are rendered into the
single-file output. Tested by
`CppEmitterSingleFile_Emit_FreeFunction_ContainsFreeFunctionSection`.

**Emit enum section**: Verifies that enum declarations are rendered into the single-file
output. Tested by `CppEmitterSingleFile_Emit_Enum_ContainsEnumSection`.

**Emit type alias section**: Verifies that namespace-level type alias declarations are
rendered into the single-file output with an H3 heading containing the alias name and a
fenced code block containing the `using` declaration and underlying type. Tested by
`CppEmitterSingleFile_Emit_TypeAlias_ContainsTypeAliasSection`.

**Emit class-scoped type alias sub-entry**: Verifies that a class-scoped `using` type alias
declaration appears as an H{depth+3} sub-entry below the owning class section, with a
signature block containing the `using` declaration and underlying type. Tested by
`CppEmitterSingleFile_Emit_ClassScopedTypeAlias_ContainsAliasSubEntry`.

**Heading depth offset**: Verifies that all heading levels — library, namespace, class, and
member — shift when a non-default heading depth is configured. Tested by
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
