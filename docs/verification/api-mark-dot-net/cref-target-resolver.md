## CrefTargetResolver

### Verification Approach

`CrefTargetResolver` is unit-tested in `test/ApiMark.DotNet.Tests/` using real
Mono.Cecil type/member definitions obtained from the fixture assembly. Tests
construct a resolver over the fixture assembly and verify that
`TryResolveType`/`TryResolveMember` succeed for every symbol kind indexed (a
top-level type, a nested type, a method, a field) and fail for identifiers
that do not exist in the assembly (unknown identifier, external-assembly
type, and a malformed cref string). No visibility filtering is exercised by
these tests — CrefTargetResolver applies none — including a test that proves
a private member still resolves (visibility filtering is explicitly a
separate, caller-supplied concern). No mocking is required.

### Test Environment

Tests require the compiled fixture assembly so that real Mono.Cecil type and
member definitions can be obtained. No external service, network dependency,
or writable output location is needed.

### Acceptance Criteria

- All `CrefTargetResolver` tests pass with zero failures.
- A top-level type's XML-doc identifier resolves via `TryResolveType`.
- A nested type's XML-doc identifier resolves via `TryResolveType`.
- An unknown type identifier returns `false` from `TryResolveType`.
- An external-assembly type's identifier (e.g. a `System.*` type) returns `false` from `TryResolveType`.
- A method's XML-doc identifier resolves via `TryResolveMember`.
- A private member's XML-doc identifier still resolves via `TryResolveMember` — CrefTargetResolver applies no visibility filtering.
- An unknown member identifier returns `false` from `TryResolveMember`.
- A malformed cref string returns `false` from `TryResolveType`.
- A field's XML-doc identifier resolves via `TryResolveMember`.

### Test Scenarios

**Top-level type resolves via TryResolveType**: Verifies that a well-formed
`T:` identifier for a top-level type declared in the indexed assembly
resolves to that type. This scenario is tested by
`CrefTargetResolver_TryResolveType_TopLevelType_ReturnsTrueAndType`.

**Nested type resolves via TryResolveType**: Verifies that a `T:` identifier
for a nested type resolves correctly, confirming that the constructor's walk
over `ModuleDefinition.GetTypes()` — Mono.Cecil's flattening enumerator —
indexes nested types at every depth. This scenario is tested by
`CrefTargetResolver_TryResolveType_NestedType_ReturnsTrueAndType`.

**Unknown type identifier returns false**: Verifies that a syntactically
well-formed but non-existent `T:` identifier is not resolved. This scenario
is tested by
`CrefTargetResolver_TryResolveType_UnknownIdentifier_ReturnsFalse`.

**External-assembly type identifier returns false**: Verifies that a `T:`
identifier for a type declared in a different assembly (e.g.
`System.ArgumentNullException`) is not resolved, confirming that
CrefTargetResolver only indexes the single assembly it was constructed over.
This scenario is tested by
`CrefTargetResolver_TryResolveType_ExternalAssemblyType_ReturnsFalse`.

**Method resolves via TryResolveMember**: Verifies that a well-formed `M:`
identifier for a method declared in the indexed assembly resolves to that
method. This scenario is tested by
`CrefTargetResolver_TryResolveMember_Method_ReturnsTrueAndMember`.

**Private member still resolves**: Verifies that CrefTargetResolver applies no
visibility filtering of its own — a private member's `M:` identifier still
resolves via `TryResolveMember`, confirming that visibility-based exclusion
from the generated documentation is strictly a separate, caller-supplied
concern layered on top of this resolver. This scenario is tested by
`CrefTargetResolver_TryResolveMember_PrivateMember_StillResolves`.

**Unknown member identifier returns false**: Verifies that a syntactically
well-formed but non-existent member identifier is not resolved. This scenario
is tested by
`CrefTargetResolver_TryResolveMember_UnknownIdentifier_ReturnsFalse`.

**Malformed cref string returns false**: Verifies that a malformed/unparsable
cref string (not matching any indexed identifier) returns `false` from
`TryResolveType` rather than throwing, matching the existing fallback
rendering behavior for crefs that cannot be resolved. This scenario is
tested by `CrefTargetResolver_TryResolveType_MalformedCref_ReturnsFalse`.

**Field resolves via TryResolveMember**: Verifies that a well-formed `F:`
identifier for a field declared in the indexed assembly resolves to that
field, confirming that all four member kinds (`Methods`/`Properties`/
`Fields`/`Events`) are indexed, not just methods. This scenario is tested by
`CrefTargetResolver_TryResolveMember_Field_ReturnsTrueAndMember`.
