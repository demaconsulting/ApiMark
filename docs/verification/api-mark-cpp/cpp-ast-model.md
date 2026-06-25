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
metadata contract. Tested by
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

**CppSourceLocation stores file and line**: Verifies that `CppSourceLocation` stores the file path
in `File` and the line number in `Line` correctly. Tested by
`CppSourceLocation_Construction_SetsFileAndLine`.

**CppParamDoc stores name and description**: Verifies that `CppParamDoc` stores the parameter name
in `Name` and the description string in `Description` correctly. Tested by
`CppParamDoc_Construction_SetsNameAndDescription`.

**CppBaseType stores name**: Verifies that `CppBaseType` stores the base class name in `Name`
correctly. Tested by `CppBaseType_Construction_SetsName`.

**CppTemplateParam stores name**: Verifies that `CppTemplateParam` stores the template parameter
name in `Name` correctly. Tested by `CppTemplateParam_Construction_SetsName`.

**CppEnumValue stores name and doc**: Verifies that `CppEnumValue` stores the enumerator name in
`Name` and its optional doc comment in `Doc` correctly. Tested by
`CppEnumValue_Construction_SetsNameAndDoc`.

**CppParameter stores name and type**: Verifies that `CppParameter` stores the parameter name in
`Name` and the type string in `TypeName` correctly. Tested by
`CppParameter_Construction_SetsNameAndTypeName`.

**CppEnum stores name and values**: Verifies that `CppEnum` stores the enum name in `Name` and the
list of declared enumerator values in `Values` correctly. Tested by
`CppEnum_Construction_SetsNameAndValues`.

**CppTypeAlias stores name and underlying type**: Verifies that `CppTypeAlias` stores the alias name
in `Name` and its underlying type string in `UnderlyingTypeName` correctly. Tested by
`CppTypeAlias_Construction_SetsNameAndUnderlyingType`.

**CppNamespaceDecl stores qualified name**: Verifies that `CppNamespaceDecl` stores the
fully-qualified namespace name in `QualifiedName` correctly. Tested by
`CppNamespaceDecl_Construction_SetsQualifiedName`.

**CppCompilationResult stores namespaces and errors**: Verifies that `CppCompilationResult` stores
the namespace list in `Namespaces` and the error string collection in `Errors` correctly. Tested by
`CppCompilationResult_Construction_SetsNamespacesAndErrors`.
