## DocumentationCoverageChecker

### Verification Approach

`DocumentationCoverageChecker` is unit-tested against the shared `ApiMark.DotNet.Fixtures`
compiled test assembly, using its real, compiler-generated XML documentation for some scenarios
and hand-written temporary XML documentation files (written and deleted by each test) for
scenarios that need to control which types/members are documented. Because the Fixtures assembly
and its compiler-generated XML doc cannot be hand-edited for individual assertions, tests that
need to isolate a single fixture type from the rest of the assembly use an `ExcludeAllExcept`
helper that generates exact-match `--exclude`-style wildcard patterns for every top-level type
except the one(s) under test, scoping `Check` down to a single fixture type without ever
modifying the Fixtures project. No mocking is required; the unit's only dependencies
(`DotNetEmitter` and `DotNetGenerator` static predicates, `XmlDocReader`) are exercised directly.

### Test Environment

Tests require the compiled `ApiMark.DotNet.Fixtures` test assembly and its
XML documentation file (built as part of the normal test build) for the
real-fixture scenarios, and write access to the temporary file system path
for hand-built XML doc fixtures. No external service or network dependency
is needed.

### Acceptance Criteria

- All `DocumentationCoverageChecker` tests pass with zero failures.
- A fully documented scope (every type/member has a non-empty `<summary>`)
  reports zero violations, and `CheckedCount` matches the number of types and
  members scanned.
- Scanning the real `ApiMark.DotNet.Fixtures` assembly's XML doc reports the
  expected undocumented method.
- A type missing a `<summary>` is reported as a `Type` violation.
- A method missing a `<summary>` is reported as a `Method` violation.
- A property missing a `<summary>` is reported as a `Property` violation.
- A field missing a `<summary>` is reported as a `Field` violation.
- An event missing a `<summary>` is reported as an `Event` violation.
- A fully documented type with a fully documented nested type reports zero
  violations at both levels, and `CheckedCount` includes both levels.
- A nested type member missing a `<summary>` is reported as a violation
  attributed to the nested type.
- The `PublicAndProtected` enforcement tier includes protected members that
  the `Public` tier does not see, independent of the emission
  `DotNetGeneratorOptions.Visibility` tier.
- A type matched by an exclude pattern is skipped entirely — not counted in
  `CheckedCount` and not reported as a violation.
- When `includeObsolete` is `false` (the default), an obsolete type is
  skipped entirely.
- When `includeObsolete` is `true`, an obsolete type is scanned and its
  missing summary is reported.
- A `NamespaceDoc` carrier type is never checked, regardless of its
  documentation state.
- A record's synthesized `EqualityContract` property is never reported as
  undocumented, for a sealed record (`All` visibility), a non-sealed public
  record (`PublicAndProtected` visibility), and a derived record (`All`
  visibility), while a hand-written property of the same name on a normal
  class is still reported.
- A field-like event's compiler-generated backing field is never reported as
  undocumented when the event itself is documented.
- A method carrying `GeneratedCodeAttribute` with only a `<remarks>` (no
  `<summary>`) is not reported as undocumented, while a hand-written method
  with only a `<remarks>` is still reported.

### Test Scenarios

**Fully documented scope reports zero violations**: Verifies that when every
type and member in the scanned scope has a non-empty `<summary>`,
`Check` returns an empty `UndocumentedItems` list and a `CheckedCount`
equal to the number of items scanned (including the compiler-generated
implicit parameterless constructor, which is hand-documented in this test's
XML fixture so it does not itself appear as a violation). This scenario is
tested by `Check_FullyDocumentedScope_ReportsZeroViolations`.

**Real fixture XML doc reports the expected undocumented method**: Verifies
that scanning the real, compiler-generated XML documentation for
`ApiMark.DotNet.Fixtures` reports the known undocumented method as a
violation. This scenario is tested by
`Check_RealFixtureDoc_ReportsUndocumentedMethod`.

**Type missing a summary is reported as a Type violation**: Verifies that a
type with no `<summary>` element produces an `UndocumentedApiItem` with
`Kind == "Type"` and the type's full name as
`DisplayName`. This scenario is tested by
`Check_TypeMissingSummary_ReportsTypeViolation`.

**Method missing a summary is reported as a Method violation**: Verifies
that an undocumented method produces a `Method`-kind violation whose
`DisplayName` includes the method's simplified parameter list (e.g.
`GetGreeting(string)`), matching `DotNetEmitter`'s display-name format. This
scenario is tested by `Check_MethodMissingSummary_ReportsMethodViolation`.

**Property missing a summary is reported as a Property violation**: Verifies
that an undocumented property produces a `Property`-kind violation. This
scenario is tested by
`Check_PropertyMissingSummary_ReportsPropertyViolation`.

**Field missing a summary is reported as a Field violation**: Verifies that
an undocumented field produces a `Field`-kind violation. This scenario is
tested by `Check_FieldMissingSummary_ReportsFieldViolation`.

**Event missing a summary is reported as an Event violation**: Verifies that
an undocumented event produces an `Event`-kind violation. This scenario is
tested by `Check_EventMissingSummary_ReportsEventViolation`.

**Nested type fully documented checks both levels with zero violations**:
Verifies that recursion into a fully documented nested type contributes to
`CheckedCount` without producing any violations. This scenario is tested by
`Check_NestedTypeFullyDocumented_ChecksBothLevelsWithZeroViolations`.

**Nested type member missing a summary reports a violation on the nested
type**: Verifies that a violation inside a nested type is attributed to the
nested type's qualified name, not the containing type. This scenario is
tested by `Check_NestedTypeMemberMissingSummary_ReportsViolationOnNestedType`.

**PublicAndProtected tier includes protected members not seen at the Public
tier**: Verifies that the enforcement visibility tier is evaluated
independently of the emission `Visibility` tier — a protected member invisible
at `ApiVisibility.Public` is scanned (and, if undocumented, reported) at
`ApiVisibility.PublicAndProtected`. This scenario is tested by
`Check_PublicAndProtectedTier_IncludesProtectedMembersNotSeenAtPublicTier`.

**Exclude pattern matches type; type is not checked**: Verifies that a type
matched by an exclude pattern contributes neither to `CheckedCount` nor to
`UndocumentedItems`, mirroring the emission exclude-pattern filter. This
scenario is tested by `Check_ExcludePatternMatchesType_TypeIsNotChecked`.

**IncludeObsolete false skips an obsolete type**: Verifies that an obsolete
type is entirely excluded from the scan (not counted, not reported) when
`includeObsolete` is `false`, the default enforcement behavior. This
scenario is tested by `Check_IncludeObsoleteFalse_SkipsObsoleteType`.

**IncludeObsolete true scans an obsolete type and reports its missing
summary**: Verifies that when `includeObsolete` is `true`, an obsolete type
is scanned and its missing `<summary>` is reported like any other type. This
scenario is tested by
`Check_IncludeObsoleteTrue_ScansObsoleteTypeAndReportsMissingSummary`.

**NamespaceDoc carrier is never checked**: Verifies that an
`internal static class NamespaceDoc` carrier type is always excluded from
the scan, regardless of its own documentation state — it is not a
documentable API surface member. This scenario is tested by
`Check_NamespaceDocCarrier_IsNeverChecked`.

**Sealed record at All visibility does not report EqualityContract**:
Verifies that a sealed record's `private` `EqualityContract` property
(visible only at `ApiVisibility.All`) is excluded from the scan. This
scenario is tested by
`Check_SealedRecordAtAllVisibility_DoesNotReportEqualityContract`.

**Non-sealed public record at PublicAndProtected does not report
EqualityContract**: Verifies that a non-sealed record's `protected virtual`
`EqualityContract` property is excluded at `ApiVisibility.PublicAndProtected`,
not just at `All`. This scenario is tested by
`Check_NonSealedPublicRecordAtPublicAndProtected_DoesNotReportEqualityContract`.

**Derived record at All visibility does not report EqualityContract**:
Verifies that a derived record's `protected override` `EqualityContract`
property is also excluded. This scenario is tested by
`Check_DerivedRecordAtAllVisibility_DoesNotReportEqualityContract`.

**Hand-written EqualityContract property is still reported**: Verifies that
a hand-written, non-compiler-generated property named `EqualityContract` on
a normal (non-record) class is still reported as undocumented, confirming
the exclusion is scoped to compiler-generated members only, not to the name.
This scenario is tested by
`Check_HandWrittenEqualityContractProperty_IsStillReported`.

**Documented field-like event does not report its backing field**: Verifies
that a field-like event's compiler-generated backing field (which shares the
event's exact name) is not separately reported as an undocumented `Field`
when the event itself carries a `<summary>`. This scenario is tested by
`Check_DocumentedFieldLikeEvent_DoesNotReportBackingField`.

**GeneratedRegex method with only remarks is not reported**: Verifies that a
`[GeneratedRegex]`-generated partial method, whose compiled XML doc carries
only a `<remarks>` (the source generator having replaced the author's
`<summary>`), is not reported as undocumented because it carries
`GeneratedCodeAttribute`. This scenario is tested by
`Check_GeneratedRegexMethodWithOnlyRemarks_IsNotReported`.

**Hand-written method with only remarks is still reported**: Verifies that a
hand-written method with only a `<remarks>` (no `GeneratedCodeAttribute`) is
still reported as undocumented, confirming the `<remarks>` fallback is
scoped to generated-code members only. This scenario is tested by
`Check_HandWrittenMethodWithOnlyRemarks_IsStillReported`.
