## CppEmitter

### Verification Approach

`CppEmitter` is unit-tested in `test/ApiMark.Cpp.Tests/CppEmitterTests.cs` without
invoking clang. A `BuildMinimalEmitter` helper constructs a `CppEmitter` directly with
a controlled `SortedDictionary<string, CppEmitter.NamespaceDeclarations>` and a
`CppTypeLinkResolver`, avoiding any I/O. An `InMemoryMarkdownWriterFactory` test double
captures emitted content. Tests cover null-argument rejection, format dispatch (gradual
vs. single-file), filename sanitization, and class-declaration building.

### Test Environment

No external services, network access, clang installation, or file system access are
required. Tests run with the standard xUnit.net test runner.

### Acceptance Criteria

- `CppEmitter.Emit` with a null factory throws `ArgumentNullException` immediately.
- `OutputFormat.GradualDisclosure` produces more than one writer in the factory.
- `OutputFormat.SingleFile` produces exactly one writer keyed as `"api"`.
- `SanitizeFileName` replaces characters invalid in file names (e.g. `*` in
  `"operator*"`) with underscores.
- `SanitizeFileName` leaves names that contain no invalid characters unchanged.
- `BuildClassDeclaration` returns `"class ClassName"` for a non-final class with no
  base types.
- `BuildClassDeclaration` includes `"final"` in the result for a class marked `IsFinal`.

### Test Scenarios

**Emit rejects null factory**: Verifies that passing `null` as the factory argument
to `CppEmitter.Emit` throws `ArgumentNullException`, providing a clear error rather
than a null-reference failure during I/O.
This scenario is tested by `CppEmitter_Emit_NullFactory_ThrowsArgumentNullException`.

**GradualDisclosure format produces multiple files**: Verifies that calling `Emit`
with `OutputFormat.GradualDisclosure` results in more than one writer being created
by the factory, confirming dispatch to `CppEmitterGradualDisclosure`.
This scenario is tested by `CppEmitter_Emit_GradualDisclosureFormat_ProducesMultipleFiles`.

**SingleFile format produces a single api file**: Verifies that calling `Emit` with
`OutputFormat.SingleFile` results in exactly one writer keyed as `"api"`, confirming
dispatch to `CppEmitterSingleFile`.
This scenario is tested by `CppEmitter_Emit_SingleFileFormat_ProducesSingleApiFile`.

**SanitizeFileName leaves regular names unchanged**: Verifies that
`SanitizeFileName("MyClass")` returns `"MyClass"` unchanged when the input contains
no invalid file-name characters.
This scenario is tested by `CppEmitter_SanitizeFileName_RegularName_IsUnchanged`.

**BuildClassDeclaration for non-final class with no base**: Verifies that
`BuildClassDeclaration` returns `"class Circle"` for a class with no base types and
`IsFinal == false`.
This scenario is tested by
`CppEmitter_BuildClassDeclaration_NonFinalNoBase_ReturnsJustClassName`.

**BuildClassDeclaration for final class appends final keyword**: Verifies that
`BuildClassDeclaration` includes the word `"final"` in the result when `IsFinal` is true.
This scenario is tested by
`CppEmitter_BuildClassDeclaration_FinalClass_AppendsFinalKeyword`.
