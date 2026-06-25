## CppAstModel

### Verification Approach

`CppAstModel` record types are unit-tested in `test/ApiMark.Cpp.Tests/CppAstModelTests.cs`
without any test doubles. Each record type is constructed directly and asserted for stored
property values, optional null defaults, value equality, and core behavioral flags.

### Test Environment

No external services, network access, clang installation, or file system access are required.
Tests run with the standard xUnit.net test runner.

### Acceptance Criteria

- Every record type stores its constructor parameters in the correct properties.
- `CppParameter.DefaultValue`, `CppDocComment.Note`, and `CppDocComment.Example` are `null`
  when omitted.
- `CppDocComment` implements value equality.
- `CppAccessibility` contains exactly `Public`, `Protected`, and `Private`.
- `CppField`, `CppFunction`, and `CppClass` expose their core behavioral flags correctly.

### Test Scenarios

**CppDocComment note/example default to null**: Verifies that constructing `CppDocComment`
without `Note` or `Example` leaves both properties `null`, matching the documented optional
metadata contract. This scenario is tested by
`CppDocComment_NoteAndExample_WhenNotProvided_AreNull`.

**Record types store constructor parameters**: Verifies that `CppField`, `CppFunction`, and
`CppClass` each store their constructor arguments in the correct properties. Tested by
`CppField_Construction_SetsCoreProperties`, `CppFunction_Construction_SetsCoreProperties`,
and `CppClass_Construction_SetsCoreProperties`.

**CppParameter.DefaultValue is null when omitted**: Verifies that constructing a
`CppParameter` without a default value leaves `DefaultValue` as `null`. Tested by
`CppParameter_DefaultValue_WhenNotProvided_IsNull`.

**CppDocComment implements value equality**: Verifies that two `CppDocComment` instances
constructed with identical arguments compare as equal, confirming record-type value
semantics. Tested by `CppDocComment_Equality_TwoIdenticalInstances_AreEqual`.

**CppAccessibility contains Public, Protected, Private**: Verifies that the
`CppAccessibility` enum exposes exactly these three values, matching the documented
member-access contract. Tested by `CppAccessibility_Values_ArePublicProtectedPrivate`.

**CppFunction.IsDeleted flag is correctly stored**: Verifies that a `CppFunction`
constructed with `IsDeleted = false` reports that value correctly from the `IsDeleted`
property. Tested by `CppFunction_Construction_SetsCoreProperties`.

**CppClass.IsFinal flag is correctly stored**: Verifies that a `CppClass` constructed with
`IsFinal = false` reports that value correctly. Tested by
`CppClass_Construction_SetsCoreProperties`.

**CppField.IsDeprecated flag**: Verifies that a `CppField` constructed with `IsDeprecated = false`
reports that value correctly from the `IsDeprecated` property. Tested by
`CppField_Construction_SetsCoreProperties`.

**CppDocComment constructor stores Params and Returns**: Verifies that constructing `CppDocComment`
with an empty params list and a returns string correctly stores both in `Params` and `Returns`.
Tested by `CppDocComment_Construction_SetsSummaryAndDetails`.

**CppFunction boolean flags default correctly**: Verifies that a `CppFunction` constructed with all
boolean flags set to `false` (IsStatic, IsVirtual, IsVariadic, IsDeprecated) reports each correctly.
Tested by `CppFunction_Construction_SetsCoreProperties`.

**CppClass.IsDeprecated flag**: Verifies that a `CppClass` constructed with `IsDeprecated = false`
reports that value correctly from the `IsDeprecated` property. Tested by
`CppClass_Construction_SetsCoreProperties`.

**CppSourceLocation stores file and line**: Verifies that `CppSourceLocation_Construction_SetsFileAndLine`
correctly stores the file path in `File` and the line number in `Line`.

**CppParamDoc stores name and description**: Verifies that `CppParamDoc_Construction_SetsNameAndDescription`
correctly stores the parameter name and description string.

**CppBaseType stores name**: Verifies that `CppBaseType_Construction_SetsName` correctly stores the
base class name.

**CppTemplateParam stores name**: Verifies that `CppTemplateParam_Construction_SetsName` correctly
stores the template parameter name.

**CppEnumValue stores name and doc**: Verifies that `CppEnumValue_Construction_SetsNameAndDoc`
correctly stores the enumerator name and its optional doc comment.

**CppParameter stores name and type**: Verifies that `CppParameter_Construction_SetsNameAndTypeName`
correctly stores the parameter name and type string.

**CppEnum stores name and values**: Verifies that `CppEnum_Construction_SetsNameAndValues` correctly
stores the enum name and the list of declared enumerator values.

**CppTypeAlias stores name and underlying type**: Verifies that
`CppTypeAlias_Construction_SetsNameAndUnderlyingType` correctly stores the alias name and its
underlying type string.

**CppNamespaceDecl stores qualified name**: Verifies that
`CppNamespaceDecl_Construction_SetsQualifiedName` correctly stores the fully-qualified namespace name.

**CppCompilationResult stores namespaces and errors**: Verifies that
`CppCompilationResult_Construction_SetsNamespacesAndErrors` correctly stores the namespace list and
the error string collection.
