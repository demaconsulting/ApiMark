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

**CppField.IsDeprecated flag**: Verifies that `CppField` stores the `IsDeprecated` flag
supplied at construction time. Tested by `CppField_Construction_SetsCoreProperties`.
