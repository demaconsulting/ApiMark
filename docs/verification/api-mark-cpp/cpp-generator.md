## CppGenerator

### Verification Approach

`CppGenerator` is integration-tested in `test/ApiMark.Cpp.Tests/` using real C++ fixture
headers from `test/ApiMark.Cpp.Fixtures/include/` and the real `ClangAstParser`. Tests cover
configuration guards, ownership filtering, visibility filtering, deprecated filtering,
Doxygen rendering, output structure, operator grouping, inheritance, aliases, external-type
tracking, and single-file output.

### Test Environment

Tests require the fixture headers and a system clang installation accessible on PATH.
`InMemoryMarkdownWriterFactory` captures output without writing files.

### Acceptance Criteria

- All `CppGenerator` tests pass with zero failures.
- Ownership filtering includes only declarations under configured public roots and selected
  by `ApiHeaderPatterns` when patterns are present.
- Deprecated filtering occurs during parse-time namespace collection; visibility filtering is
  applied during emit-time member selection.
- Every visible non-operator member and regular free function receives a deterministic detail
  page unless case-insensitive collisions require a combined page.
- Class operators and namespace operators are grouped onto shared operator pages.
- Intra-library type references emit Markdown links in table cells; unknown non-std types are
  tracked for External Types sections.
- Type pages show fully qualified names, deleted notation, finality, and direct inheritance.
- Public type aliases, enums, nested classes, and single-file output are documented correctly.

### Test Scenarios

**Inheritance signature includes base class**: Verifies that a derived class page includes its
base class in the rendered class declaration line. This scenario is tested by
`CppGenerator_Generate_InheritanceClass_EmitsBaseClassInSignature`.

**Case-collision members share one page**: Verifies that members whose names differ only by case
are emitted on a single combined detail page. This scenario is tested by
`CppGenerator_Generate_CaseCollisionClass_CreatesCombinedPage` and
`CppGenerator_Generate_CaseCollisionClass_CombinedPageContainsBothMembers`.

**Namespace operators share one page**: Verifies that namespace-level operator overloads are
combined onto one operators page. This scenario is tested by
`CppGenerator_Generate_NamespaceFreeOperator_CreatesNamespaceOperatorsPage`.

**Class-scoped type aliases are documented**: Verifies that class-level aliases receive pages and
are listed from the owning class page. This scenario is tested by
`CppGenerator_Generate_ClassScopedTypeAlias_CreatesAliasPage` and
`CppGenerator_Generate_ClassScopedTypeAlias_ListedOnClassPage`.

**CWD-relative header patterns are supported**: Verifies that relative `ApiHeaderPatterns` are
resolved from the current working directory. This scenario is tested by
`CppGenerator_Generate_ApiHeaderPatterns_CwdRelativePattern_OnlyMatchingFilesDocumented` and
`CppGenerator_Generate_ApiHeaderPatterns_CwdRelativeExclusionPattern_ExcludesMatchingFiles`.

**Null context rejected by Parse**: Verifies that passing a null `IContext` to
`CppGenerator.Parse` throws `ArgumentNullException` before any file I/O is attempted.
Tested by `CppGenerator_Parse_NullContext_ThrowsArgumentNullException`.

**Ownership filtering limited to configured public roots**: Verifies that only declarations
whose source file falls under a configured `PublicIncludeRoot` appear in the generated
output. Tested by `CppGenerator_Generate_ValidHeaders_CreatesTypePageForSampleClass` and
`CppGenerator_Generate_ApiHeaderPatterns_TransitiveInclude_ExcludesNonSelectedSymbols`.

**Intra-library type references emit Markdown links**: Verifies that a known intra-library
type referenced in a member signature produces a Markdown hyperlink in the generated table
cell. Tested by `CppGenerator_Generate_TypeLinkInMemberSignature_EmitsMarkdownLink`.

**Deleted notation on type pages**: Verifies that a method declared `= delete` is annotated
as deleted on its generated page. Tested by
`CppGenerator_Generate_DeletedMember_ShowsDeletedNotation`.
