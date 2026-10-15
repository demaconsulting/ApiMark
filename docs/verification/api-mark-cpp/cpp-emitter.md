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
- File names come from the shared registry: illegal characters, template brackets, and reserved device names become legal forms once, and collisions are resolved up front.
- `BuildClassDeclaration` renders non-final, final, and inherited class declarations correctly.
- `WriteCombinedMemberPage` emits one shared page for case-insensitive member collisions.
- `WriteCombinedMemberPage` rejects a `members` list containing fewer than two elements with `ArgumentException`.
- `GetIncludePath` returns a root-relative path when the source file is under a configured public root; returns the full normalized path when no root matches.
- `GetIncludePath` rejects a null `sourceFile` with `ArgumentNullException`.
- `WriteExternalTypesSection` writes an H2 "External Types" heading and a table row per entry when the set is non-empty; writes nothing when the set is empty.

### Test Scenarios

**Registry-based file names**: Verifies that file names come from the shared registry: illegal characters are
replaced once, collisions are separated, and every link resolves. Tested by
`CppNames_ClassEnumAliasFunctionSameName_AreSeparatedInKindOrder`, `CppNames_CaseTwinClasses_SecondGetsSuffix`,
`CppNames_ReservedNames_AreNeverTaken`, `CppNames_TemplateAndDeviceNames_AreEscapedOnce`,
`CppNames_QualifiedNamespaceKey_IsOneEscapedSegment`, `CppNames_AngleTemplateAndLiteralBraceNames_AreDistinctPages`,
`CppEmitter_NestedTemplateInEscapedNamespace_LinkTargetEqualsWrittenPath`,
`CppEmitter_OverloadedFreeFunctions_ShareOnePageWithEveryOverload`,
`CppEmitter_ClassAndFunctionSameName_BothPagesWrittenAndLinked`, `CppEmitter_CollisionModel_AllPagesUniqueLegalAndLinksResolve`,
and `CppEmitter_CollisionModel_IsDeterministic`.

**Null factory rejection**: Verifies that passing null to `CppEmitter.Emit` throws
`ArgumentNullException` before any I/O occurs. Tested by
`CppEmitter_Emit_NullFactory_ThrowsArgumentNullException`.

**GradualDisclosure dispatch produces multiple files**: Verifies that requesting
`OutputFormat.GradualDisclosure` results in more than one writer being created. Tested by
`CppEmitter_Emit_GradualDisclosureFormat_ProducesMultipleFiles`.

**SingleFile dispatch produces exactly one api file**: Verifies that requesting
`OutputFormat.SingleFile` creates exactly one writer keyed as `api`. Tested by
`CppEmitter_Emit_SingleFileFormat_ProducesSingleApiFile`.

**Registry names are legal and unique**: Verified by `CppNames_*` and `CppEmitter_CollisionModel_*` tests.

**BuildClassDeclaration with final class**: Verifies that the `final` keyword is appended to the
class declaration when `CppClass.IsFinal` is true. Tested by
`CppEmitter_BuildClassDeclaration_FinalClass_AppendsFinalKeyword`.

**BuildClassDeclaration with base types**: Verifies that base class names are appended to
the class declaration line in the form `class X : public Base`. Tested by
`CppEmitter_BuildClassDeclaration_WithBaseTypes_AppendsInheritanceList`.

**BuildClassDeclaration non-final no-base**: Verifies that a non-final class with no base types
produces a declaration string containing only the class keyword and name. Tested by
`CppEmitter_BuildClassDeclaration_NonFinalNoBase_ReturnsJustClassName`.

**WriteCombinedMemberPage for case-insensitive collisions**: Verifies that members whose
names differ only in case are merged onto a single lowercase-keyed page. Tested by
`CppEmitter_WriteCombinedMemberPage_Overloads_ProducesSingleCombinedPage`.

**GetIncludePath returns relative path for matching root**: Verifies that a source file
residing under a configured public include root produces a root-relative, forward-slash
path. Tested by `CppEmitter_GetIncludePath_MatchingRoot_ReturnsRelativePath`.

**GetIncludePath rejects null source file**: Verifies that passing null to `GetIncludePath`
throws `ArgumentNullException` immediately, so callers that supply an invalid path receive a
clear error before any path processing is attempted. Tested by
`CppEmitter_GetIncludePath_NullSourceFile_ThrowsArgumentNullException`.

**GetIncludePath returns full path when no root matches**: Verifies that a source file not
under any configured root produces the full normalized path. Tested by
`CppEmitter_GetIncludePath_NoMatchingRoot_ReturnsFileName`.

**WriteCombinedMemberPage rejects too few members**: Verifies that a `members` list
containing only one element throws `ArgumentException`, enforcing the contract that a
combined page requires at least two members. Tested by
`CppEmitter_WriteCombinedMemberPage_TooFewMembers_ThrowsArgumentException`.

**WriteExternalTypesSection emits H2 heading with table rows**: Verifies that a non-empty
external-types set causes `WriteExternalTypesSection` to write an H2 "External Types" heading
and a table row for each entry containing the type name and namespace. Tested by
`CppEmitter_WriteExternalTypesSection_WithEntries_WritesExternalTypesSection`.

**WriteExternalTypesSection produces no output for empty set**: Verifies that an empty
external-types set causes `WriteExternalTypesSection` to write no headings and no tables.
Tested by `CppEmitter_WriteExternalTypesSection_EmptySet_WritesNothing`.

**BuildClassDeclaration final class with base types**: Verifies that a class marked both
`final` and with a public base type produces a declaration string containing both the `final`
keyword and the inheritance list in the correct order. Tested by
`CppEmitter_BuildClassDeclaration_FinalClassWithBaseTypes_AppendsFinalAndInheritance`.
