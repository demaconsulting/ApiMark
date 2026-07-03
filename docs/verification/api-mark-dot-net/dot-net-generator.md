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
- Exclude-pattern filtering correctly omits types and namespaces whose full
  namespace-qualified name or containing namespace matches a configured wildcard
  (`*`) exclude pattern, leaves unaffected types unchanged, and causes namespaces
  that become fully excluded to disappear from every generated index and page.
- Members implemented with a bare `<inheritdoc />` tag inherit interface-authored summaries
  and parameter descriptions on their generated member detail pages; the full pipeline from
  Mono.Cecil inheritance mapping through XmlDocReader resolution to emitted Markdown is
  exercised end-to-end.
- `Parse` throws `FileNotFoundException` when the assembly path does not exist on disk,
  and this check occurs before the XML documentation path is verified.
- The `DotNetGenerator` constructor throws `ArgumentNullException` when `options` is null.
- A NamespaceDoc carrier's `<remarks>` and `<example>` content are surfaced on the namespace
  page in addition to the summary.
- A type whose `<remarks>` contains a `<list>` renders the list as Markdown in generated output.

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

**Missing assembly path throws FileNotFoundException**: Verifies that calling `Parse` when the
configured assembly path does not exist throws `FileNotFoundException` with the missing path,
giving callers a precise fail-fast error before any XML documentation path check or Mono.Cecil
I/O is attempted. AssemblyPath is checked before XmlDocPath. This scenario is tested by
`DotNetGenerator_Parse_MissingAssemblyPath_ThrowsFileNotFoundException`.

**Null options throws ArgumentNullException**: Verifies that passing `null` to the
`DotNetGenerator` constructor throws `ArgumentNullException`, preventing a confusing
`NullReferenceException` at an unpredictable point during generation. This scenario is tested
by `DotNetGenerator_Constructor_NullOptions_ThrowsArgumentNullException`.

**Null context throws ArgumentNullException**: Verifies that passing `null` as the
generation context to `DotNetGenerator.Parse` throws `ArgumentNullException`, giving the
caller a precise, actionable failure instead of a `NullReferenceException` at the first
`context.WriteLine` call. This scenario is tested by
`DotNetGenerator_Parse_NullContext_ThrowsArgumentNullException`.

**Single-file output writes a complete api.md tree**: Verifies that when `OutputFormat.SingleFile`
is configured, the generator produces exactly one writer keyed `api`, containing an H1 assembly
title, H2 namespace heading, H3 type heading (e.g., `SampleClass`), H4 member headings with
parentheses, no group headings (`Constructors`, `Methods`, `Properties`), and at least one
compact bullet-list paragraph (`- **MemberName**: description`) summarizing a type's members.
This scenario is tested by
`DotNetGenerator_Generate_SingleFileOutput_WritesSingleApiMarkdown`.

**Namespace description from NamespaceDoc appears on namespace page**: Verifies that when a
namespace carries a NamespaceDoc carrier class, its XML summary is emitted as a paragraph on
the namespace page, confirming that developer-authored namespace descriptions appear in generated
output. This scenario is tested by
`DotNetGenerator_NamespacePage_NamespaceDocClass_ExcludedFromTypeListing`.

**NamespaceDoc remarks appear on the namespace page**: Verifies that a NamespaceDoc carrier's
`<remarks>` content is emitted as a paragraph on the namespace page, alongside the summary. This
scenario is tested by
`DotNetGenerator_NamespacePage_NamespaceDocRemarks_AppearsOnNamespacePage`.

**NamespaceDoc example is emitted as a code block**: Verifies that a NamespaceDoc carrier's
`<example><code>` content is emitted as a fenced code block on the namespace page. This scenario
is tested by `DotNetGenerator_NamespacePage_NamespaceDocExample_EmitsCodeBlock`.

**Remarks bullet list renders as Markdown in generated output**: Verifies that a type whose
`<remarks>` contains a `<list type="bullet">` renders the list as `- item` dash lines within the
type page paragraph, confirming end-to-end list rendering through the generator. This scenario is
tested by `DotNetGenerator_Generate_RemarksWithBulletList_RendersListInMarkdown`.

**Type signature includes direct base class or interface**: Verifies that the type signature
code block for a class that implements an interface includes the interface name in the
`:` clause, confirming that direct inheritance is visible at a glance without opening the source
file. This scenario is tested by
`DotNetGenerator_Generate_SampleImplementation_TypeSignatureShowsInterface`.

**Table cells include Markdown links for intra-assembly types**: Verifies that the Returns
column in the type page Methods table contains a Markdown link for a method that returns an
intra-assembly type, confirming that readers can navigate directly to the referenced type page.
This scenario is tested by
`DotNetGenerator_Generate_IntraAssemblyReturnType_EmitsMarkdownLinkInReturnsCell`.

**External Types section appears when non-System types are referenced**: Verifies that a
member detail page contains an External Types section listing any non-System external types
referenced in its parameters or return type, confirming that readers have the context needed
to identify external dependencies without opening source code. This scenario is tested by
`DotNetGenerator_Generate_ExternalNonSystemParameterType_EmitsExternalTypesSection`.
