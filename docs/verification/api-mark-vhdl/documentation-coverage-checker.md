## DocumentationCoverageChecker

### Verification Approach

`DocumentationCoverageChecker` is unit-tested against real parsed VHDL file
models. `mux.vhd`/`counter.vhd`/`common_types.vhd` (all fully documented, an
existing shared fixture) verify the zero-violation and tier-parity scenarios;
a dedicated new fixture, `undocumented.vhd`, intentionally leaves its entity,
generic, port, package, type, constant, component, and subprogram
undocumented, plus declares an architecture with an intentionally
undocumented internal signal, so a single test file can cover every
violation-kind scenario and the architecture-internals scope-boundary
scenario without disturbing any existing fully-documented fixture. No
mocking is required. Tests exercise both the checker indirectly through
`VhdlGenerator.CheckDocumentationCoverage` and the enforcement-tier
validation/fallback contract of `VhdlGenerator` itself.

### Test Environment

N/A - standard test environment using the .NET test runner is sufficient;
VHDL parsing does not depend on any external tool (unlike the C++ checker's
dependency on clang).

### Acceptance Criteria

- All `DocumentationCoverageChecker` tests pass with zero failures.
- A fully documented file (`counter.vhd`) reports zero violations, with a
  non-zero `CheckedCount`.
- An undocumented entity, its generic, and its ports are each reported with
  the correct `Kind` (`Entity`, `Generic`, `Port`) and qualified display name.
- An undocumented package, and its type, constant, component, and subprogram,
  are each reported with the correct `Kind` (`Package`, `Type`, `Constant`,
  `Component`, `Subprogram`) and qualified display name.
- An undocumented architecture-internal signal is **never** flagged — no
  violation's `Kind` or `DisplayName` references it — proving the
  public-interface-only scope boundary is honored, not merely coincidentally
  absent.
- `Public`, `PublicAndProtected`, and `All` all produce an identical
  `CheckedCount` and `UndocumentedCount`, since VHDL has no visibility
  concept.
- `VhdlGenerator.CheckDocumentationCoverage` throws
  `InvalidOperationException` when called before `Parse`, throws
  `ArgumentException` for an unrecognized tier value, falls back to
  `VhdlGeneratorOptions.EnforceDocsVisibility` when the method parameter is
  null or empty, and throws `InvalidOperationException` when no tier is
  configured by either source.

### Test Scenarios

**Fully documented files report zero violations**: Verifies that scanning
the fully documented `counter.vhd` fixture at the `Public` tier reports zero
violations with a positive `CheckedCount`. This scenario is tested by
`Check_FullyDocumentedFiles_ReportsZeroViolations`.

**Undocumented entity reports entity, generic, and port violations**:
Verifies that `undocumented.vhd`'s entity, generic, and both ports are each
reported with the correct `Kind` and qualified display name
(`undocumented_entity`, `undocumented_entity.WIDTH`,
`undocumented_entity.clk`, `undocumented_entity.y`). This scenario is tested
by `Check_UndocumentedEntity_ReportsEntityGenericAndPortViolations`.

**Undocumented package reports package and member violations**: Verifies
that `undocumented.vhd`'s package and its type, constant, component, and
subprogram are each reported with the correct `Kind` and qualified display
name. This scenario is tested by
`Check_UndocumentedPackage_ReportsPackageAndMemberViolations`.

**Architecture-internal signal is never flagged (scope boundary, not a
bug)**: Verifies that `undocumented.vhd`'s intentionally undocumented
architecture-internal signal (`internal_undocumented_signal`) never appears
in any violation's `DisplayName`, and that no violation uses a `"Signal"`
kind at all — proving architecture internals are genuinely out of scope for
this checker rather than merely happening to pass. This scenario is tested
by `Check_ArchitectureInternalSignal_IsNeverFlagged`.

**All three enforcement tiers produce identical results**: Verifies that
`Public`, `PublicAndProtected`, and `All` all report the same `CheckedCount`
and `UndocumentedCount` for the same file, confirming VHDL's lack of a
visibility concept is honored uniformly across the CLI vocabulary. This
scenario is tested by `Check_AllThreeEnforceTiers_ProduceIdenticalResults`.

**CheckDocumentationCoverage before Parse throws**: Verifies that calling
`VhdlGenerator.CheckDocumentationCoverage` before `Parse` has completed
throws `InvalidOperationException` naming `Parse` in its message. This
scenario is tested by
`VhdlGenerator_CheckDocumentationCoverage_BeforeParse_ThrowsInvalidOperationException`.

**Invalid enforcement tier throws ArgumentException**: Verifies that an
unrecognized tier string (`"NotAVisibilityTier"`) throws `ArgumentException`.
This scenario is tested by
`VhdlGenerator_CheckDocumentationCoverage_InvalidEnforceTier_ThrowsArgumentException`.

**Null enforcement tier falls back to the configured options value**:
Verifies that passing `null` as the tier parameter falls back to
`VhdlGeneratorOptions.EnforceDocsVisibility` when it is set. This scenario is
tested by
`VhdlGenerator_CheckDocumentationCoverage_NullEnforceTier_FallsBackToOptionsValue`.

**No tier configured by either source throws InvalidOperationException**:
Verifies that calling with a null tier parameter, when
`EnforceDocsVisibility` is also left unset, throws
`InvalidOperationException` naming `EnforceDocsVisibility` in its message.
This scenario is tested by
`VhdlGenerator_CheckDocumentationCoverage_NoTierConfigured_ThrowsInvalidOperationException`.
