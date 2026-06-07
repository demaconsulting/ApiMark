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
- The generator discovers namespaces, types, free functions, and enums from the fixture headers.
- Doxygen `@brief` comments appear as description paragraphs in generated output.
- Visibility filtering (Public, PublicAndProtected, All) correctly includes and excludes class
  members based on their C++ access specifier.
- Deprecated filtering correctly excludes or includes declarations marked `[[deprecated]]`
  based on the IncludeDeprecated option.
- All visible members — including parameterless methods, constructors, and free functions —
  receive their own dedicated detail pages.
- Output files follow the naming convention: `api.md` entrypoint, `{namespace}.md` namespace
  summaries, `{namespace}/{TypeName}.md` type pages, and `{namespace}/{TypeName}/{MemberName}.md`
  member detail pages.

## Test Scenarios

**Valid headers create the api entrypoint file**: Verifies that the generator creates the top-level
`api.md` entrypoint file when run against real fixture headers, confirming the full generation path
from header parsing to file emission is wired correctly. This scenario is tested by
`CppGenerator_Generate_ValidHeaders_CreatesApiEntrypoint`.

**Valid headers create a namespace summary page**: Verifies that the generator creates a namespace
summary page for each namespace discovered in the fixture headers so all owned declarations are
reachable from the entrypoint. This scenario is tested by
`CppGenerator_Generate_ValidHeaders_CreatesNamespacePage`.

**Valid headers create a type page for SampleClass**: Verifies that a representative class defined
in the fixture headers receives its own type page, confirming the ownership filter and type page
generation path are correct. This scenario is tested by
`CppGenerator_Generate_ValidHeaders_CreatesTypePageForSampleClass`.

**Deprecated class is excluded when IncludeDeprecated is false**: Verifies that a class marked
`[[deprecated]]` does not receive a type page when the IncludeDeprecated option is false,
confirming the default exclude behavior. This scenario is tested by
`CppGenerator_Generate_IncludeDeprecatedFalse_ExcludesDeprecatedClass`.

**Deprecated class is included when IncludeDeprecated is true**: Verifies that a class marked
`[[deprecated]]` receives a type page when IncludeDeprecated is explicitly set to true,
confirming the opt-in include behavior. This scenario is tested by
`CppGenerator_Generate_IncludeDeprecatedTrue_IncludesDeprecatedClass`.

**Protected method is excluded under Public visibility**: Verifies that a protected method does
not receive its own member page when the visibility is set to Public, confirming the access
specifier filter is applied correctly for the public-only audience. This scenario is tested by
`CppGenerator_Generate_PublicVisibility_ExcludesProtectedMethod`.

**Protected method is included under PublicAndProtected visibility**: Verifies that a protected
method receives its own member page when visibility is PublicAndProtected, confirming that the
broader visibility mode includes the expected members. This scenario is tested by
`CppGenerator_Generate_PublicAndProtectedVisibility_IncludesProtectedMethod`.

**Private method is included under All visibility**: Verifies that a private method receives its
own member page when visibility is All, confirming that all access specifiers are included in the
most permissive mode. This scenario is tested by
`CppGenerator_Generate_AllVisibility_IncludesPrivateMethod`.

**Method with parameters creates a member page**: Verifies that a method with parameters receives
its own dedicated member detail page, confirming that parameterized members are handled by the
member page emission path. This scenario is tested by
`CppGenerator_Generate_MethodWithParameters_CreatesMemberPage`.

**All members receive separate files**: Verifies that every visible member — including
parameterless methods and free functions — is emitted as a separate file, making navigation fully
deterministic without requiring callers to know member shape. This scenario is tested by
`CppGenerator_AllMembers_GetSeparateFiles`.

**Output files follow naming convention**: Verifies that generated file keys follow the expected
naming convention: `api` entrypoint, `{namespace}` namespace summaries, `{namespace}/{TypeName}`
type pages, and `{namespace}/{TypeName}/{MemberName}` member pages. This scenario is tested by
`CppGenerator_OutputFiles_FollowNamingConvention`.

**Type with doc comment writes summary to paragraph**: Verifies that the Doxygen `@brief` comment
on a documented class is extracted and rendered as a description paragraph in the type page output.
This scenario is tested by `CppGenerator_Generate_TypeWithDocComment_WritesSummaryToParagraph`.

**Method with doc comment writes summary to paragraph**: Verifies that the Doxygen `@brief`
comment on a documented method is extracted and rendered as a description paragraph in the member
page output. This scenario is tested by
`CppGenerator_Generate_MethodWithDocComment_WritesSummaryToParagraph`.

**Missing doc comment writes placeholder text**: Verifies that a member with no Doxygen doc
comment emits the standard no-description placeholder rather than an empty or absent description
field. This scenario is tested by `CppGenerator_Generate_MissingDocComment_WritesPlaceholder`.

**Free functions receive their own pages**: Verifies that free functions in a namespace receive
dedicated pages at `{namespace}/{functionName}`, parallel to the member page convention for class
members. This scenario is tested by `CppGenerator_Generate_FreeFunctions_GetOwnPages`.

**Valid headers create an enum page**: Verifies that an enum declared in the fixture headers
receives its own type page following the same `{namespace}/{typeName}` convention as classes.
This scenario is tested by `CppGenerator_Generate_ValidHeaders_CreatesEnumPage`.

**Enum page contains all declared values**: Verifies that the enum type page includes all declared
enum value names so the complete enumeration is visible in the generated reference. This scenario
is tested by `CppGenerator_Generate_EnumPage_ContainsValues`.

**Template class creates a type page**: Verifies that a primary class template receives its own
type page, confirming that template declarations are handled by the ownership filter and type page
emission path. This scenario is tested by `CppGenerator_Generate_TemplateClass_CreatesTypePage`.

**Inheritance class creates a type page**: Verifies that a class that inherits from another class
receives its own type page, confirming that derived types are documented independently. This
scenario is tested by `CppGenerator_Generate_InheritanceClass_CreatesTypePage`.

**Constructor creates a constructor detail page**: Verifies that an explicit constructor receives
its own member detail page, confirming that constructors are treated as documented members. This
scenario is tested by `CppGenerator_Generate_Constructor_CreatesConstructorPage`.

**Type page contains fully qualified C++ name**: Verifies that the type page signature block
contains the fully qualified C++ name (e.g. `fixtures::SampleClass`) so an AI reader knows
exactly how to reference the type in code. This scenario is tested by
`CppGenerator_Generate_TypePage_ContainsQualifiedName`.

**Member page contains fully qualified C++ name**: Verifies that the member page signature block
contains the fully qualified C++ name (e.g. `fixtures::SampleClass::GetGreeting`) so an AI reader
can call the member without guessing the namespace. This scenario is tested by
`CppGenerator_Generate_MemberPage_ContainsQualifiedName`.

**Variadic function creates its own page**: Verifies that a variadic free function declared with
`...` receives its own dedicated page, confirming that variadic functions are handled correctly
by the free-function emission path. This scenario is tested by
`CppGenerator_Generate_VariadicFunction_CreatesPage`.

**Constructor throws when options are null**: Verifies that passing a null options object to the
CppGenerator constructor throws `ArgumentNullException` immediately, so misconfigured callers fail
fast before any I/O is attempted. This scenario is tested by
`CppGenerator_Constructor_NullOptions_ThrowsArgumentNullException`.

**Generate throws when factory is null**: Verifies that passing a null factory to Generate throws
`ArgumentNullException`, so callers that forget to supply a factory receive a clear error rather
than an unrelated null-reference failure during I/O. This scenario is tested by
`CppGenerator_Generate_NullFactory_ThrowsArgumentNullException`.

**Generate throws when include root does not exist**: Verifies that Generate throws
`DirectoryNotFoundException` when a configured PublicIncludeRoot path does not exist on disk,
providing a clear diagnostic rather than silently producing empty output. This scenario is tested
by `CppGenerator_Generate_NonexistentIncludeRoot_ThrowsDirectoryNotFoundException`.
