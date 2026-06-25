## ClangAstParser

### Verification Approach

`ClangAstParser` is integration-tested in `test/ApiMark.Cpp.Tests/ClangAstParserTests.cs`
using real fixture headers and a real clang installation. The tests exercise argument guards,
clang invocation, AST parsing, and representative namespace/type/member extraction.

### Test Environment

Tests require fixture headers in `test/ApiMark.Cpp.Fixtures/include/` and a system clang
installation accessible on PATH. When clang is not available, integration tests are skipped.

### Acceptance Criteria

- `Parse` rejects `null` headers with `ArgumentNullException`.
- `Parse` rejects a `null` options argument with `ArgumentNullException`.
- `Parse` rejects an empty header list with `ArgumentException`.
- `Parse` throws `InvalidOperationException` when an explicit clang path is invalid.
- Parsing the fixture headers returns a non-empty namespace set containing `fixtures`,
  `SampleClass`, and at least one member.
- `CppCompilationResult.Errors` contains only stderr error/fatal-error lines collected from
  clang (clean fixture parse produces an empty errors list).

### Test Scenarios

**Null headers rejected**: Verifies that passing a null header list to `Parse` throws
`ArgumentNullException` immediately. Tested by
`ClangAstParser_Parse_NullHeaders_ThrowsArgumentNullException`.

**Null options rejected**: Verifies that passing a null `CppGeneratorOptions` to `Parse`
throws `ArgumentNullException` immediately. Tested by
`ClangAstParser_Parse_NullOptions_ThrowsArgumentNullException`.

**Empty headers rejected**: Verifies that passing an empty header list to `Parse` throws
`ArgumentException`. Tested by `ClangAstParser_Parse_EmptyHeaders_ThrowsArgumentException`.

**Invalid explicit clang path rejected**: Verifies that specifying a non-existent clang
executable path causes `Parse` to throw `InvalidOperationException`. Tested by
`ClangAstParser_Parse_InvalidExplicitClangPath_ThrowsInvalidOperationException`.

**Fixture headers return non-empty namespace set**: Verifies that parsing the real fixture
headers produces at least one namespace in the result, confirming the full clang invocation
and AST-walking pipeline is wired correctly. Tested by
`ClangAstParser_Parse_FixtureHeaders_ReturnsNonEmptyNamespaces`.

**Fixture headers contain the fixtures namespace**: Verifies that the parsed result includes a
namespace named `fixtures`, confirming that the ownership filter correctly selects declarations
from the supplied headers. Tested by
`ClangAstParser_Parse_FixtureHeaders_ContainsFixturesNamespace`.

**Fixtures namespace contains SampleClass**: Verifies that `SampleClass` appears in the
`fixtures` namespace, confirming that class-level declaration parsing is correct. Tested by
`ClangAstParser_Parse_FixtureHeaders_FixturesNamespaceContainsSampleClass`.

**SampleClass has at least one member**: Verifies that the parsed `SampleClass` record
contains at least one method, confirming that member-level parsing within a class is functional.
Tested by `ClangAstParser_Parse_FixtureHeaders_SampleClassHasMembers`.

**Clean fixture parse produces empty errors list**: Verifies that `CppCompilationResult.Errors`
is empty when parsing well-formed fixture headers, confirming that `CollectStderrErrors`
correctly filters out non-error output and returns no false positives. Tested by
`ClangAstParser_Parse_FixtureHeaders_ErrorsCollectionIsEmpty`.

**Known integration-only gap**: Non-zero exit and malformed JSON paths are not isolated by the
current implementation without adding a process seam, so those behaviors remain covered only by
real-process integration when reproducible in environment-specific failure cases.
