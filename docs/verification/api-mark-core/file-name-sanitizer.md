## FileNameSanitizer

### Verification Approach

FileNameSanitizer is verified with unit tests in
`test/ApiMark.Core.Tests/FileNameSanitizerTests.cs` that call the real implementation
directly. End-to-end legality of generated output is verified by
`DotNetEmitterFileNameLegalityTests` in `ApiMark.DotNet.Tests`.

### Test Environment

N/A - standard test environment using the .NET test runner is sufficient.

### Acceptance Criteria

- All `FileNameSanitizerTests` test cases pass with zero failures.
- Angle brackets become braces; legal names are unchanged.
- Every illegal character, a trailing dot, and an empty name produce a legal segment.
- Reserved device names (with and without an extension part) are moved aside.
- Distinct lossy inputs remain distinct; `A<B>` and `A{B}` do not collide.
- Inputs imitating the sanitizer's own output forms do not collide with it.
- `IsLegalSegment` rejects every illegal shape and accepts legal names.

### Test Scenarios

**FileNameSanitizer_SanitizeSegment_AngleBrackets_BecomeBraces**: Verifies
`IFoo<System.Int32>.Method` becomes `IFoo{System.Int32}.Method`.

**FileNameSanitizer_SanitizeSegment_LegalName_IsUnchanged**: Verifies ordinary names,
hyphenated arity names, dotted names, and non-ASCII letters are unchanged.

**FileNameSanitizer_SanitizeSegment_IllegalCharacter_IsReplacedWithHashSuffix**: Verifies
each illegal character yields a legal segment ending in the hash of the original.

**FileNameSanitizer_SanitizeSegment_TrailingDot_IsLegal**: Verifies a trailing dot does
not survive.

**FileNameSanitizer_SanitizeSegment_ReservedDeviceName_IsMovedAside**: Verifies `CON`,
`con`, `Aux`, `NUL`, `PRN`, `COM1`, `lpt9`, and `Con.Foo` gain a `-` suffix while `CON1`
and `Console` are unchanged.

**FileNameSanitizer_SanitizeSegment_EmptyName_ReturnsUnderscore**: Verifies an empty name
becomes `_`.

**FileNameSanitizer_SanitizeSegment_NullName_ThrowsArgumentNullException**: Verifies a
null argument is rejected.

**FileNameSanitizer_SanitizeSegment_DistinctLossyInputs_StayDistinctAndDeterministic**:
Verifies different lossy inputs map to different segments and the mapping is
deterministic.

**FileNameSanitizer_SanitizeSegment_AngleBracketsVersusLiteralBraces_AreDistinct**:
Verifies `A<B>` becomes `A{B}` while `A{B}` gets a hash suffix and stays legal.

**FileNameSanitizer_SanitizeSegment_InputsImitatingOutputForms_AreDistinct**: Verifies
inputs that equal a hashed output, `CON-`, or `Con-.Foo` do not collide with the output of
the names they imitate.

**FileNameSanitizer_IsLegalSegment_IllegalName_ReturnsFalse**: Verifies each illegal shape
(null, empty, illegal characters, control characters, trailing dot or space, reserved
names including superscript variants) is rejected.

**FileNameSanitizer_IsLegalSegment_LegalName_ReturnsTrue**: Verifies braces and hyphens
are accepted.
