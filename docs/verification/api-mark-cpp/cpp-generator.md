## CppGenerator

### Verification Approach

`CppGenerator` is integration-tested in `test/ApiMark.Cpp.Tests/` using real C++ fixture headers
located in `test/ApiMark.Cpp.Fixtures/include/`. System clang is used via `ClangAstParser` as-is
because header parsing and declaration metadata interpretation are central to the unit's
responsibility. Tests are organized by behavioral area: error handling, ownership filtering,
visibility filtering, deprecated filtering, Doxygen doc comment rendering, output file structure,
enums, templates, inheritance, free functions, constructors, qualified names, and variadic
functions. An `InMemoryMarkdownWriterFactory` test double (from `ApiMark.Core.TestHelpers`) is
supplied to capture emitted content without writing to the file system.

### Test Environment

Tests require the fixture header files in `test/ApiMark.Cpp.Fixtures/include/` (located via
`[CallerFilePath]` resolution in `FixturePaths`) and a system clang installation accessible on
PATH (or via xcrun on macOS / vswhere on Windows). No external service, network dependency, or
privileged configuration is required beyond a standard clang installation.

### Acceptance Criteria

- All `CppGenerator` tests pass with zero failures.
- The ownership filter produces type pages only for declarations physically defined under a
  configured PublicIncludeRoot and whose source file was selected by the api-headers patterns
  (when patterns are configured); transitively-included dependency headers that are under a
  PublicIncludeRoot but not selected by --api-headers are excluded.
- Visibility filtering correctly excludes non-public members under Public mode, includes protected
  members under PublicAndProtected mode, and includes private members under All mode.
- Deprecated filtering correctly excludes declarations marked `[[deprecated]]` when IncludeDeprecated
  is false and includes them when it is true.
- Doxygen `@brief` comments appear as description paragraphs in generated output; missing doc
  comments produce the standard placeholder text.
- Every visible non-operator member — including parameterless methods, constructors, free
  functions, and variadic functions — receives its own dedicated detail page unless
  case-insensitive file-name collisions require combining members onto one page.
- Operator overloads declared inside a class are all grouped onto a single
  `{namespace}/{TypeName}/operators.md` page; the type page links to it via an Operators
  section rather than listing individual operator pages.
- Namespace-level operator free functions are all grouped onto a single
  `{namespace}/operators.md` page; the namespace summary page links to it via an Operators
  section.
- Generated file keys follow the naming convention: `api` entrypoint, `{namespace}` namespace
  summaries, `{namespace}/{TypeName}` type pages, `{namespace}/{TypeName}/{MemberName}` member
  pages, `{namespace}/{TypeName}/operators` class operator pages, and `{namespace}/operators`
  namespace operator pages.
- Type and member pages contain the fully qualified C++ name in their signature blocks.
- Enum pages list all declared enum values.
- Template class primary templates receive their own type pages.
- Null-input error paths throw the expected exception types immediately.

### Test Scenarios

**Constructor rejects null options**: Verifies that passing a null options object to the CppGenerator
constructor throws `ArgumentNullException` immediately, confirming that misconfigured callers fail
fast before any I/O is attempted. This scenario is tested by
`CppGenerator_Constructor_NullOptions_ThrowsArgumentNullException`.

**Generate rejects null factory**: Verifies that passing a null factory to Generate throws
`ArgumentNullException`, providing a clear error rather than an unrelated null-reference failure
during I/O. This scenario is tested by `CppGenerator_Generate_NullFactory_ThrowsArgumentNullException`.

**Generate throws for nonexistent include root**: Verifies that Generate throws
`DirectoryNotFoundException` when a configured PublicIncludeRoot path does not exist on disk,
providing a clear diagnostic instead of silently producing empty output. This scenario is tested by
`CppGenerator_Generate_NonexistentIncludeRoot_ThrowsDirectoryNotFoundException`.

**Valid headers create the api entrypoint**: Verifies that the generator creates the `api` key in
the writer factory when run against the fixture headers, confirming the top-level entrypoint
generation path is wired correctly. This scenario is tested by
`CppGenerator_Generate_ValidHeaders_CreatesApiEntrypoint`.

**Valid headers create a namespace summary page**: Verifies that a namespace summary page is created
for the fixture namespace, confirming that namespace discovery and page emission are correct. This
scenario is tested by `CppGenerator_Generate_ValidHeaders_CreatesNamespacePage`.

**Valid headers create a type page for SampleClass**: Verifies that SampleClass defined in the
fixture headers receives a type page at the expected key, confirming that the ownership filter
and type page emission path both operate correctly. This scenario is tested by
`CppGenerator_Generate_ValidHeaders_CreatesTypePageForSampleClass`.

**Deprecated class excluded by default**: Verifies that a class marked `[[deprecated]]` does not
receive a type page when IncludeDeprecated is false, confirming the default exclude behavior. This
scenario is tested by `CppGenerator_Generate_IncludeDeprecatedFalse_ExcludesDeprecatedClass`.

**Deprecated class included when requested**: Verifies that a class marked `[[deprecated]]` receives
a type page when IncludeDeprecated is explicitly true, confirming the opt-in include behavior. This
scenario is tested by `CppGenerator_Generate_IncludeDeprecatedTrue_IncludesDeprecatedClass`.

**Protected method excluded under Public visibility**: Verifies that a protected method does not
receive a member page when Visibility is Public, confirming that the access specifier filter
excludes non-public members correctly in the default mode. This scenario is tested by
`CppGenerator_Generate_PublicVisibility_ExcludesProtectedMethod`.

**Protected method included under PublicAndProtected visibility**: Verifies that a protected method
receives its own member page when Visibility is PublicAndProtected, confirming the broader mode
includes protected members. This scenario is tested by
`CppGenerator_Generate_PublicAndProtectedVisibility_IncludesProtectedMethod`.

**Private method included under All visibility**: Verifies that a private method receives its own
member page when Visibility is All, confirming the most permissive mode includes all access
specifiers. This scenario is tested by `CppGenerator_Generate_AllVisibility_IncludesPrivateMethod`.

**Method with parameters creates a member page**: Verifies that a method with parameters receives
its own dedicated member detail page, confirming that parameterized methods are handled by the
member emission path. This scenario is tested by
`CppGenerator_Generate_MethodWithParameters_CreatesMemberPage`.

**All members receive separate files**: Verifies that every visible member — parameterless methods,
methods with parameters, and free functions — receives a separate file, making navigation fully
deterministic. This scenario is tested by `CppGenerator_AllMembers_GetSeparateFiles`.

**Output files follow naming convention**: Verifies that the generated file keys follow the
established naming convention: `api` for the entrypoint, `{namespace}` for namespace summaries,
`{namespace}/{TypeName}` for type pages, and `{namespace}/{TypeName}/{MemberName}` for member
detail pages. This scenario is tested by `CppGenerator_OutputFiles_FollowNamingConvention`.

**api.md lists namespaces with declaration count**: Verifies that the api.md entrypoint lists all
namespaces with a Types column showing the count of classes, enums, and free functions directly
in each namespace, giving AI agents a complete navigation map and scope signal in one read. This
scenario is tested by `CppGenerator_Generate_ApiMd_ListsNamespacesWithTypeCount`.

**Type with doc comment writes summary to paragraph**: Verifies that a Doxygen `@brief` comment
on a class is rendered as a description paragraph in the type page, confirming that doc comment
extraction and rendering are wired correctly. This scenario is tested by
`CppGenerator_Generate_TypeWithDocComment_WritesSummaryToParagraph`.

**Method with doc comment writes summary to paragraph**: Verifies that a Doxygen `@brief` comment
on a method is rendered as a description paragraph in the member page, confirming that method-level
doc comments are extracted and rendered. This scenario is tested by
`CppGenerator_Generate_MethodWithDocComment_WritesSummaryToParagraph`.

**Missing doc comment writes placeholder**: Verifies that a member with no Doxygen doc comment
emits the standard no-description placeholder rather than an empty or absent description field,
consistent with the DotNetGenerator behavior for missing XML doc entries. This scenario is tested
by `CppGenerator_Generate_MissingDocComment_WritesPlaceholder`.

**Free functions receive their own pages**: Verifies that free functions in a namespace receive
dedicated pages at `{namespace}/{functionName}`, confirming that namespace-level functions are
treated as documented members with their own pages. This scenario is tested by
`CppGenerator_Generate_FreeFunctions_GetOwnPages`.

**Valid headers create an enum page**: Verifies that an enum declared in the fixture headers
receives a type page at the expected key, confirming that enum types are handled by the ownership
filter and type page emission path. This scenario is tested by
`CppGenerator_Generate_ValidHeaders_CreatesEnumPage`.

**Enum page contains all declared values**: Verifies that the enum type page includes all declared
enum value names in its output, confirming that enum member enumeration is complete. This scenario
is tested by `CppGenerator_Generate_EnumPage_ContainsValues`.

**Template class creates a type page**: Verifies that a primary class template receives its own
type page, confirming that template declarations are handled by the ownership filter and type page
emission path. This scenario is tested by `CppGenerator_Generate_TemplateClass_CreatesTypePage`.

**Inheritance class creates a type page**: Verifies that a class that inherits from another class
receives its own type page, confirming that derived types are documented independently of their
base class. This scenario is tested by `CppGenerator_Generate_InheritanceClass_CreatesTypePage`.

**Constructor creates a constructor detail page**: Verifies that an explicit constructor receives
its own member detail page, confirming that constructors are treated as documented members. This
scenario is tested by `CppGenerator_Generate_Constructor_CreatesConstructorPage`.

**Type page contains fully qualified C++ name**: Verifies that the type page signature block
contains the fully qualified C++ name so an AI reader knows exactly how to reference the type in
code without guessing the namespace prefix. This scenario is tested by
`CppGenerator_Generate_TypePage_ContainsQualifiedName`.

**Member page contains fully qualified C++ name**: Verifies that the member page signature block
contains the fully qualified C++ name so an AI reader can call the member without needing to
resolve the namespace separately. This scenario is tested by
`CppGenerator_Generate_MemberPage_ContainsQualifiedName`.

**Variadic function creates its own page**: Verifies that a variadic free function declared with
`...` receives its own dedicated page, confirming that variadic functions are handled correctly by
the free-function emission path. This scenario is tested by
`CppGenerator_Generate_VariadicFunction_CreatesPage`.

**Case-collision class creates a combined page**: Verifies that members whose names differ only by
case are merged into a single combined detail page on case-insensitive targets. This scenario is
tested by `CppGenerator_Generate_CaseCollisionClass_CreatesCombinedPage`.

**Case-collision class does not create a separate cased page**: Verifies that the generator does
not emit a second detail page for the same member name with different casing. This scenario is
tested by `CppGenerator_Generate_CaseCollisionClass_DoesNotCreateSeparateCasedPage`.

**Case-collision class combined page contains both members**: Verifies that the combined detail
page includes documentation for both case-colliding members. This scenario is tested by
`CppGenerator_Generate_CaseCollisionClass_CombinedPageContainsBothMembers`.

**Final class emits final keyword in signature**: Verifies that when a class is declared `final`,
its type page signature block contains the `final` keyword in the class declaration line so that
readers can immediately identify the class as non-subclassable without opening the header file.
This scenario is tested by `CppGenerator_Generate_FinalClass_EmitsFinalKeywordInSignature`.

**Non-final class does not emit final keyword**: Verifies that a class not declared `final` does
not have the `final` keyword anywhere in its type page signature block, confirming that the
annotation is only applied when explicitly declared. This scenario is tested by
`CppGenerator_Generate_NonFinalClass_DoesNotEmitFinalKeyword`.

**Class with operator overloads creates operators page**: Verifies that a class declaring
`operator+` and `operator==` produces a single shared `operators.md` page at
`{namespace}/{TypeName}/operators` rather than individual colliding pages. This prevents the
file-name collision that arises because multiple operator names sanitize to the same safe file
name. This scenario is tested by
`CppGenerator_Generate_ClassWithOperators_CreatesOperatorsPage`.

**Operators page contains operator entry**: Verifies that the operators page for a class with
overloads contains a heading for each declared operator, confirming all overloads are documented
on the combined page. This scenario is tested by
`CppGenerator_Generate_ClassWithOperators_OperatorsPageContainsOperatorEntry`.

**Type page links to operators page**: Verifies that the type page for a class with operator
overloads contains a link to `operators.md` in its Operators section, allowing readers to
navigate from the type overview to the operator detail page. This scenario is tested by
`CppGenerator_Generate_ClassWithOperators_TypePageLinksToOperatorsPage`.

**Namespace free operator creates namespace operators page**: Verifies that a namespace-level
operator free function (e.g. `operator<<`) produces a shared `operators.md` page at
`{namespace}/operators` rather than an individual page that would collide with other namespace
operators. This scenario is tested by
`CppGenerator_Generate_NamespaceFreeOperator_CreatesNamespaceOperatorsPage`.

**Transitive-include symbols from non-selected headers are excluded**: Verifies that when a
selected header transitively includes another header that is under a PublicIncludeRoot but was not
selected by `--api-headers`, symbols defined in the non-selected header are excluded from the
generated output. This confirms that ownership requires both root-membership and header selection,
preventing dependency types from appearing in the docs. This scenario is tested by
`CppGenerator_Generate_ApiHeaderPatterns_TransitiveInclude_ExcludesNonSelectedSymbols`.
