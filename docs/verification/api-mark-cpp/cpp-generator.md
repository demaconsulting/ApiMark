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
- Gitignore-style `ApiHeaderPatterns` (include, exclude, re-include, last-match-wins) are
  applied correctly to restrict the documented header set.
- Doxygen `@code`/`@endcode` blocks are rendered as fenced `cpp` code blocks on both
  gradual-disclosure member pages and single-file output.
- `api.md` lists all namespaces with a declaration count column for AI navigation scope.

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
cell. Tested by `CppGenerator_Generate_IntraLibraryReturnType_EmitsMarkdownLinkInReturnsCell`.

**Deleted notation on type pages**: Verifies that a method declared `= delete` is annotated
as deleted on its generated page. Tested by
`CppGenerator_Generate_DeletedCopyConstructor_EmitsDeleteSuffix` and
`CppGenerator_Generate_DeletedCopyAssignmentOperator_EmitsDeleteSuffix`.

**Class operator overloads produce a shared operators page**: Verifies that all operator
overloads for a class are grouped onto a single `operators.md` page rather than individual
colliding pages. Tested by `CppGenerator_Generate_ClassWithOperators_CreatesOperatorsPage`.

**Operators page contains each operator entry**: Verifies that every declared operator
overload appears as a heading on the shared operators page so readers can locate a specific
overload. Tested by `CppGenerator_Generate_ClassWithOperators_OperatorsPageContainsOperatorEntry`.

**Type page links to operators page**: Verifies that the owning class's type page contains a
table cell linking to the shared `operators.md` page so readers can navigate to it. Tested by
`CppGenerator_Generate_ClassWithOperators_TypePageLinksToOperatorsPage`.

**Final class emits final keyword in signature**: Verifies that the type page for a `final`
class includes the `final` keyword in its signature block. Tested by
`CppGenerator_Generate_FinalClass_EmitsFinalKeywordInSignature`.

**api.md lists namespaces with declaration count**: Verifies that `api.md` contains a namespace
table where each row includes the namespace name and a declarations count column. Tested by
`CppGenerator_Generate_ApiMd_ListsNamespacesWithTypeCount`.

**No ApiHeaderPatterns documents all headers**: Verifies that when `ApiHeaderPatterns` is
empty, all recognized header files under the include roots are documented. Tested by
`CppGenerator_Generate_NoApiHeaderPatterns_DocumentsAllHeaders`.

**Include pattern restricts to matching files**: Verifies that a positive `ApiHeaderPatterns`
entry restricts documentation to headers matching that pattern. Tested by
`CppGenerator_Generate_ApiHeaderPatterns_IncludePattern_OnlyMatchingFilesDocumented`.

**Exclude pattern removes matching files**: Verifies that a `!`-prefixed exclusion pattern
removes the named header from the documented set while leaving other headers present. Tested by
`CppGenerator_Generate_ApiHeaderPatterns_ExcludePattern_ExcludesMatchingFiles`.

**Re-include after exclude uses last-match-wins semantics**: Verifies that a header first
excluded and then re-included by a later positive pattern is documented, confirming gitignore-style
last-match-wins semantics. Tested by
`CppGenerator_Generate_ApiHeaderPatterns_ReInclude_GitignoreSemantics_IncludesReIncludedHeader`.

**Exclude without re-include permanently excludes header**: Verifies that an exclusion pattern
without a subsequent re-include permanently removes the header from the documented set. Tested by
`CppGenerator_Generate_ApiHeaderPatterns_ExcludeWithoutReInclude_ExcludesHeader`.

**Single-file output writes one api.md**: Verifies that selecting `OutputFormat.SingleFile`
produces exactly one writer keyed `api` containing all namespace and type documentation in a
flat heading hierarchy. Tested by `CppGenerator_Generate_SingleFileOutput_WritesSingleApiMarkdown`.

**Code example block on member page**: Verifies that a Doxygen `@code`/`@endcode` block on a
method produces a fenced `cpp` code block on the gradual-disclosure member page. Tested by
`CppGenerator_Generate_MethodWithCodeExample_EmitsCodeBlockOnMemberPage`.

**Code example block in single-file output**: Verifies that a Doxygen `@code`/`@endcode` block
on a method produces a fenced `cpp` code block in single-file output. Tested by
`CppGenerator_SingleFile_MethodWithCodeExample_EmitsCodeBlock`.

**Intra-library return type emits Markdown link in table cell**: Verifies that a method whose
return type is a known intra-library type produces a Markdown hyperlink in the Returns column of
the Methods table. Tested by
`CppGenerator_Generate_IntraLibraryReturnType_EmitsMarkdownLinkInReturnsCell`.

**Case-collision members do not create separate cased page**: Verifies that when a
case-insensitive collision exists, no separate page is created for the upper-case member name —
only the combined lowercase page exists. Tested by
`CppGenerator_Generate_CaseCollisionClass_DoesNotCreateSeparateCasedPage`.
