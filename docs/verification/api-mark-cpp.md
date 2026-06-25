# ApiMarkCpp

## Verification Approach

ApiMarkCpp is verified with integration-style tests in `test/ApiMark.Cpp.Tests/` that exercise the
full C++ generation pipeline using real C++ fixture headers with Doxygen doc comments located in
`test/ApiMark.Cpp.Fixtures/include/`. System clang is used as-is so
verification proves the interaction between header parsing, declaration ownership filtering,
visibility filtering, Doxygen comment rendering, and file emission. No internal production component
is mocked or stubbed; fixture header files are located in the source tree via `[CallerFilePath]`
resolution in `FixturePaths`.

## Test Environment

Tests require a .NET runtime capable of running the `ApiMark.Cpp` library, a system clang
installation accessible on PATH (or via xcrun on macOS / vswhere on Windows), and fixture header
files that are available in the source tree. No additional network dependency or machine-specific
configuration beyond a standard clang installation is required.

## Acceptance Criteria

- All ApiMarkCpp tests pass with zero failures.
- The generator discovers namespaces, types, free functions, enums, and type aliases from the fixture headers.
- Doxygen `@brief` comments appear as description paragraphs in generated output.
- Visibility filtering (Public, PublicAndProtected, All) correctly includes and excludes class
  members based on their C++ access specifier.
- Deprecated filtering correctly excludes or includes declarations marked `[[deprecated]]`
  based on the IncludeDeprecated option.
- All visible members — including parameterless methods, constructors, and free functions —
  receive their own dedicated detail pages, except where case-insensitive filename collisions
  require combining members onto one shared page.
- Explicitly deleted functions and operators are documented with a `= delete` suffix in their
  signatures.
- Type pages for derived classes include the direct base class name in the class signature block.
- `using` type aliases receive their own pages at `{namespace}/{aliasName}.md` and are listed
  in the namespace summary under a "Type Aliases" section.
- Output files follow the naming convention: `api.md` entrypoint, `{namespace}.md` namespace
  summaries, `{namespace}/{TypeName}.md` type pages, `{namespace}/{AliasName}.md` type alias
  pages, `{namespace}/{TypeName}/{MemberName}.md` member detail pages,
  `{namespace}/{TypeName}/{NestedType}.md` nested-type pages,
  `{namespace}/{TypeName}/{AliasName}.md` class-scoped type alias pages,
  `{namespace}/{TypeName}/operators.md` class operator overloads pages, and
  `{namespace}/operators.md` namespace-level operator overloads pages.
- When the single-file format is specified, all documentation is written to a single `api.md`
  file using a flat H1/H2/H3/H4 heading hierarchy.
- `api.md` lists all namespaces in a table with a Declarations count column so AI agents can
  calibrate exploration depth for each namespace.
- Doxygen `@code`/`@endcode` blocks are rendered as fenced `cpp` code blocks on both
  gradual-disclosure member pages and single-file output.
- When output contains references to external (non-library, non-std) types, the generated
  documentation includes an External Types section listing those types alphabetically.
- All operator overloads for a class are grouped onto a single `operators.md` page at
  `{namespace}/{TypeName}/operators.md`, and the owning type page includes a link to that page.
- Namespace-level free-function operator overloads are grouped onto a single `operators.md`
  page at `{namespace}/operators.md`.
- Type strings in method and member table cells that match a documented intra-library type are
  rendered as Markdown hyperlinks to the corresponding type page.
- Absolute `ApiHeaderPatterns` entries select headers by their full path without WorkingDirectory
  resolution.

## Test Scenarios

**Valid headers create the api entrypoint file**: Verifies that the generator creates the top-level
`api.md` entrypoint file when run against real fixture headers, confirming the full generation path
from header parsing to file emission is wired correctly. Tested by
`CppGenerator_Generate_ValidHeaders_CreatesApiEntrypoint`.

**Valid headers create a namespace summary page**: Verifies that the generator creates a namespace
summary page for each namespace discovered in the fixture headers so all owned declarations are
reachable from the entrypoint. Tested by
`CppGenerator_Generate_ValidHeaders_CreatesNamespacePage`.

**Valid headers create a type page for SampleClass**: Verifies that a representative class defined
in the fixture headers receives its own type page, confirming the ownership filter and type page
generation path are correct. Tested by
`CppGenerator_Generate_ValidHeaders_CreatesTypePageForSampleClass`.

**Inheritance class includes base type in signature**: Verifies that a class with a public base class
has the base class name included in the generated type page class signature. Tested by
`CppGenerator_Generate_InheritanceClass_EmitsBaseClassInSignature`.

**Deprecated class is excluded when IncludeDeprecated is false**: Verifies that a class marked
`[[deprecated]]` does not receive a type page when the IncludeDeprecated option is false,
confirming the default exclude behavior. Tested by
`CppGenerator_Generate_IncludeDeprecatedFalse_ExcludesDeprecatedClass`.

**Deprecated class is included when IncludeDeprecated is true**: Verifies that a class marked
`[[deprecated]]` receives a type page when IncludeDeprecated is explicitly set to true,
confirming the opt-in include behavior. Tested by
`CppGenerator_Generate_IncludeDeprecatedTrue_IncludesDeprecatedClass`.

**Protected method is excluded under Public visibility**: Verifies that a protected method does
not receive its own member page when the visibility is set to Public, confirming the access
specifier filter is applied correctly for the public-only audience. Tested by
`CppGenerator_Generate_PublicVisibility_ExcludesProtectedMethod`.

**Protected method is included under PublicAndProtected visibility**: Verifies that a protected
method receives its own member page when visibility is PublicAndProtected, confirming that the
broader visibility mode includes the expected members. Tested by
`CppGenerator_Generate_PublicAndProtectedVisibility_IncludesProtectedMethod`.

**Private method is included under All visibility**: Verifies that a private method receives its
own member page when visibility is All, confirming that all access specifiers are included in the
most permissive mode. Tested by
`CppGenerator_Generate_AllVisibility_IncludesPrivateMethod`.

**Method with parameters creates a member page**: Verifies that a method with parameters receives
its own dedicated member detail page, confirming that parameterized members are handled by the
member page emission path. Tested by
`CppGenerator_Generate_MethodWithParameters_CreatesMemberPage`.

**All members receive separate files**: Verifies that every visible member — including
parameterless methods and free functions — is emitted as a separate file, making navigation fully
deterministic without requiring callers to know member shape. Tested by
`CppGenerator_AllMembers_GetSeparateFiles`.

**Output files follow naming convention**: Verifies that generated file keys follow the expected
naming convention: `api` entrypoint, `{namespace}` namespace summaries, `{namespace}/{TypeName}`
type pages, and `{namespace}/{TypeName}/{MemberName}` member pages. Tested by
`CppGenerator_OutputFiles_FollowNamingConvention`.

**Type with doc comment writes summary to paragraph**: Verifies that the Doxygen `@brief` comment
on a documented class is extracted and rendered as a description paragraph in the type page output.
Tested by `CppGenerator_Generate_TypeWithDocComment_WritesSummaryToParagraph`.

**Method with doc comment writes summary to paragraph**: Verifies that the Doxygen `@brief`
comment on a documented method is extracted and rendered as a description paragraph in the member
page output. Tested by
`CppGenerator_Generate_MethodWithDocComment_WritesSummaryToParagraph`.

**Missing doc comment writes placeholder text**: Verifies that a member with no Doxygen doc
comment emits the standard no-description placeholder rather than an empty or absent description
field. Tested by `CppGenerator_Generate_MissingDocComment_WritesPlaceholder`.

**Free functions receive their own pages**: Verifies that free functions in a namespace receive
dedicated pages at `{namespace}/{functionName}`, parallel to the member page convention for class
members. Tested by `CppGenerator_Generate_FreeFunctions_GetOwnPages`.

**Valid headers create an enum page**: Verifies that an enum declared in the fixture headers
receives its own type page following the same `{namespace}/{typeName}` convention as classes.
Tested by `CppGenerator_Generate_ValidHeaders_CreatesEnumPage`.

**Enum page contains all declared values**: Verifies that the enum type page includes all declared
enum value names so the complete enumeration is visible in the generated reference. Tested by `CppGenerator_Generate_EnumPage_ContainsValues`.

**Template class creates a type page**: Verifies that a primary class template receives its own
type page, confirming that template declarations are handled by the ownership filter and type page
emission path. Tested by `CppGenerator_Generate_TemplateClass_CreatesTypePage`.

**Inheritance class creates a type page**: Verifies that a class that inherits from another class
receives its own type page, confirming that derived types are documented independently. Tested by `CppGenerator_Generate_InheritanceClass_CreatesTypePage`.

**Constructor creates a constructor detail page**: Verifies that an explicit constructor receives
its own member detail page, confirming that constructors are treated as documented members. Tested by `CppGenerator_Generate_Constructor_CreatesConstructorPage`.

**Type page contains fully qualified C++ name**: Verifies that the type page signature block
contains the fully qualified C++ name (e.g. `fixtures::SampleClass`) so an AI reader knows
exactly how to reference the type in code. Tested by
`CppGenerator_Generate_TypePage_ContainsQualifiedName`.

**Member page contains fully qualified C++ name**: Verifies that the member page signature block
contains the fully qualified C++ name (e.g. `fixtures::SampleClass::GetGreeting`) so an AI reader
can call the member without guessing the namespace. Tested by
`CppGenerator_Generate_MemberPage_ContainsQualifiedName`.

**Variadic function creates its own page**: Verifies that a variadic free function declared with
`...` receives its own dedicated page, confirming that variadic functions are handled correctly
by the free-function emission path. Tested by
`CppGenerator_Generate_VariadicFunction_CreatesPage`.

**Constructor throws when options are null**: Verifies that passing a null options object to the
CppGenerator constructor throws `ArgumentNullException` immediately, so misconfigured callers fail
fast before any I/O is attempted. Tested by
`CppGenerator_Constructor_NullOptions_ThrowsArgumentNullException`.

**Generate throws when factory is null**: Verifies that passing a null factory to Generate throws
`ArgumentNullException`, so callers that forget to supply a factory receive a clear error rather
than an unrelated null-reference failure during I/O. Tested by
`CppGenerator_Generate_NullFactory_ThrowsArgumentNullException`.

**Generate throws when include root does not exist**: Verifies that Generate throws
`DirectoryNotFoundException` when a configured PublicIncludeRoot path does not exist on disk,
providing a clear diagnostic rather than silently producing empty output. Tested by `CppGenerator_Generate_NonexistentIncludeRoot_ThrowsDirectoryNotFoundException`.

**Deleted copy constructor signature contains = delete suffix**: Verifies that a copy constructor
declared with `= delete` is documented with a `= delete` suffix in its signature block so that
readers can see the intentional prohibition without opening the header file. Tested by `CppGenerator_Generate_DeletedCopyConstructor_EmitsDeleteSuffix`.

**Deleted copy-assignment operator signature contains = delete suffix**: Verifies that a
copy-assignment operator declared with `= delete` is documented with a `= delete` suffix in its
signature so that the prohibition is visible on the combined operators page. Tested by `CppGenerator_Generate_DeletedCopyAssignmentOperator_EmitsDeleteSuffix`.

**Type aliases receive their own pages**: Verifies that `using` type alias declarations in
documented namespaces produce individual pages at `{namespace}/{aliasName}`, following the same
convention as class and enum pages. Tested by
`CppGenerator_Generate_TypeAlias_CreatesAliasPages`.

**Type alias page contains declaration and summary**: Verifies that the type alias page contains
the `using {name} = {underlying}` declaration in a fenced code block and the Doxygen `@brief`
summary as a description paragraph. Tested by
`CppGenerator_Generate_TypeAliasPage_ContainsDeclarationAndSummary`.

**Namespace page lists type aliases**: Verifies that the namespace summary page includes a
"Type Aliases" section that lists every owned alias so readers can discover them without opening
individual alias pages. Tested by
`CppGenerator_Generate_NamespacePage_ListsTypeAliases`.

**Single-file format writes all namespaces to one api.md file**: Verifies that when the
`--format single-file` option is specified, all documentation is written to a single `api.md`
file using a flat heading hierarchy rather than producing separate namespace and type pages. Tested by `CppGenerator_Generate_SingleFileFormat_WritesToSingleFile`.

**api.md lists all namespaces with type count**: Verifies that `api.md` contains a namespace
table where every documented namespace appears with a Declarations count column so that AI agents
have a complete navigation map in a single read. Tested by
`CppGenerator_Generate_ApiMd_ListsNamespacesWithTypeCount`.

**Class operator overloads grouped on single operators page**: Verifies that all operator
overloads for a class are combined onto a single `operators.md` page at
`{namespace}/{TypeName}/operators` and that the owning type page links to it. Tested by `CppGenerator_Generate_ClassWithOperators_CreatesOperatorsPage`,
`CppGenerator_Generate_ClassWithOperators_OperatorsPageContainsOperatorEntry`, and
`CppGenerator_Generate_ClassWithOperators_TypePageLinksToOperatorsPage`.

**Namespace-level free-function operators grouped on single operators page**: Verifies that
namespace-level free-function operator overloads are grouped onto a single `operators.md` page
at `{namespace}/operators` rather than producing individual pages. Tested by
`CppGenerator_Generate_NamespaceFreeOperator_CreatesNamespaceOperatorsPage`.

**Intra-library return type emits Markdown link in table cell**: Verifies that a method whose
return type is a documented intra-library type produces a Markdown hyperlink in the Returns
column of the Methods table. Tested by
`CppGenerator_Generate_IntraLibraryReturnType_EmitsMarkdownLinkInReturnsCell`.

**Gitignore-style ApiHeaderPatterns restrict documented headers**: Verifies that the generator
correctly applies include, exclude, and re-include patterns to restrict the documented API surface.
Tested by `CppGenerator_Generate_NoApiHeaderPatterns_DocumentsAllHeaders`,
`CppGenerator_Generate_ApiHeaderPatterns_IncludePattern_OnlyMatchingFilesDocumented`,
`CppGenerator_Generate_ApiHeaderPatterns_ExcludePattern_ExcludesMatchingFiles`,
`CppGenerator_Generate_ApiHeaderPatterns_ReInclude_GitignoreSemantics_IncludesReIncludedHeader`, and
`CppGenerator_Generate_ApiHeaderPatterns_ExcludeWithoutReInclude_ExcludesHeader`.

**Absolute header patterns select headers by full path**: Verifies that an `ApiHeaderPatterns`
entry containing an absolute path selects the specified header for documentation without
WorkingDirectory resolution, allowing headers outside the project tree to be included. Tested by
`CppGenerator_Generate_ApiHeaderPatterns_AbsolutePattern_DocumentsMatchingFile`.

**Code example blocks rendered as fenced code**: Verifies that Doxygen `@code`/`@endcode` blocks
on documented methods are rendered as fenced `cpp` code blocks on both gradual-disclosure member
pages and single-file output. Tested by
`CppGenerator_Generate_MethodWithCodeExample_EmitsCodeBlockOnMemberPage` and
`CppGenerator_SingleFile_MethodWithCodeExample_EmitsCodeBlock`.

**External types section in generated output**: Verifies that when a documented type references an
external (non-std, non-library) type, the generated page includes an `External Types` section with
the external type listed. Tested by
`CppGenerator_Generate_ExternalTypeReference_EmitsExternalTypesSection`.
