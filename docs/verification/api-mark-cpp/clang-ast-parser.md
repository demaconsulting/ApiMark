## ClangAstParser

### Verification Approach

`ClangAstParser` is integration-tested in `test/ApiMark.Cpp.Tests/ClangAstParserTests.cs`
using real C++ fixture headers located in `test/ApiMark.Cpp.Fixtures/include/`. The tests
invoke `ClangAstParser.Parse` directly with actual header file paths and a real
`CppGeneratorOptions` instance. All tests check for clang availability using
`IsClangAvailable()` and call `Assert.Skip` when clang is not on PATH, so the tests are
safe to run in environments without clang. No mock or stub is used for the clang executable
itself; the test verifies the real integration path.

### Test Environment

Tests require the fixture header files in `test/ApiMark.Cpp.Fixtures/include/` (located via
`[CallerFilePath]` resolution in `FixturePaths`) and a system clang installation accessible
on PATH. When clang is not available the tests are automatically skipped. No external network
access or privileged configuration is required.

### Acceptance Criteria

- Parsing the fixture headers returns a non-empty `Namespaces` collection.
- The returned namespaces include one whose `QualifiedName` contains `"fixtures"`.
- The `fixtures` namespace contains a class named `SampleClass`.
- `SampleClass` has at least one member.

### Test Scenarios

**Parse fixture headers returns non-empty namespaces**: Verifies that
`ClangAstParser.Parse` invoked with the fixture header files and a valid
`CppGeneratorOptions` returns a `CppCompilationResult` whose `Namespaces`
collection is non-empty, confirming that clang was invoked successfully and the
JSON AST was parsed and filtered correctly.
This scenario is tested by `ClangAstParser_Parse_FixtureHeaders_ReturnsNonEmptyNamespaces`.

**Parse fixture headers contains the fixtures namespace**: Verifies that the
returned namespaces include one whose `QualifiedName` contains `"fixtures"`,
confirming that the fixture headers' namespace declarations are correctly
deserialized from the clang JSON AST.
This scenario is tested by `ClangAstParser_Parse_FixtureHeaders_ContainsFixturesNamespace`.

**Fixtures namespace contains SampleClass**: Verifies that the `fixtures` namespace
returned by `Parse` contains a class named `SampleClass`, confirming that class
declarations inside the fixture namespace are correctly collected into the result.
This scenario is tested by
`ClangAstParser_Parse_FixtureHeaders_FixturesNamespaceContainsSampleClass`.

**SampleClass has members**: Verifies that the `SampleClass` class in the
`fixtures` namespace has at least one member, confirming that the member-collection
logic within the JSON walker operates correctly on the fixture header.
This scenario is tested by `ClangAstParser_Parse_FixtureHeaders_SampleClassHasMembers`.
