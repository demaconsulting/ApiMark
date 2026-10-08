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
- A single combined page is created for members whose sanitized file names collide on a
  case-insensitive filesystem.
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

**Combined collision page is created for CaseCollisionClass**: Verifies that when
`CaseCollisionClass` has members whose sanitized names differ only in case (`name`
and `Name`), the emitter creates a single combined page keyed by the lower-invariant
name rather than two separate colliding pages. This scenario is tested by
`DotNetEmitterGradualDisclosure_Emit_CaseCollision_CreatesCombinedPage`.

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
