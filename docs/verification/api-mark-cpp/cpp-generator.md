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
- When `ApiHeaderPatterns` is empty, all headers under configured PublicIncludeRoots with
  recognized C++ extensions are documented. When patterns are configured, only headers whose last
  matching pattern is a positive (non-`!`) pattern are included; gitignore last-match-wins
  semantics apply.
- Methods returning types documented within the same library emit Markdown links in the Returns
  column of the Methods table; methods returning types not found in the library emit plain text.
- Types referenced in member signatures that are not documented within the library are tracked
  and listed in the External Types section of the library entrypoint.

### Test Scenarios

**Constructor rejects null options**: Verifies that passing a null options object to the CppGenerator
constructor throws `ArgumentNullException` immediately, confirming that misconfigured callers fail
fast before any I/O is attempted. This scenario is tested by
`CppGenerator_Constructor_NullOptions_ThrowsArgumentNullException`.

**Generate rejects null factory**: Verifies that passing a null factory to `CppEmitter.Emit`
(obtained from `CppGenerator.Parse`) throws `ArgumentNullException`, providing a clear error rather
than an unrelated null-reference failure during I/O. This scenario is tested by
`CppGenerator_Generate_NullFactory_ThrowsArgumentNullException`.

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

**No api-header patterns documents all headers**: Verifies that when `ApiHeaderPatterns` is empty,
all headers with recognized C++ extensions under the configured PublicIncludeRoots are documented
without any pattern filtering. This scenario is tested by
`CppGenerator_Generate_NoApiHeaderPatterns_DocumentsAllHeaders`.

**Include pattern restricts documented headers**: Verifies that a specific include pattern (e.g.
`**/SampleClass.h`) restricts header enumeration so only files matching the pattern are documented;
headers not matching the pattern are excluded. This scenario is tested by
`CppGenerator_Generate_ApiHeaderPatterns_IncludePattern_OnlyMatchingFilesDocumented`.

**Exclusion pattern excludes matching headers**: Verifies that a `!`-prefixed exclusion pattern
excludes matching headers while headers not matching the exclusion pattern are still documented.
This scenario is tested by
`CppGenerator_Generate_ApiHeaderPatterns_ExcludePattern_ExcludesMatchingFiles`.

**Re-include pattern overrides earlier exclusion (gitignore semantics)**: Verifies that a header
excluded by a `!`-prefixed pattern is re-included when a subsequent positive pattern matches it.
Last-pattern-wins (gitignore) semantics are confirmed by this scenario, which is tested by
`CppGenerator_Generate_ApiHeaderPatterns_ReInclude_GitignoreSemantics_IncludesReIncludedHeader`.

**Exclusion without re-include permanently excludes header**: Verifies that a header excluded by a
`!`-prefixed pattern with no subsequent positive pattern remains excluded from the generated
output, confirming that the last matching pattern wins. This scenario is tested by
`CppGenerator_Generate_ApiHeaderPatterns_ExcludeWithoutReInclude_ExcludesHeader`.

**Intra-library return type emits a Markdown link in the Returns cell**: Verifies that when a
method returns a type that is itself documented within the same library, the Returns column in the
Methods table contains a Markdown link to that type's page rather than plain text. This gives AI
readers navigable links for cross-type traversal. This scenario is tested by
`CppGenerator_Generate_IntraLibraryReturnType_EmitsMarkdownLinkInReturnsCell`.

**Unknown namespaced type is tracked as external**: Verifies that when `CppTypeLinkResolver`
resolves a type whose namespace is not in the known-types dictionary, the original type string is
returned as plain text (no broken link) and the type is recorded in the external types tracking
set with its namespace and type name. This prevents broken links and enables the External Types
section to enumerate all referenced-but-undocumented types. This scenario is tested by
`CppTypeLinkResolver_Linkify_UnknownNamespacedType_TracksExternalType`.

**Single-file output writes a complete api.md tree**: Verifies that when `OutputFormat.SingleFile`
is configured, the generator produces exactly one writer keyed `api`, containing an H1 library
name heading, H2 namespace heading, H3 class heading (e.g., `SampleClass`), H4 member headings
with parentheses, no group headings (`Constructors`, `Methods`), and at least one compact
bullet-list paragraph (`- **MemberName**: description`) summarizing a class's members.
This scenario is tested by
`CppGenerator_Generate_SingleFileOutput_WritesSingleApiMarkdown`.

**Deleted copy constructor emits = delete suffix**: Verifies that a copy constructor declared
`= delete` in the header is documented with a `= delete` suffix in its generated signature,
making the intentional prohibition visible to readers without requiring them to open the header.
This scenario is tested by `CppGenerator_Generate_DeletedCopyConstructor_EmitsDeleteSuffix`.

**Deleted copy assignment operator emits = delete suffix**: Verifies that a copy-assignment
operator declared `= delete` is documented with a `= delete` suffix in its signature,
confirming that deleted operators carry the prohibition annotation.
This scenario is tested by
`CppGenerator_Generate_DeletedCopyAssignmentOperator_EmitsDeleteSuffix`.

**Type alias creates alias pages**: Verifies that `using` type aliases declared in documented
namespaces receive their own dedicated pages, confirming that aliases are treated as first-class
documented entities.
This scenario is tested by `CppGenerator_Generate_TypeAlias_CreatesAliasPages`.

**Type alias page contains declaration and summary**: Verifies that the alias page contains both
the `using` declaration and the Doxygen summary comment, providing both the type information and
the documentation.
This scenario is tested by `CppGenerator_Generate_TypeAliasPage_ContainsDeclarationAndSummary`.

**Namespace page lists type aliases**: Verifies that the namespace summary page lists owned type
aliases so readers can discover them without visiting each alias page individually.
This scenario is tested by `CppGenerator_Generate_NamespacePage_ListsTypeAliases`.

**Type alias page simplifies underlying type**: Verifies that verbose underlying type names are
simplified in the alias page declaration, consistent with how other type names are simplified
throughout the generated documentation.
This scenario is tested by `CppGenerator_Generate_TypeAliasPage_SimplifiesUnderlyingType`.

**Default parameter signature contains default value**: Verifies that a function parameter with
a default value has the default displayed in the generated signature block.
This scenario is tested by `CppGenerator_Generate_DefaultParameter_SignatureContainsDefault`.

**Bool default parameter signature contains false**: Verifies that a `bool` parameter defaulted
to `false` shows `false` in the generated signature.
This scenario is tested by `CppGenerator_Generate_BoolDefaultParameter_SignatureContainsFalse`.

**Negative int default parameter signature contains negative value**: Verifies that an integer
parameter defaulted to a negative value shows the negative value in the generated signature.
This scenario is tested by
`CppGenerator_Generate_NegativeIntDefaultParameter_SignatureContainsNegativeValue`.

**Float default parameter signature contains value**: Verifies that a float parameter with a
default value shows the value in the generated signature.
This scenario is tested by `CppGenerator_Generate_FloatDefaultParameter_SignatureContainsValue`.

**Nested class creates a nested class page**: Verifies that a class declared inside another class
receives its own type page, confirming that nested type declarations are documented independently.
This scenario is tested by `CppGenerator_Generate_NestedClass_CreatesNestedClassPage`.

**Nested class is listed on outer class page**: Verifies that the outer class type page lists
its nested class in a Nested Types section, providing navigation from the containing type to
its nested types.
This scenario is tested by `CppGenerator_Generate_NestedClass_ListedOnOuterClassPage`.

**Class-scoped type alias creates alias page**: Verifies that a `using` type alias declared
inside a class receives its own page, confirming that class-scoped aliases are treated as
first-class documented entities.
This scenario is tested by `CppGenerator_Generate_ClassScopedTypeAlias_CreatesAliasPage`.

**Class-scoped type alias listed on class page**: Verifies that the class type page lists its
scoped type aliases, providing navigation from the type page to alias pages.
This scenario is tested by `CppGenerator_Generate_ClassScopedTypeAlias_ListedOnClassPage`.

**Class-scoped type alias does not collide across classes**: Verifies that same-named type
aliases in different classes do not collide in the generated output, confirming that
namespace-and-class-scoped paths are used for alias page keys.
This scenario is tested by
`CppGenerator_Generate_ClassScopedTypeAlias_DoesNotCollideAcrossClasses`.

**Method with code example emits code block on member page**: Verifies that a method whose
Doxygen comment includes a `@code`/`@endcode` block produces a fenced code block on its
member detail page.
This scenario is tested by `CppGenerator_Generate_MethodWithCodeExample_EmitsCodeBlockOnMemberPage`.

**Single-file method with code example emits code block**: Verifies that in single-file output
mode, a method with a Doxygen code example produces a fenced code block in the single api.md file.
This scenario is tested by `CppGenerator_SingleFile_MethodWithCodeExample_EmitsCodeBlock`.

### Unit Test Scenarios

**CppSourceLocation stores file and line correctly**: Verifies that `CppSourceLocation` records the
`File` and `Line` values passed at construction, confirming that source location information is
preserved for use in `#include` directives and diagnostic messages on generated pages. This
scenario is tested by `CppSourceLocation_Construction_SetsFileAndLine`.

**CppParamDoc stores name and description correctly**: Verifies that `CppParamDoc` records the
parameter name and description passed at construction, confirming that per-parameter documentation
extracted from Doxygen `@param` blocks is preserved for rendering in parameter tables. This
scenario is tested by `CppParamDoc_Construction_SetsNameAndDescription`.

**CppDocComment stores summary and details correctly**: Verifies that `CppDocComment` records the
summary and details strings passed at construction, confirming that `@brief` and `@details` content
is preserved for rendering as description and extended-details paragraphs. This scenario is tested
by `CppDocComment_Construction_SetsSummaryAndDetails`.

**Two identical CppDocComment instances are equal**: Verifies that record equality holds for
`CppDocComment`, confirming that the record semantics do not require reference identity for
documentation comparison or deduplication logic. This scenario is tested by
`CppDocComment_Equality_TwoIdenticalInstances_AreEqual`.

**CppBaseType stores name correctly**: Verifies that `CppBaseType` records the base class name
passed at construction, confirming that inheritance information is preserved for rendering in type
signature blocks. This scenario is tested by `CppBaseType_Construction_SetsName`.

**CppTemplateParam stores name correctly**: Verifies that `CppTemplateParam` records the template
parameter name passed at construction, confirming that template parameter information is preserved
for rendering in type signature blocks. This scenario is tested by
`CppTemplateParam_Construction_SetsName`.

**CppEnumValue stores name and documentation correctly**: Verifies that `CppEnumValue` records the
name and doc comment passed at construction, confirming that enum value names and their
documentation are preserved for rendering in enum value tables. This scenario is tested by
`CppEnumValue_Construction_SetsNameAndDoc`.

**CppParameter stores name and type name correctly**: Verifies that `CppParameter` records the
parameter name and type name passed at construction, confirming that function parameter information
is preserved for rendering in parameter signature blocks. This scenario is tested by
`CppParameter_Construction_SetsNameAndTypeName`.

**CppParameter default value is null when not provided**: Verifies that `CppParameter.DefaultValue`
is null when no default value is passed at construction, confirming that the default-value field is
optional and correctly absent for parameters without defaults. This scenario is tested by
`CppParameter_DefaultValue_WhenNotProvided_IsNull`.

**CppField stores core properties correctly**: Verifies that `CppField` records the name, type name,
accessibility, and static flag passed at construction, confirming that field metadata is preserved
for use in visibility filtering and field signature rendering. This scenario is tested by
`CppField_Construction_SetsCoreProperties`.

**CppFunction stores core properties correctly**: Verifies that `CppFunction` records the name,
return type, accessibility, and constructor flag passed at construction, confirming that function
metadata is preserved for use in visibility filtering, constructor detection, and signature
rendering. This scenario is tested by `CppFunction_Construction_SetsCoreProperties`.

**CppClass stores core properties correctly**: Verifies that `CppClass` records the name, base
types, and final flag passed at construction, confirming that class metadata is preserved for use
in ownership filtering, inheritance rendering, and page generation. This scenario is tested by
`CppClass_Construction_SetsCoreProperties`.

**CppEnum stores name and values correctly**: Verifies that `CppEnum` records the name and value
list passed at construction, confirming that enum type metadata is preserved for rendering in enum
pages. This scenario is tested by `CppEnum_Construction_SetsNameAndValues`.

**CppTypeAlias stores name and underlying type correctly**: Verifies that `CppTypeAlias` records
the alias name and underlying type name passed at construction, confirming that type alias metadata
is preserved for rendering in alias pages. This scenario is tested by
`CppTypeAlias_Construction_SetsNameAndUnderlyingType`.

**CppNamespaceDecl stores qualified name correctly**: Verifies that `CppNamespaceDecl` records the
fully qualified namespace name passed at construction, confirming that the namespace key used for
page naming and dictionary lookups is preserved. This scenario is tested by
`CppNamespaceDecl_Construction_SetsQualifiedName`.

**CppCompilationResult stores namespaces and errors correctly**: Verifies that
`CppCompilationResult` records both the namespace declarations and error messages passed at
construction, confirming that the complete parse output is preserved for consumption by the emitter
and diagnostic reporting. This scenario is tested by
`CppCompilationResult_Construction_SetsNamespacesAndErrors`.

**CppAccessibility enum contains Public, Protected, and Private values**: Verifies that the
`CppAccessibility` enum declares the three expected access specifiers, confirming that all C++
access levels required by the visibility filter are represented. This scenario is tested by
`CppAccessibility_Values_ArePublicProtectedPrivate`.

**CppEmitter rejects a null factory with an ArgumentNullException**: Verifies that calling
`CppEmitter.Emit` with a null factory throws `ArgumentNullException` before any I/O is attempted,
providing a clear failure rather than a misleading null-reference error from within a file-write
operation. This scenario is tested by `CppEmitter_Emit_NullFactory_ThrowsArgumentNullException`.

**CppEmitter dispatches GradualDisclosure format to produce multiple files**: Verifies that when
`OutputFormat.GradualDisclosure` is configured the emitter produces more than one Markdown writer,
confirming that the dispatch path routes to the gradual-disclosure emitter. This scenario is tested
by `CppEmitter_Emit_GradualDisclosureFormat_ProducesMultipleFiles`.

**CppEmitter dispatches SingleFile format to produce exactly one api file**: Verifies that when
`OutputFormat.SingleFile` is configured the emitter produces exactly one writer keyed `api`,
confirming that the dispatch path routes to the single-file emitter. This scenario is tested by
`CppEmitter_Emit_SingleFileFormat_ProducesSingleApiFile`.

**CppEmitter SanitizeFileName replaces invalid characters in operator names**: Verifies that
`CppEmitter.SanitizeFileName` replaces file-system-invalid characters such as `*` when applied to
C++ operator names, confirming that the sanitized name is safe for use as a file-system path
component. This scenario is tested by
`CppEmitter_SanitizeFileName_OperatorName_ReplacesInvalidChars`.

**CppEmitter SanitizeFileName leaves regular names unchanged**: Verifies that
`CppEmitter.SanitizeFileName` returns the original name unchanged when it contains no
file-system-invalid characters, confirming that the sanitizer does not corrupt well-formed
identifiers. This scenario is tested by `CppEmitter_SanitizeFileName_RegularName_IsUnchanged`.

**CppEmitter BuildClassDeclaration returns class name for a non-final class with no bases**:
Verifies that `CppEmitter.BuildClassDeclaration` returns `class ClassName` for a class that is
neither final nor derived, confirming that the minimal class declaration is emitted without
unnecessary keywords. This scenario is tested by
`CppEmitter_BuildClassDeclaration_NonFinalNoBase_ReturnsJustClassName`.

**CppEmitter BuildClassDeclaration appends final keyword for a final class**: Verifies that
`CppEmitter.BuildClassDeclaration` appends the `final` specifier to the class declaration when the
class is marked final, confirming that the non-subclassable constraint is visible in the generated
signature block. This scenario is tested by
`CppEmitter_BuildClassDeclaration_FinalClass_AppendsFinalKeyword`.

**CppEmitterGradualDisclosure creates the api index page from minimal data**: Verifies that the
gradual-disclosure emitter creates the `api` writer key when constructed from a minimal
namespace-declarations dictionary (without invoking clang), confirming that the entrypoint page is
emitted correctly for any non-empty namespace set. This scenario is tested by
`CppEmitterGradualDisclosure_Emit_MinimalData_CreatesApiIndexPage`.

**CppEmitterGradualDisclosure creates a namespace page from minimal data**: Verifies that the
gradual-disclosure emitter creates a writer whose key contains the `testlib` namespace name,
confirming that namespace summary pages are emitted for all namespaces in the declarations
dictionary. This scenario is tested by
`CppEmitterGradualDisclosure_Emit_MinimalData_CreatesNamespacePage`.

**CppEmitterGradualDisclosure creates a type page for Widget from minimal data**: Verifies that
the gradual-disclosure emitter creates a writer whose key contains `Widget`, confirming that
per-type pages are emitted for all classes in each namespace. This scenario is tested by
`CppEmitterGradualDisclosure_Emit_MinimalData_CreatesTypePage`.

**CppEmitterGradualDisclosure api index page heading contains the library name**: Verifies that
the api index page includes a heading containing the configured library name, confirming that the
top-level heading identifies the documented library. This scenario is tested by
`CppEmitterGradualDisclosure_Emit_MinimalData_ApiIndexContainsLibraryNameHeading`.

**CppEmitterSingleFile creates exactly one writer from minimal data**: Verifies that the
single-file emitter produces exactly one Markdown writer when constructed from a minimal
namespace-declarations dictionary (without invoking clang), confirming that all documentation is
consolidated into a single file. This scenario is tested by
`CppEmitterSingleFile_Emit_MinimalData_CreatesExactlyOneWriter`.

**CppEmitterSingleFile creates only the api writer from minimal data**: Verifies that the single
writer produced by the single-file emitter is keyed as `api`, confirming that the output file name
follows the established convention for single-file mode. This scenario is tested by
`CppEmitterSingleFile_Emit_MinimalData_CreatesApiFileOnly`.

**CppEmitterSingleFile api file contains a library-name heading**: Verifies that the single output
file includes a heading containing the configured library name, confirming that the top-level
section identifies the documented library. This scenario is tested by
`CppEmitterSingleFile_Emit_MinimalData_ApiFileContainsLibraryNameHeading`.

**CppEmitterSingleFile api file contains a namespace-level heading**: Verifies that the single
output file includes a heading containing the namespace name, confirming that namespaces are
represented as sections in the consolidated document. This scenario is tested by
`CppEmitterSingleFile_Emit_MinimalData_ApiFileContainsNamespaceHeading`.

**ClangAstParser returns non-empty namespaces for fixture headers**: Verifies that parsing the
fixture header files via clang produces at least one namespace, confirming that the clang
invocation and JSON AST parsing pipeline successfully extracts namespace declarations. This
scenario is tested by `ClangAstParser_Parse_FixtureHeaders_ReturnsNonEmptyNamespaces`.

**ClangAstParser produces the fixtures namespace from fixture headers**: Verifies that parsing
the fixture header files produces a namespace whose qualified name contains `fixtures`, confirming
that the correct namespace is extracted and its key is correctly formed. This scenario is tested by
`ClangAstParser_Parse_FixtureHeaders_ContainsFixturesNamespace`.

**ClangAstParser produces a SampleClass in the fixtures namespace**: Verifies that the `fixtures`
namespace produced by parsing the fixture headers contains a class named `SampleClass`, confirming
that class declarations in a specific namespace are correctly attributed to that namespace by the
AST parser. This scenario is tested by
`ClangAstParser_Parse_FixtureHeaders_FixturesNamespaceContainsSampleClass`.

**ClangAstParser extracts member declarations for SampleClass**: Verifies that the `SampleClass`
parsed from the fixture headers has non-empty members, confirming that member declarations within a
class body are correctly extracted and associated with the class. This scenario is tested by
`ClangAstParser_Parse_FixtureHeaders_SampleClassHasMembers`.
