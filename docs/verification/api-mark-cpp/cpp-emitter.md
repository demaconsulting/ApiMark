## CppEmitter

### Verification Approach

`CppEmitter` is unit-tested in `test/ApiMark.Cpp.Tests/CppEmitterTests.cs` without invoking
clang. Tests construct controlled namespace/type data and capture output with an
`InMemoryMarkdownWriterFactory`.

### Test Environment

No external services, network access, clang installation, or file system access are required.
Tests run with the standard xUnit.net test runner.

### Acceptance Criteria

- `CppEmitter.Emit` rejects a null factory.
- Gradual-disclosure dispatch produces multiple files.
- Single-file dispatch produces exactly one `api` file.
- `SanitizeFileName` preserves valid names and replaces invalid characters with underscores.
- `BuildClassDeclaration` renders non-final, final, and inherited class declarations correctly.
- `WriteCombinedMemberPage` emits one shared page for case-insensitive member collisions.

### Test Scenarios

**Sanitize invalid characters**: Verifies that invalid file-name characters are replaced with
underscores instead of causing output-path failures. This scenario is tested by
`CppEmitter_SanitizeFileName_InvalidCharacters_AreReplacedWithUnderscore`.

**Null factory rejection**: Verifies that passing null to `CppEmitter.Emit` throws
`ArgumentNullException` before any I/O occurs. Tested by
`CppEmitter_Emit_NullFactory_ThrowsArgumentNullException`.

**GradualDisclosure dispatch produces multiple files**: Verifies that requesting
`OutputFormat.GradualDisclosure` results in more than one writer being created. Tested by
`CppEmitter_Emit_GradualDisclosureFormat_ProducesMultipleFiles`.

**SingleFile dispatch produces exactly one api file**: Verifies that requesting
`OutputFormat.SingleFile` creates exactly one writer keyed as `api`. Tested by
`CppEmitter_Emit_SingleFileFormat_ProducesSingleApiFile`.

**SanitizeFileName preserves valid names**: Verifies that names containing only valid
file-name characters are returned unchanged. Tested by
`CppEmitter_SanitizeFileName_RegularName_IsUnchanged`.

**BuildClassDeclaration with base types**: Verifies that base class names are appended to
the class declaration line in the form `class X : public Base`. Tested by
`CppEmitter_BuildClassDeclaration_WithBaseTypes_AppendsInheritanceList`.

**BuildClassDeclaration with final and base types**: Verifies that both the `final` keyword
and the inheritance list are correctly combined in the declaration string. Tested by
`CppEmitter_BuildClassDeclaration_FinalClassWithBaseTypes_AppendsFinalAndInheritance`.

**WriteCombinedMemberPage for case-insensitive collisions**: Verifies that members whose
names differ only in case are merged onto a single lowercase-keyed page. Tested by
`CppEmitter_WriteCombinedMemberPage_CaseInsensitiveCollision_ProducesSingleCombinedPage`.

**GetIncludePath returns relative path for matching root**: Verifies that a source file
residing under a configured public include root produces a root-relative, forward-slash
path. Tested by `CppEmitter_GetIncludePath_MatchingRoot_ReturnsRelativePath`.

**GetIncludePath returns full path when no root matches**: Verifies that a source file not
under any configured root produces the full normalized path. Tested by
`CppEmitter_GetIncludePath_NoMatchingRoot_ReturnsFileName`.

**WriteExternalTypesSection emits heading with entries**: Verifies that a non-empty
external-types set causes `WriteExternalTypesSection` to write an "External Types" heading.
Tested by `CppEmitter_WriteExternalTypesSection_WithEntries_WritesExternalTypesSection`.
