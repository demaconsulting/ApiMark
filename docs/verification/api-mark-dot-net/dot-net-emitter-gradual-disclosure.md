## DotNetEmitterGradualDisclosure

### Verification Approach

`DotNetEmitterGradualDisclosure` is integration-tested by parsing a fixture
assembly and calling `Emit` with `OutputFormat.GradualDisclosure` and an
`InMemoryMarkdownWriterFactory`. Tests inspect the set of created writer keys
and the content written to specific pages. No internal production components
are mocked beyond the in-memory factory.

### Test Environment

Tests require the compiled fixture assembly, its XML documentation file, and
the `InMemoryMarkdownWriterFactory` from `ApiMark.Core.TestHelpers`. No external
service or network dependency is needed.

### Acceptance Criteria

- All `DotNetEmitterGradualDisclosure` tests pass with zero failures.
- The api index page is created with the expected assembly name heading.
- When the assembly carries an `AssemblyDescriptionAttribute`, its value is emitted as a
  paragraph on the api index page after the assembly-level heading.
- When an explicit `LibraryDescription` option is supplied, its value is emitted as the
  paragraph instead of the compiled `AssemblyDescriptionAttribute` value.
- A namespace summary page is created for each namespace in the fixture assembly.
- A type page is created for each visible type in each namespace.
- At least one member detail page is created for a visible member of a visible type.
- Members whose names differ only in case receive separate pages (`Name`, `name-2`) chosen by the registry.
- A type with pure method overloads produces a consolidated method overload page.
- A type with operator overloads produces an `operators.md` page.
- A type with a nested type produces a dedicated page under the containing type's folder.
- When a namespace has NamespaceDoc remarks and example parts, they are emitted on the namespace page after the summary (remarks as a paragraph, example code as a fenced code block).
- A type whose `<remarks>` contains a `<list type="number">` renders the list as ordered Markdown items on the type page.
- A type whose `<summary>` contains a `<list type="number">` renders the list as real multi-line ordered Markdown items on the type page body, with trailing prose as a separate paragraph.
- A member with no `<summary>` but present `<remarks>` content does not show the "No description provided." placeholder on its detail page, and the remarks content is shown.
- A member with neither `<summary>` nor `<remarks>` still shows the "No description provided." placeholder on its detail page (regression guard).
- A `<see cref>` to a visible intra-assembly type renders as a Markdown link in gradual-disclosure output.
- A `<see cref>` to a visible intra-assembly member renders as a Markdown link in gradual-disclosure output.
- A `<see cref>` to a member filtered out by visibility renders as plain code-span text with no link.
- A `<see cref>` to an external framework type renders as plain code-span text with no link.
- A malformed/unresolvable `<see cref>` renders as plain code-span text with no link.
- A `<seealso cref>` to a visible intra-assembly type renders as a Markdown link.
- Every entry in `DotNetAstModel.MemberPageIndex` corresponds to a page actually written during `Emit`.
- Type, member, overload, operators, combined, nested-type and nested-member pages start with the expected breadcrumb paragraph.
- Parameter tables show default values of every supported constant kind and per-type nullability.
- Init-only properties render `init`; generic method signatures include type parameters and constraints; record and struct kind keywords are correct.
- Each fenced example block carries its own language; prose example parts are not fenced.

### Test Scenarios

**Api index page is created**: Verifies that the gradual-disclosure emitter
creates the `api` writer key, confirming that the top-level assembly entrypoint
is emitted as the first page in the output tree. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesApiIndexPage`.

**Api index heading contains the assembly name**: Verifies that the api index
page includes a heading containing the fixture assembly name. This scenario is
tested by `DotNetEmitterGradualDisclosure_Emit_ValidModel_ApiIndexContainsAssemblyNameHeading`.

**Assembly description paragraph follows assembly heading on the api index page**:
Verifies that when the assembly carries an `AssemblyDescriptionAttribute`, its
value is emitted as a paragraph immediately after the assembly-level heading on
the api index page. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_AssemblyWithDescription_EmitsDescriptionParagraph`.

**Explicit LibraryDescription overrides the compiled attribute**: Verifies that
when `DotNetGeneratorOptions.LibraryDescription` is supplied, its value is emitted
as the introductory paragraph on the api index page instead of the assembly's
compiled `AssemblyDescriptionAttribute` value. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_LibraryDescriptionSupplied_OverridesAssemblyDescription`.

**Namespace page is created for the fixture namespace**: Verifies that a writer
whose key contains the fixture namespace name is created. This scenario is tested
by `DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesNamespacePage`.

**NamespaceDoc remarks appear on the namespace page**: Verifies that a namespace
carrying a NamespaceDoc carrier with `<remarks>` has that remarks text emitted as a
paragraph on the namespace page after the summary. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_NamespaceWithDoc_EmitsNamespaceRemarks`.

**NamespaceDoc example is emitted as a code block on the namespace page**: Verifies
that a namespace carrying a NamespaceDoc carrier with `<example><code>` has that
example emitted as a fenced code block on the namespace page. This scenario is tested
by `DotNetEmitterGradualDisclosure_Emit_NamespaceWithDoc_EmitsNamespaceExampleCodeBlock`.

**Remarks numbered list renders as Markdown on the type page**: Verifies that a type
whose `<remarks>` contains a `<list type="number">` renders the list as `1. item`
ordered lines on its type page. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_TypeWithListRemarks_RendersNumberedListInMarkdown`.

**Type page is created for SampleClass**: Verifies that a writer whose key
contains `SampleClass` is created, confirming that per-type pages are emitted for
all visible types. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesTypePage`.

**Member detail page is created for SampleClass.Reset**: Verifies that a writer
whose key contains both `SampleClass` and `Reset` is created, confirming that
per-member detail pages are emitted for all visible members. This scenario is
tested by `DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesMemberDetailPage`.

**Case-twin members get separate pages for CaseCollisionClass**: Verifies that when
`CaseCollisionClass` has members whose names differ only in case (`name`
and `Name`), the emitter creates two pages, `Name` and `name-2`, chosen by the registry. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_CaseCollision_CreatesSeparatePages`.

**Method overload page is created for overloaded methods**: Verifies that a type
with multiple overloads of the same method name produces a consolidated overload
page rather than separate pages per overload. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesMethodOverloadPage`.

**Operators page is created for types with operator overloads**: Verifies that a
type defining operator overloads produces an `operators.md` page under the type
folder. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesOperatorsPage`.

**Nested type page is created under the containing type's folder**: Verifies that
a type containing a nested type produces a dedicated page for that nested type
placed under the containing type's folder path. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesNestedTypePage`.

**Nested Types section is written even when the containing type has no own
members**: Verifies that a type acting as a pure namespace-like container —
with no own members/operators, only a nested type — still has its Nested
Types section written and its nested type's page created, so that cref links
into the nested type (which `DotNetGenerator.CollectTypeCrefLinkIndex`
indexes unconditionally) never dangle. This scenario is tested using the
`TwoLevelNestedClass`/`Middle`/`Inner` fixture by
`DotNetGenerator_Generate_GradualDisclosure_MemberlessContainerType_WritesNestedTypePages`.

**Child namespace page is created**: Verifies that a child namespace (such as
`ApiMark.DotNet.Fixtures.Inner`) also produces a dedicated Markdown summary page,
confirming that child namespace enumeration works correctly. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_ValidModel_CreatesChildNamespacePage`.

**Summary numbered list renders as multi-line Markdown on the type page body**:
Verifies that a type whose `<summary>` contains a `<list type="number">`
renders the list as real multi-line ordered Markdown items on the type page
body — a blank line before the list, each `1.` item on its own line, and
trailing prose following `</list>` as a separate paragraph — confirming the
type-page body switched from the single-line `GetSummary` to the multi-line
`GetSummaryMarkdown`. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_TypeWithListSummary_RendersNumberedListInMarkdown`.

**Member with remarks-only content suppresses the placeholder and shows the
remarks**: Verifies that `GeneratedRegexClass.DigitsRegex` — whose compiled XML
documentation genuinely has only `<remarks>` and no `<summary>` — does not show
the "No description provided." placeholder on its member-detail page, and that
its remarks content is shown instead, confirming the placeholder-suppression
rule (summary absent AND remarks present ⇒ no placeholder). This scenario is
tested by
`DotNetEmitterGradualDisclosure_Emit_MemberWithRemarksOnly_SuppressesPlaceholderAndShowsRemarks`.

**Member with neither summary nor remarks still shows the placeholder
(regression guard)**: Verifies that, using a synthetic XML doc that omits any
`<member>` entry for `GeneratedRegexClass.DigitsRegex`, the member-detail page
still shows the "No description provided." placeholder — confirming the
placeholder is suppressed only when remarks content is actually present, not
unconditionally. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_MemberWithNeitherSummaryNorRemarks_StillShowsPlaceholder`.

**Type with remarks-only content suppresses the placeholder and shows the
remarks on its type page**: Verifies that, using a synthetic XML doc override
that gives `SampleClass` only a `<remarks>` entry and no `<summary>`, the
placeholder-suppression rule applies identically at the type level — the
type's own page (distinguished from a same-named constructor member page by
its exact `<namespace-folder>/SampleClass` key) shows the remarks text and not
the "No description provided." placeholder. Exercises the type-level branch in
`WriteTypeHeaderSections` directly, which the member-level test above does not
cover. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_TypeWithRemarksOnly_SuppressesPlaceholderAndShowsRemarksOnTypePage`.

**CrefLinking: see cref to a visible type renders as a Markdown link**:
Verifies end-to-end that a `<see cref>` pointing at another in-assembly,
visible type renders as a real relative Markdown link (code-span wrapped) in
gradual-disclosure output, confirming the full `CrefLinkContext`
construction/threading chain from `DotNetGenerator.Parse` through
`DotNetEmitterGradualDisclosure`'s page writers to `XmlDocReader` works
end-to-end. This scenario is tested by
`CrefLinking_SeeCrefToVisibleType_RendersAsMarkdownLink`.

**CrefLinking: see cref to a visible member renders as a Markdown link**:
Verifies the member counterpart: a `<see cref>` pointing at a visible
in-assembly member renders as a Markdown link resolved via
`DotNetAstModel.MemberPageIndex`. This scenario is tested by
`CrefLinking_SeeCrefToVisibleMember_RendersAsMarkdownLink`.

**CrefLinking: see cref to a member filtered out by visibility falls back
with no link**: Verifies that a `<see cref>` pointing at a member excluded
from the generated documentation by the active visibility settings renders
as plain code-span text, with no link, exactly as it did before this feature
existed. This scenario is tested by
`CrefLinking_SeeCrefToFilteredMember_RendersFallbackWithNoLink`.

**CrefLinking: see cref to an external type falls back with no link**:
Verifies that a `<see cref>` pointing at an external framework type (not
indexed by `CrefTargetResolver`) renders as plain code-span text, with no
link. This scenario is tested by
`CrefLinking_SeeCrefToExternalType_RendersFallbackWithNoLink`.

**CrefLinking: malformed cref falls back with no link**: Verifies that a
malformed/unresolvable cref string renders as plain code-span text, with no
link, matching today's fallback. This scenario is tested by
`CrefLinking_SeeCrefMalformed_RendersFallbackWithNoLink`.

**CrefLinking: seealso cref to a visible type renders as a Markdown link**:
Verifies that `<seealso cref>` (nested inside `<remarks>`) shares the same
end-to-end linking path as `<see cref>` in gradual-disclosure output. This
scenario is tested by
`CrefLinking_SeeAlsoCrefToVisibleType_RendersAsMarkdownLink`.

**CrefLinking: see cref in a namespace's NamespaceDoc remarks renders as a
Markdown link**: Verifies that `<see cref>` inside the `<remarks>` of a
`NamespaceDoc` carrier class is resolved and linkified on the namespace page,
using a `CrefLinkContext` scoped to the namespace page's own folder — the
same linking path as type- and member-level summaries, applied to
namespace-level prose. This scenario is tested by
`DotNetGenerator_NamespacePage_NamespaceDocRemarksSeeCref_RendersAsLink`.

**Every MemberPageIndex entry matches an actually-written page**: Verifies,
across the full fixture assembly, that every entry in
`DotNetAstModel.MemberPageIndex` (populated by `BuildMemberPageIndex` via
`DotNetGenerator.CollectTypeCrefLinkIndex`) corresponds to a page key that a
writer actually wrote during `Emit` — directly guarding against
`BuildMemberPageIndex`'s grouping logic silently drifting out of sync with
the real output of `ProcessTypeMembers`/`ProcessOverloadGroup`/
`ProcessCollisionMember`, since both the index builder and the page writer
now call the same extracted `GroupMembersByFileName`/`GetOrderedOverloads`/
`DecideGroupPageFileName` helpers but could in principle diverge again
through a future edit to only one call site. This scenario is tested by
`BuildMemberPageIndex_AllEntries_MatchActualWrittenPages`.

**Breadcrumbs on every page kind**: Arrange the fixture assembly. Act by emitting gradual
disclosure. Expected: the first paragraph of each type, member, overload, operators, combined,
nested-type and nested-member page is the relative breadcrumb with the expected crumbs and
`›` separator (type pages omit the type itself; other pages include it). Tested by
`DotNetEmitterGradualDisclosure_Emit_Pages_HaveRelativeBreadcrumbs`,
`DotNetEmitterGradualDisclosure_Emit_OverloadPage_HasBreadcrumb`,
`DotNetEmitterGradualDisclosure_Emit_NestedType_BreadcrumbsIncludeOuterType` and
`DotNetEmitterGradualDisclosure_Emit_MemberPage_FirstParagraphIsBreadcrumb`. Cross-reference
tests exempt only that first breadcrumb paragraph from the no-link check, so links in the
body are still checked.

**API Reference crumb on every page**: Arrange the fixtures assembly and the global-namespace fixtures
assembly. Act by emitting gradual disclosure and walking every page except `api.md`. Expected: the paragraph
after the H1 starts with `[API Reference](` and a relative link that resolves to `api.md`, and every link in the
breadcrumb resolves to an emitted page. Tested by
`DotNetEmitterGradualDisclosure_Emit_AllPages_HaveApiReferenceBreadcrumb` and
`DotNetEmitterGradualDisclosure_Emit_GlobalNamespacePages_HaveApiReferenceBreadcrumb`.

**Global-namespace types**: Arrange the `ApiMark.DotNet.Fixtures.Global` assembly, whose public
types are declared without a namespace. Act by emitting gradual disclosure. Expected: no
exception; a `global-namespace` page with H1 `(global namespace)` and type pages under
`global-namespace/`; an `api.md` row `[(global namespace)](global-namespace.md)` with the
type count; namespace-page links to the type pages; breadcrumbs starting with
`[(global namespace)]`; crefs and signature types linking to the global type pages. An
assembly without global types produces no `global-namespace` output. Tested by
`DotNetEmitterGradualDisclosure_Emit_GlobalNamespaceTypes_DoesNotThrowAndCreatesPages`,
`DotNetEmitterGradualDisclosure_Emit_GlobalNamespaceTypes_ApiIndexListsGlobalNamespace`,
`DotNetEmitterGradualDisclosure_Emit_GlobalNamespaceTypes_NamespacePageLinksToTypePages`,
`DotNetEmitterGradualDisclosure_Emit_GlobalNamespaceTypes_BreadcrumbLinksToGlobalNamespacePage`,
`DotNetEmitterGradualDisclosure_Emit_GlobalNamespaceTypes_CrefResolvesToTypePage`,
`DotNetEmitterGradualDisclosure_Emit_GlobalNamespaceTypes_SignatureTypeLinksToTypePage` and
`DotNetEmitterGradualDisclosure_Emit_NoGlobalNamespaceTypes_NoGlobalNamespaceOutput`.

**Parameter default values**: Arrange `DefaultValueClass` members covering null, default,
enum, bool, numeric, string, char and long defaults. Act by emitting. Expected: each parameter
table row shows the C#-source default. Tested by
`DotNetEmitterGradualDisclosure_Emit_DefaultValueParameter_ShowsDefaultInParameterTable`,
`DotNetEmitterGradualDisclosure_Emit_DefaultValueVariants_RenderDefaults` and
`DotNetEmitterGradualDisclosure_Emit_CharAndLongDefaults_RenderDefaults`.

**Per-type nullability**: Arrange nullable parameters and a `Func<...>` return. Expected:
each type keeps its own nullable flags. Tested by
`DotNetEmitterGradualDisclosure_Emit_NullableParameterAndFuncReturn_RendersPerTypeFlags`.

**Init accessors**: Expected: init-only properties render `init`. Tested by
`DotNetEmitterGradualDisclosure_Emit_InitOnlyProperties_RenderInitAccessor`.

**Generic method signatures**: Expected: the signature has the type parameters and `where`
constraints. Tested by
`DotNetEmitterGradualDisclosure_Emit_GenericMethod_SignatureHasTypeParametersAndConstraints`.

**Record and struct kinds**: Expected: type signatures show `record struct`,
`readonly record struct` and related kind keywords. Tested by
`DotNetEmitterGradualDisclosure_Emit_RecordAndStructKinds_ShowKindKeywords`.

**Example languages**: Arrange `LangExampleClass` and `LangParaExampleClass`. Expected: each
code block is fenced with its own language, and prose parts are paragraphs, not fences. Tested by
`DotNetEmitterGradualDisclosure_Emit_LangExample_TagsEachCodeBlockWithItsLanguage` and
`DotNetEmitterGradualDisclosure_Emit_LangExampleInPara_TagsCodeBlocksAndKeepsProseAsParagraphs`.

**Exact overview rows and headings**: Arrange the nullable, params, inheritance, and accessor
fixtures. Act by emitting gradual disclosure. Expected: exact Member and Type cells (names,
defaults, `params`, tuples, `?`), exact H1 headings, a `protected` marker on protected rows, rows
whose parameter text equals the signature block's, and no `void?` anywhere. Tested by
`DotNetEmitterGradualDisclosure_Emit_NullableMatrixAndParams_OverviewRowsMatchSignature`,
`DotNetEmitterGradualDisclosure_Emit_ParamsConstructor_OverviewRowShowsParams`,
`DotNetEmitterGradualDisclosure_Emit_MethodPageHeading_ShowsNullabilityAndParams`,
`DotNetEmitterGradualDisclosure_Emit_MatrixTypes_RowParametersEqualSignatureParameters`,
`DotNetEmitterGradualDisclosure_Emit_AllFixtures_NeverRenderNullableVoid`,
`DotNetEmitterGradualDisclosure_Emit_InheritanceShapes_PagesShowModifiers`,
`DotNetEmitterGradualDisclosure_Emit_ProtectedMember_OverviewRowHasProtectedMarker`, and
`DotNetEmitterGradualDisclosure_Emit_AccessorShapes_PageSignatureHonorsVisibility`.

**Overload index and escaped headings**: Arrange the `OverloadMatrix` fixtures (non-generic and
generic containing types; `ToAvalonia` with four return types; `Scale` with a repeated return type;
non-generic and generic `Transact`; text-only example on `Pick<T>`). Act by emitting gradual
disclosure. Expected: the Returns cell lists distinct return types in overload order
(`decimal, float, long, char[]`; `double`; `int, T`), headings are escaped (`Transact\<T\>(Func\<T\>)`),
the `<example><code>` block appears only after the generic overload heading, a text-only example is a
paragraph, and no heading or link label contains an unescaped angle bracket. Tested by
`DotNetEmitterGradualDisclosure_Emit_OverloadGroupRow_ListsDistinctReturnTypesInOrder`,
`DotNetEmitterGradualDisclosure_Emit_OverloadPage_EscapesGenericHeadings`,
`DotNetEmitterGradualDisclosure_Emit_OverloadPage_ExampleAttachedToDocumentingOverload`,
`DotNetEmitterGradualDisclosure_Emit_TextOnlyExample_RendersAsProse`, and
`DotNetEmitterGradualDisclosure_Emit_AllFixtures_NoUnescapedAngleBracketsInHeadingsOrLinkLabels`.

**Overload index aggregation**: Arrange the `ConversionExtensions` fixture (four `ToAvalonia` overloads
with distinct summaries returning `Color`, `Point`, `Size`, and `Rect`) and `UndoHistory` (a void and a generic
`Transact` with different summaries; two `Clear` overloads with the same summary). Act by emitting gradual
disclosure. Expected: the summary cell is `Overloaded; see the member page.` for differing summaries and
`Clears the history.` for identical ones; the Returns cell lists every distinct type in overload order
(`[Color](Color.md), [Point](Point.md), [Size](Size.md), [Rect](Rect.md)` and `void, T`). Tested by
`DotNetEmitterGradualDisclosure_Emit_OverloadIndex_DifferingSummaries_UsesNeutralText`,
`DotNetEmitterGradualDisclosure_Emit_OverloadIndex_IdenticalSummaries_UsesSharedSummary`,
`DotNetEmitterGradualDisclosure_Emit_OverloadIndex_AllSummariesMissing_UsesNeutralText` and
`DotNetEmitterGradualDisclosure_Emit_OverloadIndex_SomeSummariesMissing_UsesNeutralText` (a stripped copy of the
XML docs makes the `Clear` overloads lack a summary entirely or partly; the cell must stay neutral, never the
no-description placeholder), and
`DotNetEmitterGradualDisclosure_Emit_OverloadIndex_ReturnColumn_ListsDistinctTypesIncludingVoid`.

**Overload page sections**: Arrange `UndoHistory.Transact`. Act by emitting gradual disclosure. Expected: the
page headings are exactly `Transact`, `Transact(string, Action)`, and `Transact\<T\>(string, Func\<T\>)`; the
example code block follows only the generic heading, and a Type Parameter table with `T` and its description
follows the generic heading only. Tested by `DotNetEmitterGradualDisclosure_Emit_OverloadPage_HasExactHeadings`,
`DotNetEmitterGradualDisclosure_Emit_OverloadPage_ExampleOnlyUnderGenericHeading`, and
`DotNetEmitterGradualDisclosure_Emit_OverloadPage_GenericOverloadHasTypeParameterTable`.

**Overload cref links and anchors**: Arrange summaries with crefs to `Transact(string, Action)`,
`Transact{T}(string, Func{T})` and `ConversionExtensions.ToAvalonia(int)`. Act by emitting gradual
disclosure. Expected: link text keeps the declaring type and C# parameter types, for example
``[`UndoHistory.Transact(string, Action)`](Transact.md#transactstring-action)``, and every anchor in every
generated link equals the GitHub slug of a heading on the linked page. Tested by
`DotNetEmitterGradualDisclosure_Emit_OverloadCref_LinksToOverloadHeadingAnchor` and
`DotNetEmitterGradualDisclosure_Emit_AllLinkAnchors_MatchRealHeadings`.

**Inline code for references**: Arrange `InlineCodeDocs.Format<T>` whose summary, typeparam, param, returns,
exception, remarks, and example text use `paramref`, `typeparamref`, and `langword`. Act by emitting gradual
disclosure. Expected: each reference appears as a code span, such as ``Formats `value` of type `T`, or `null`.``.
Tested by `DotNetEmitterGradualDisclosure_Emit_InlineReferences_RenderAsCodeSpansEverywhere`.

**Generic type pages**: Arrange the `GenericTypeDocs` fixtures. Act by emitting gradual disclosure.
Expected: file names use the brace-arity `Name{arity}` form, the H1 is `PropertyEditorRegistry\<TEditor\>`, a
Type Parameters table lists each `<typeparam>` (placeholder when undocumented), signatures carry
`where` constraints, and a non-generic nested type has no Type Parameters section. Tested by
`DotNetEmitterGradualDisclosure_Emit_GenericTypePage_ShowsEscapedHeadingAndTypeParameters`,
`DotNetEmitterGradualDisclosure_Emit_MultiParameterGenericType_ShowsPlaceholderAndConstraints`,
`DotNetEmitterGradualDisclosure_Emit_GenericMethodPage_ShowsConstraintAndTypeParameters`, and
`DotNetEmitterGradualDisclosure_Emit_NestedTypeOfGenericHost_HasNoOwnTypeParameters`.

**Namespace navigation**: Arrange the root, `Inner.RemarksOnly` and `Navigation` namespaces. Act by
emitting gradual disclosure. Expected: exact breadcrumbs on namespace pages, a Start here paragraph
and a Key Types column (documented types first, then alphabetical, at most three). Tested by
`DotNetEmitterGradualDisclosure_Emit_NamespacePages_ShowBreadcrumb` and `DotNetEmitterGradualDisclosure_Emit_ApiPage_HasStartHereAndKeyTypes`.

**Legal file names**: Emits the `LegalNameShapes` fixtures (explicit generic interface
implementations, nested generics, operators, types named `CON` or `Aux`, case-differing names) at
every visibility level. Expected: every path segment is legal on Windows and Linux, no two paths
collide case-insensitively, and every relative link resolves case-sensitively. Tested by
`DotNetEmitterFileNameLegalityTests`.

**Generics and inheritance docs test classes**: `DotNetEmitterGradualDisclosureGenericsTests` covers the generic type,
method, overload, and navigation scenarios above; `DotNetEmitterInheritanceDocsTests` covers inherited-member
listing, unresolved `<inheritdoc/>` placeholders, record primary-constructor summaries, and external cref linking.
