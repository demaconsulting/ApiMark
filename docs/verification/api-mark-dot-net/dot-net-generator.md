## DotNetGenerator

### Verification Approach

`DotNetGenerator` is integration-tested in `test/ApiMark.DotNet.Tests/` using controlled sample
assemblies and XML documentation fixtures. Mono.Cecil is used as-is because assembly metadata
interpretation is central to the unit's responsibility. Tests constrain inputs so each assertion
isolates one behavior at a time: type discovery, visibility filtering, type-name simplification,
and Markdown file output. An `InMemoryMarkdownWriterFactory` test
double (from `ApiMark.Core.TestHelpers`) is supplied to capture emitted content without writing to
the file system.

### Test Environment

Tests require sample fixture assemblies compiled with XML documentation enabled and a writable test
output folder. The .NET SDK is required to build the fixture assemblies. No external service,
network dependency, or privileged configuration is needed.

### Acceptance Criteria

- All `DotNetGenerator` tests pass with zero failures.
- Type discovery returns the expected namespaces, types, and member sets for fixture assemblies.
- Visibility filtering correctly excludes non-public members when the public visibility mode is
  selected.
- Type-name simplification renders primitives, generics, nullable types, and common collections in
  the expected C# form.
- Every visible member receives its own dedicated detail page unless case-insensitive file-name
  collisions require combining members onto one page, and the resulting page is linked from the
  type page, making navigation fully deterministic.
- Constructor XML summary and parameter descriptions are read from the XML documentation file
  and written to the constructor's member detail page.
- Operator overload methods are documented on a single per-type `operators.md` page using C#
  operator symbols as headings; no individual per-operator pages are created.
- Generated Markdown content matches expected file names, headings, and signatures.
- Output files follow the naming convention: `api.md` entrypoint, `{Namespace}.md`
  namespace summaries, `{Namespace}/{TypeName}.md` type pages, and
  `{Namespace}/{TypeName}/{MemberName}.md` member detail pages.
- Obsolete member filtering correctly excludes or includes types and members based on the
  IncludeObsolete option.
- Members implemented with a bare `<inheritdoc />` tag inherit interface-authored summaries
  and parameter descriptions on their generated member detail pages; the full pipeline from
  Mono.Cecil inheritance mapping through XmlDocReader resolution to emitted Markdown is
  exercised end-to-end.

### Test Scenarios

**Type discovery finds the expected public API surface**: Verifies that the generator enumerates
the expected namespaces, types, and members from sample assemblies so no documented API surface is
silently missed. This scenario is tested by
`DotNetGenerator_ReadAssembly_WithMonoCecil_ReturnsTypesAndMembers`.

**Constructor accepts assembly and XML paths without error**: Verifies that DotNetGenerator can be
constructed with valid options and that `Parse` (followed by `Emit`) completes without exception, confirming the basic
end-to-end path is functional. This scenario is tested by
`DotNetGenerator_Constructor_AcceptsAssemblyAndXmlPaths`.

**XML documentation comments appear in generated Markdown**: Verifies that summary and remarks text
authored in the XML documentation file are emitted into the Markdown output so generated pages carry
developer-written descriptions. This scenario is tested by
`DotNetGenerator_ReadXmlComments_SummaryAndRemarks_AppearInMarkdown`.

**Visibility filtering excludes and includes members correctly**: Verifies that Public visibility
excludes protected and private members, PublicAndProtected includes protected members, and All
includes private members. This scenario is tested by
`DotNetGenerator_Visibility_PublicPublicAndProtectedAll_FilterExpectedApis`.

**Obsolete toggle controls whether obsolete APIs appear in output**: Verifies that obsolete types
and members are excluded when IncludeObsolete is false and included when it is true. This scenario
is tested by `DotNetGenerator_IncludeObsolete_Toggle_ControlsObsoleteOutput`.

**Error handling rejects a missing XML documentation file**: Verifies that `Parse` throws
`FileNotFoundException` when the configured XML documentation path does not exist on disk. This
scenario is tested by `DotNetGenerator_Generate_XmlDocMissing_ThrowsFileNotFoundException`.

**Type-name simplification renders readable C# signatures**: Verifies that primitive aliases,
nullable forms, generic arguments, and common collection types are simplified into compact,
C#-friendly display text. This scenario is tested by
`ApiMarkDotNet_TypeNames_CommonSignatures_RenderReadably`.

**All members receive dedicated detail pages**: Verifies that every visible member
is emitted as a separate file and linked from its parent type page, making all
navigation paths deterministic without requiring callers to know member content or
shape. This scenario is tested by `DotNetGenerator_AllMembers_GetSeparateFiles`.

**Markdown generation writes expected files and content**: Verifies that generator output includes
expected headings, signatures, and file names for a representative assembly so downstream tools can
consume stable Markdown. This scenario is tested by
`DotNetGenerator_OutputFiles_FollowNamingConvention`.

**api.md lists all namespaces with type count**: Verifies that the api.md entrypoint lists every
namespace — root and child — in a single table with a Types column showing the direct type count
for each namespace, giving AI agents a complete navigation map in one read. This scenario is tested
by `DotNetGenerator_Generate_ApiMd_ListsAllNamespacesWithTypeCount`.

**Constructor XML documentation appears in the constructor detail page**: Verifies that the
XML summary and parameter descriptions authored for a constructor are read from the XML
documentation file and emitted onto the constructor's member detail page, confirming the
`#ctor` doc ID mapping is applied correctly. This scenario is tested by
`DotNetGenerator_Generate_ConstructorWithXmlSummary_WritesSummaryToMemberPage` and
`DotNetGenerator_Generate_ConstructorWithXmlParams_WritesParamDescriptionsToMemberPage`.

**Inherited property documentation appears on the member detail page**: Verifies that the
`SampleImplementation.Name` property detail page contains the summary text `Gets the name.`
inherited from `ISampleInterface.Name` via a bare `<inheritdoc />` tag, proving that the full
pipeline from Mono.Cecil inheritance mapping through XmlDocReader resolution to emitted Markdown
works correctly for properties. This scenario is tested by
`DotNetGenerator_Generate_SampleImplementationNameMemberPage_UsesInheritedSummary`.

**Inherited method documentation appears on the member detail page**: Verifies that the
`SampleImplementation.Execute` method detail page contains both the summary text
`Executes the specified input.` and the `input` parameter description `The input to execute.`
inherited from `ISampleInterface.Execute(string)` via a bare `<inheritdoc />` tag, proving
end-to-end inheritance resolution for both summary and parameter documentation. This scenario is
tested by
`DotNetGenerator_Generate_SampleImplementationExecuteMemberPage_UsesInheritedSummaryAndParamDescription`.

**Operator overloads produce a shared operators.md page**: Verifies that all operator
overload methods defined on a type are grouped onto a single `operators.md` page named with
C# operator symbols as H2 headings, that the type page shows an Operators section linking to
it, and that no individual per-operator pages are created. This scenario is tested by
`DotNetGenerator_Generate_TypeWithOperators_CreatesOperatorsPage`,
`DotNetGenerator_Generate_TypeWithOperators_TypePageHasOperatorsSection`,
`DotNetGenerator_Generate_TypeWithOperators_OperatorsPageContainsSummaries`, and
`DotNetGenerator_Generate_TypeWithOperators_OperatorsPageUsesSymbolHeadings`.

**Conversion operator XML documentation appears on the operators page**: Verifies that XML
summaries authored on `implicit` and `explicit` conversion operators are correctly resolved
using the `~ReturnType` suffix in the XML doc member ID, and that their headings use C# syntax
(`implicit operator T` / `explicit operator T`) rather than the raw IL names (`op_Implicit` /
`op_Explicit`). This scenario is tested by
`DotNetGenerator_Generate_TypeWithConversionOperators_OperatorsPageContainsSummaries` and
`DotNetGenerator_Generate_TypeWithConversionOperators_OperatorsPageUsesConversionSyntax`.

**Nested type page is generated under the outer type folder**: Verifies that a public nested
class declared inside an outer class receives a dedicated page at
`{NamespacePath}/{OuterTypeName}/{NestedTypeName}.md` and that its XML summary appears on that
page. This scenario is tested by
`DotNetGenerator_Generate_NestedClass_CreatesNestedClassPage` and
`DotNetGenerator_Generate_NestedClass_PageContainsSummary`.

**Outer type page lists nested types in a Nested Types section**: Verifies that the outer
type's own page includes a "Nested Types" H2 section with a table row linking to each visible
nested type. This scenario is tested by
`DotNetGenerator_Generate_NestedClass_ListedOnOuterClassPage`.

**Conversion operator returning a nested type resolves XML documentation**: Verifies that when
a conversion operator's return type is a nested type, the `~ReturnType` suffix in the XML doc
member ID uses `.` as the separator (matching the XML doc format) rather than the `/` separator
used by Cecil's `FullName`. This scenario is tested by
`DotNetGenerator_Generate_ConversionOperatorReturningNestedType_OperatorsPageContainsSummary`.

**Case-collision class creates a combined page**: Verifies that members whose names differ only by
case are merged into a single combined detail page on case-insensitive targets. This scenario is
tested by `DotNetGenerator_Generate_CaseCollisionClass_CreatesCombinedPage`.

**Case-collision class does not create a separate cased page**: Verifies that the generator does
not emit a second detail page for the same member name with different casing. This scenario is
tested by `DotNetGenerator_Generate_CaseCollisionClass_DoesNotCreateSeparateCasedPage`.

**Case-collision class combined page contains both members**: Verifies that the combined detail
page includes documentation for both case-colliding members. This scenario is tested by
`DotNetGenerator_Generate_CaseCollisionClass_CombinedPageContainsBothMembers`.

**Single-file output writes a complete api.md tree**: Verifies that when `OutputFormat.SingleFile`
is configured, the generator produces exactly one writer keyed `api`, containing an H1 assembly
title, H2 namespace heading, H3 type heading (e.g., `SampleClass`), H4 member headings with
parentheses, no group headings (`Constructors`, `Methods`, `Properties`), and at least one
compact bullet-list paragraph (`- **MemberName**: description`) summarizing a type's members.
This scenario is tested by
`DotNetGenerator_Generate_SingleFileOutput_WritesSingleApiMarkdown`.

### Unit Test Scenarios

**DotNetAstModel namespaces are sorted alphabetically**: Verifies that `DotNetAstModel.AllNamespaces`
returns all parsed namespaces in ordinal alphabetical order so that downstream pages list namespaces
in a stable, deterministic sequence. This scenario is tested by
`DotNetAstModel_AllNamespaces_ReturnsAlphabeticallySorted`.

**DotNetAstModel namespace dictionary contains the fixture namespace**: Verifies that
`DotNetAstModel.ByNamespace` contains an entry for the fixture namespace after parsing, confirming
that type-lookup by exact namespace name is reliable. This scenario is tested by
`DotNetAstModel_ByNamespace_ContainsFixtureNamespace`.

**DotNetAstModel root-namespace list is non-empty**: Verifies that `DotNetAstModel.RootNamespaces`
is populated after parsing the fixture assembly, confirming that the root-namespace index used when
building namespace folder paths is correctly initialized. This scenario is tested by
`DotNetAstModel_RootNamespaces_ContainsFixtureNamespace`.

**DotNetAstModel options property returns construction-time options**: Verifies that
`DotNetAstModel.Options` returns the same `DotNetGeneratorOptions` instance that was passed at
construction, confirming that configuration is preserved through the parse step for use during
emission. This scenario is tested by `DotNetAstModel_Options_ReturnsOptionsPassedAtConstruction`.

**DotNetAstModel assembly property returns the loaded assembly**: Verifies that
`DotNetAstModel.Assembly` is non-null and has the expected name after parsing, confirming that the
Mono.Cecil `AssemblyDefinition` is retained in the model for member iteration during emission. This
scenario is tested by `DotNetAstModel_Assembly_ReturnsLoadedAssembly`.

**DotNetAstModel resolver property is non-null**: Verifies that `DotNetAstModel.Resolver` is
non-null after parsing, confirming that the type-reference resolver used during link generation is
initialized as part of the model. This scenario is tested by `DotNetAstModel_Resolver_IsNotNull`.

**DotNetEmitter rejects a null factory with an ArgumentNullException**: Verifies that calling
`DotNetEmitter.Emit` with a null factory throws `ArgumentNullException` before any I/O is attempted,
providing a clear failure rather than a misleading null-reference error from within a file-write
operation. This scenario is tested by `DotNetEmitter_Emit_NullFactory_ThrowsArgumentNullException`.

**DotNetEmitter dispatches GradualDisclosure format to produce multiple files**: Verifies that when
`OutputFormat.GradualDisclosure` is configured the emitter produces more than one Markdown writer,
confirming that the dispatch path routes to the gradual-disclosure emitter and not the single-file
emitter. This scenario is tested by
`DotNetEmitter_Emit_GradualDisclosureFormat_ProducesMultipleFiles`.

**DotNetEmitter dispatches SingleFile format to produce exactly one api file**: Verifies that when
`OutputFormat.SingleFile` is configured the emitter produces exactly one writer keyed `api`,
confirming that the dispatch path routes to the single-file emitter. This scenario is tested by
`DotNetEmitter_Emit_SingleFileFormat_ProducesSingleApiFile`.

**GetNamespaceFolderPath returns the dotted name for a root namespace**: Verifies that a namespace
that is itself a configured root namespace returns its full dotted name as the folder path, so
namespace pages land at the expected location in the output tree. This scenario is tested by
`DotNetEmitter_GetNamespaceFolderPath_RootNamespace_ReturnsDottedName`.

**GetNamespaceFolderPath returns a slash-separated path for a child namespace**: Verifies that a
namespace that is a child of a configured root returns a slash-separated path (root dotted name,
then `/`, then the child suffix), so child namespace pages land under the correct parent folder.
This scenario is tested by
`DotNetEmitter_GetNamespaceFolderPath_ChildNamespace_ReturnsSlashSeparated`.

**DotNetEmitterGradualDisclosure creates the api index page**: Verifies that the
gradual-disclosure emitter creates the `api` writer key, confirming that the top-level assembly
entrypoint is emitted as the first page in the output tree. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesApiIndexPage`.

**DotNetEmitterGradualDisclosure creates a namespace page for the fixture namespace**: Verifies
that the gradual-disclosure emitter creates a writer whose key contains the fixture namespace name,
confirming that namespace summary pages are emitted for all discovered namespaces. This scenario is
tested by `DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesNamespacePage`.

**DotNetEmitterGradualDisclosure creates a type page for SampleClass**: Verifies that the
gradual-disclosure emitter creates a writer whose key contains `SampleClass`, confirming that
per-type pages are emitted for all visible types in each namespace. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesTypePage`.

**DotNetEmitterGradualDisclosure api index page heading contains the assembly name**: Verifies that
the api index page includes a heading containing the fixture assembly name, confirming that the
top-level heading identifies the documented assembly so AI readers know its scope immediately. This
scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_ApiIndexContainsAssemblyNameHeading`.

**DotNetEmitterSingleFile creates exactly one writer**: Verifies that the single-file emitter
produces exactly one Markdown writer, confirming that all documentation is consolidated into a
single file and no additional writers are created. This scenario is tested by
`DotNetEmitterSingleFile_Emit_ValidModel_CreatesExactlyOneWriter`.

**DotNetEmitterSingleFile creates only the api writer**: Verifies that the single writer produced
by the single-file emitter is keyed as `api`, confirming that the output file name follows the
established convention. This scenario is tested by
`DotNetEmitterSingleFile_Emit_ValidModel_CreatesApiFileOnly`.

**DotNetEmitterSingleFile api file contains an assembly-level heading**: Verifies that the single
output file includes a heading containing the fixture assembly name, confirming that the top-level
section identifies the documented assembly. This scenario is tested by
`DotNetEmitterSingleFile_Emit_ValidModel_ApiFileContainsAssemblyHeading`.

**DotNetEmitterSingleFile api file contains a namespace-level heading**: Verifies that the single
output file includes a heading containing the fixture namespace name, confirming that namespaces are
represented as sections in the consolidated document. This scenario is tested by
`DotNetEmitterSingleFile_Emit_ValidModel_ApiFileContainsNamespaceHeading`.

**DotNetEmitterSingleFile api file contains a type-level heading for SampleClass**: Verifies that
the single output file includes a heading for `SampleClass`, confirming that all visible types
receive a dedicated section in the consolidated document. This scenario is tested by
`DotNetEmitterSingleFile_Emit_ValidModel_ApiFileContainsTypeHeading`.

**TypeLinkResolver returns an empty string for a null type reference**: Verifies that
`TypeLinkResolver.Linkify` returns an empty string rather than throwing when passed a null type
reference, providing a safe no-op for callers that may encounter unresolvable references during
table cell generation. This scenario is tested by
`TypeLinkResolver_Linkify_NullTypeRef_ReturnsEmptyString`.

**TypeLinkResolver resolves System.Int32 to the C# alias "int"**: Verifies that
`TypeLinkResolver.Linkify` returns the C# primitive alias `int` for `System.Int32`, confirming
that the alias table is applied to well-known system types so table cells contain readable C# names.
This scenario is tested by `TypeLinkResolver_Linkify_Int32_ReturnsCSharpAlias`.

**TypeLinkResolver resolves System.String to the C# alias "string"**: Verifies that
`TypeLinkResolver.Linkify` returns the C# primitive alias `string` for `System.String`, confirming
that string references in table cells are shown using the idiomatic C# keyword. This scenario is
tested by `TypeLinkResolver_Linkify_StringType_ReturnsCSharpAlias`.

**TypeLinkResolver returns a Markdown link for an intra-assembly type when generateLinks is true**:
Verifies that an intra-assembly type resolves to a Markdown link when `generateLinks` is true,
confirming that the link-generation mode produces clickable cross-references in table cells. This
scenario is tested by
`TypeLinkResolver_Linkify_GenerateLinksTrue_IntraAssemblyType_ReturnsMarkdownLink`.

**TypeLinkResolver returns plain text for an intra-assembly type when generateLinks is false**:
Verifies that an intra-assembly type resolves to plain text when `generateLinks` is false,
confirming that the no-link mode suppresses markup for contexts where links would not render. This
scenario is tested by
`TypeLinkResolver_Linkify_GenerateLinksFalse_IntraAssemblyType_ReturnsPlainText`.
