## DocumentationCoverageChecker

### Verification Approach

`DocumentationCoverageChecker` is unit-tested against the shared
`ApiMark.Cpp.Fixtures` header set, using real clang parses of individual
fixture headers scoped via `ApiHeaderPatterns` so each test only invokes
clang against a small, deterministic file set. `SampleClass.h`'s intentionally
undocumented public `Refresh()` method (the same fixture pattern used for the
.NET checker's real-fixture test) verifies the checker reports real
undocumented declarations end-to-end; `ProtectedMembersClass.h` and
`DeprecatedClass.h`, both fully documented, verify tier and deprecated-filter
behavior without introducing new undocumented fixture content. No mocking is
required. Tests exercise both the checker indirectly through
`CppGenerator.CheckDocumentationCoverage` and the enforcement-tier
validation/fallback contract of `CppGenerator` itself.

### Test Environment

Tests require clang to be discoverable on the host (via `ClangPath`,
`APIMARK_CLANG_PATH`, PATH, or platform-specific discovery), since
`CppGenerator.Parse` invokes clang to produce the AST consumed by the
checker. In sandboxes without clang installed, this test project is expected
to fail to run — a pre-existing environmental limitation of the whole
`ApiMark.Cpp.Tests` project, not specific to documentation-coverage
enforcement.

### Acceptance Criteria

- All `DocumentationCoverageChecker` tests pass with zero failures (on a host
  with clang installed).
- A fully documented header reports zero violations.
- The real `SampleClass.h` fixture's undocumented `Refresh()` method is
  reported as a `Function` violation with the qualified display name
  `fixtures::SampleClass::Refresh()`, including its empty parameter-signature
  suffix.
- Every violation is labeled with the correct `Kind` string, and every
  `Class`, `Function`, `Field`, `Enum`, `EnumValue`, and `TypeAlias` kind the
  checker can produce is exercised against a genuinely undocumented
  declaration of that kind.
- Undocumented overloaded constructors, methods, and free functions are each
  reported with a distinct `DisplayName` that includes a parenthesized
  parameter-type signature, so overloads of the same name never collapse
  into a single, ambiguous violation.
- The `PublicAndProtected` enforcement tier scans more declarations than the
  `Public` tier, independent of the emission `Visibility` tier.
- `[[deprecated]]` declarations are skipped when `IncludeDeprecated` is
  `false` (the default) and scanned when `true`.
- `CppGenerator.CheckDocumentationCoverage` throws `InvalidOperationException`
  when called before `Parse`, throws `ArgumentException` for an unrecognized
  tier value, falls back to `CppGeneratorOptions.EnforceDocsVisibility` when
  the method parameter is `null` or an empty string, and throws
  `InvalidOperationException` when no tier is configured by either source.

### Test Scenarios

**Fully documented header reports zero violations**: Verifies that scanning
`ProtectedMembersClass.h` (fully documented at every visibility tier) at the
`All` tier reports zero violations. This scenario is tested by
`Check_FullyDocumentedHeader_ReportsZeroViolations`.

**Real fixture header reports its undocumented function**: Verifies that
scanning the real `SampleClass.h` fixture reports its intentionally
undocumented `Refresh()` method as a `Function`-kind violation with display
name `fixtures::SampleClass::Refresh()`. This scenario is tested by
`Check_RealFixtureHeader_ReportsUndocumentedFunction`.

**Various declaration kinds report the expected kind labels**: Verifies that
every kind the checker can produce (`Class`, `Function`, `Field`, `Enum`,
`EnumValue`, `TypeAlias`) is actually observed, using a dedicated
`UndocumentedKindsFixture.h` header containing one genuinely undocumented
declaration of each kind. This scenario is tested by
`Check_VariousDeclarationKinds_ReportsExpectedKindLabels`.

**Overloaded undocumented constructor reports a distinct parameter
signature**: Verifies that an undocumented constructor overload is reported
with its parameter-type signature appended to its `DisplayName`
(`fixtures::UndocumentedKindsClass::UndocumentedKindsClass(...)`),
distinguishing it from the documented, unreported no-argument constructor
overload of the same class. This scenario is tested by
`Check_OverloadedUndocumentedConstructor_ReportsDistinctParameterSignature`.

**Overloaded undocumented method reports a distinct parameter signature**:
Verifies that an undocumented method overload (`DoWork(int)`) is reported
with its parameter-type signature, distinguishing it from the documented,
unreported no-argument overload (`DoWork()`) of the same name. This scenario
is tested by
`Check_OverloadedUndocumentedMethod_ReportsDistinctParameterSignature`.

**Overloaded undocumented free function reports a distinct parameter
signature**: Verifies that two undocumented free-function overloads sharing
the same name are each reported with a distinct `DisplayName` that includes
their respective parameter-type signature, so neither collapses into the
other. This scenario is tested by
`Check_OverloadedUndocumentedFreeFunction_ReportsDistinctParameterSignature`.

**PublicAndProtected tier surfaces protected members not seen at Public**:
Verifies that the enforcement visibility tier is evaluated independently of
the emission `Visibility` tier — scanning at `PublicAndProtected` checks more
declarations than scanning at `Public`, since `SampleClass.h`'s protected
`OnNameChanged()` member enters the scan only at the wider tier. This
scenario is tested by
`Check_PublicAndProtectedTier_SurfacesProtectedMembersNotSeenAtPublic`.

**Deprecated declarations are filtered by IncludeDeprecated**: Verifies that
`DeprecatedClass.h`'s `[[deprecated]]` class and method are excluded from the
scan entirely (zero `CheckedCount`) when `IncludeDeprecated` is `false`, and
included when `true`. This scenario is tested by
`Check_DeprecatedClass_FilteredByIncludeDeprecatedOption`.

**CheckDocumentationCoverage before Parse throws**: Verifies that calling
`CppGenerator.CheckDocumentationCoverage` before `Parse` has completed throws
`InvalidOperationException` naming `Parse` in its message. This scenario is
tested by
`CppGenerator_CheckDocumentationCoverage_BeforeParse_ThrowsInvalidOperationException`.

**Invalid enforcement tier throws ArgumentException**: Verifies that an
unrecognized tier string (`"NotAVisibilityTier"`) throws `ArgumentException`.
This scenario is tested by
`CppGenerator_CheckDocumentationCoverage_InvalidEnforceTier_ThrowsArgumentException`.

**Null enforcement tier falls back to the configured options value**:
Verifies that passing `null` as the tier parameter falls back to
`CppGeneratorOptions.EnforceDocsVisibility` when it is set. This scenario is
tested by
`CppGenerator_CheckDocumentationCoverage_NullEnforceTier_FallsBackToOptionsValue`.

**Empty-string enforcement tier falls back to the configured options value**:
Verifies that passing an empty string (as opposed to `null`) as the tier
parameter falls back to `CppGeneratorOptions.EnforceDocsVisibility`
identically, confirming the `null`/empty-string boundary is treated
consistently. This scenario is tested by
`CppGenerator_CheckDocumentationCoverage_EmptyStringEnforceTier_FallsBackToOptionsValue`.

**No tier configured by either source throws InvalidOperationException**:
Verifies that calling with a null tier parameter, when
`EnforceDocsVisibility` is also left unset, throws
`InvalidOperationException` naming `EnforceDocsVisibility` in its message.
This scenario is tested by
`CppGenerator_CheckDocumentationCoverage_NoTierConfigured_ThrowsInvalidOperationException`.
