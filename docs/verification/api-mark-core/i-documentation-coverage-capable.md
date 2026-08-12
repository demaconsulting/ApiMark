## IDocumentationCoverageCapable

### Verification Approach

`IDocumentationCoverageCapable` is a contract interface. Verification confirms
that the contract is invocable through a minimal inline stub
(`StubDocumentationCoverageCapable`) via `IDocumentationCoverageCapableTests`,
and that the shared `DocumentationCoverageResult` type correctly derives its
computed properties (`UndocumentedCount`, `HasViolations`) from the supplied
`UndocumentedApiItem` list. Per-language enforcement behavior (visibility
filtering, obsolete/deprecated filtering, invalid-tier rejection) is verified
separately in each language's own checker test suite
(`ApiMark.DotNet.Tests`, `ApiMark.Cpp.Tests`, `ApiMark.Vhdl.Tests`).

### Test Environment

N/A - standard test environment using the .NET test runner is sufficient for
IDocumentationCoverageCapable verification. Interface contract compliance is
enforced at compile time.

### Acceptance Criteria

- All `IDocumentationCoverageCapable` contract tests pass with zero failures.
- A stub implementation of `IDocumentationCoverageCapable` is invocable
  through an interface reference and returns the expected result.
- `DocumentationCoverageResult.UndocumentedCount` and `HasViolations` are
  correctly derived from the supplied `UndocumentedApiItem` list, both when
  violations are present and when the list is empty.
- Every `IDocumentationCoverageCapable` implementation falls back to its own
  configured enforcement tier when the method parameter is `null`/empty,
  rejects an unrecognized tier value with `ArgumentException`, and throws
  `InvalidOperationException` when called before `Parse` or when no tier is
  configured by either source — verified per-language in each of
  `ApiMark.DotNet.Tests`, `ApiMark.Cpp.Tests`, and `ApiMark.Vhdl.Tests`, since
  the interface itself defines no behavior of its own to test directly.

### Test Scenarios

**Documentation-coverage capability contract is satisfied**: Verifies that a
minimal stub implementation of `IDocumentationCoverageCapable` compiles
against the interface and can be invoked through an interface reference,
confirming the contract is correctly defined for `DotNetGenerator`,
`CppGenerator`, and `VhdlGenerator` to fulfill. This scenario is tested by
`ApiMarkCore_DocumentationCoverageContract_SupportedLanguage_CanBeInvoked`.

**DocumentationCoverageResult derives counts correctly with violations
present**: Verifies that `DocumentationCoverageResult.UndocumentedCount` and
`HasViolations` are correctly derived from a non-empty `UndocumentedApiItem`
list, and that `CheckedCount` and `UndocumentedItems` are exposed unchanged.
This scenario is tested by
`DocumentationCoverageResult_WithUndocumentedItems_DerivesCountsCorrectly`.

**DocumentationCoverageResult reports no violations for an empty item list**:
Verifies that `HasViolations` is `false` and `UndocumentedCount` is `0` when
the result is constructed with an empty `UndocumentedApiItem` list. This
scenario is tested by
`DocumentationCoverageResult_WithNoUndocumentedItems_HasViolationsIsFalse`.
