## CppAstModel

### Verification Approach

`CppAstModel` record types are unit-tested in `test/ApiMark.Cpp.Tests/CppAstModelTests.cs`
without any test doubles. Each record type is constructed directly using its C#
record primary constructor, and the resulting property values are asserted immediately.
Tests are organized into two groups: construction-and-validation tests (one per record
type, verifying basic property assignment) and core-properties tests (for the more
complex record types `CppField`, `CppFunction`, and `CppClass`, verifying behavioral
flag properties in addition to name and type). No clang installation or file system
access is required.

### Test Environment

No external services, network access, or file system access are required. Tests run with
the standard xUnit.net test runner.

### Acceptance Criteria

- Every record type stores its constructor parameters in the correct properties.
- Record types with default-value parameters (`CppParameter.DefaultValue`,
  `CppDocComment.Note`, `CppDocComment.Example`) report `null` when the parameter
  is omitted.
- `CppDocComment` implements value equality: two instances constructed with identical
  parameters are equal.
- `CppAccessibility` contains exactly the values `Public`, `Protected`, and `Private`.
- `CppField`, `CppFunction`, and `CppClass` expose their core behavioral flags
  (`IsStatic`, `IsConstructor`, `IsFinal`) correctly after construction.

### Test Scenarios

**CppSourceLocation stores file and line**: Verifies that constructing a
`CppSourceLocation("myfile.h", 42)` produces `File == "myfile.h"` and `Line == 42`.
This scenario is tested by `CppSourceLocation_Construction_SetsFileAndLine`.

**CppParamDoc stores name and description**: Verifies that `CppParamDoc("count",
"Number of items.")` stores both fields correctly.
This scenario is tested by `CppParamDoc_Construction_SetsNameAndDescription`.

**CppDocComment stores Summary and Details**: Verifies that `CppDocComment` stores
the `Summary` and `Details` parameters correctly after construction.
This scenario is tested by `CppDocComment_Construction_SetsSummaryAndDetails`.

**CppDocComment record equality**: Verifies that two `CppDocComment` instances
constructed with identical parameters are equal via C# record value equality.
This scenario is tested by `CppDocComment_Equality_TwoIdenticalInstances_AreEqual`.

**CppBaseType stores name**: Verifies that `CppBaseType("Shape")` stores `Name ==
"Shape"`.
This scenario is tested by `CppBaseType_Construction_SetsName`.

**CppTemplateParam stores name**: Verifies that `CppTemplateParam("T")` stores
`Name == "T"`.
This scenario is tested by `CppTemplateParam_Construction_SetsName`.

**CppEnumValue stores name and doc**: Verifies that `CppEnumValue` stores `Name`
and `Doc` correctly.
This scenario is tested by `CppEnumValue_Construction_SetsNameAndDoc`.

**CppParameter stores name and type name**: Verifies that `CppParameter("radius",
"double")` stores `Name == "radius"` and `TypeName == "double"`.
This scenario is tested by `CppParameter_Construction_SetsNameAndTypeName`.

**CppParameter default value is null when not provided**: Verifies that
`CppParameter("radius", "double")` has `DefaultValue == null` because the optional
parameter was not supplied.
This scenario is tested by `CppParameter_DefaultValue_WhenNotProvided_IsNull`.

**CppEnum stores name and values**: Verifies that `CppEnum` stores `Name` and a
`Values` list with the correct count.
This scenario is tested by `CppEnum_Construction_SetsNameAndValues`.

**CppTypeAlias stores name and underlying type**: Verifies that `CppTypeAlias`
stores `Name` and `UnderlyingTypeName` correctly.
This scenario is tested by `CppTypeAlias_Construction_SetsNameAndUnderlyingType`.

**CppNamespaceDecl stores qualified name**: Verifies that `CppNamespaceDecl` stores
`QualifiedName` correctly.
This scenario is tested by `CppNamespaceDecl_Construction_SetsQualifiedName`.

**CppCompilationResult stores namespaces and errors**: Verifies that
`CppCompilationResult` stores `Namespaces` and `Errors` correctly.
This scenario is tested by `CppCompilationResult_Construction_SetsNamespacesAndErrors`.

**CppAccessibility has expected values**: Verifies that `CppAccessibility` contains
`Public`, `Protected`, and `Private` enum values.
This scenario is tested by `CppAccessibility_Values_ArePublicProtectedPrivate`.

**CppField stores core properties**: Verifies that `CppField` stores `Name`,
`TypeName`, `Accessibility`, and `IsStatic` correctly after construction.
This scenario is tested by `CppField_Construction_SetsCoreProperties`.

**CppFunction stores core properties**: Verifies that `CppFunction` stores `Name`,
`ReturnTypeName`, `Accessibility`, and `IsConstructor` correctly after construction.
This scenario is tested by `CppFunction_Construction_SetsCoreProperties`.

**CppClass stores core properties**: Verifies that `CppClass` stores `Name`,
`BaseTypes`, and `IsFinal` correctly after construction.
This scenario is tested by `CppClass_Construction_SetsCoreProperties`.
