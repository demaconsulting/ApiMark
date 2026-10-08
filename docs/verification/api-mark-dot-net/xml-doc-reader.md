## XmlDocReader

### Verification Approach

`XmlDocReader` is unit-tested with temporary XML documentation files written by
each test's arrange step. Each test writes a minimal XML doc file containing only
the member element required for the assertion, calls the relevant getter, and then
deletes the file in a `finally` block. No mocking is required; the class has no
injectable dependencies other than the optional external member lookup delegate,
which is exercised with hand-built `XElement` instances and simple lambda
delegates (no `ExternalXmlDocResolver` instance or file I/O is required for these
tests — the delegate is the seam under test, not its typical
`ExternalXmlDocResolver`-backed implementation, which is covered separately in
`ExternalXmlDocResolverTests.cs`). Tests exercise each getter independently, plus the
constructor's file-not-found guard, and all four `<inheritdoc />` resolution
styles (bare, `cref`, `path`, and `cref + path`), including their cross-assembly
external-fallback variants.

### Test Environment

Tests require write access to the temporary file system path returned by
`Path.GetTempFileName()`. No external service, network dependency, or fixture
assembly is needed.

### Acceptance Criteria

- All `XmlDocReader` tests pass with zero failures.
- `XmlDocReader` constructor throws `FileNotFoundException` for a missing file.
- `GetSummary` returns trimmed single-line text and preserves inline references.
- `GetSummary` returns `null` when the member is absent.
- `GetRemarks` returns trimmed multi-line text for a present member.
- `GetRemarks` returns `null` when the member or element is absent.
- `GetExceptions` returns all `cref` values from `<exception>` elements.
- `GetExceptionDetails` returns formatted type names and descriptions; empty-cref entries are filtered.
- `GetParams` returns parameter names and descriptions in declaration order.
- `GetReturns` returns trimmed returns text.
- `GetReturns` returns `null` when the member or element is absent.
- `GetExample` returns trimmed text; `null` for whitespace-only content.
- `GetExample` returns `null` when the member or element is absent.
- `GetExampleParts` separates prose text nodes from `<code>` elements.
- `GetExampleParts` strips common leading indentation from multi-line `<code>` blocks.
- `GetExampleParts` preserves relative indentation when lines have varying indent depths.
- `GetExampleParts` preserves blank lines inside `<code>` blocks without counting them in the indent calculation.
- `GetExampleParts` applies dedent to the whole-value fallback path when no `<code>` children are present.
- `GetExampleParts` returns an empty list when the member is absent.
- `GetRemarks` renders a `<list type="bullet">` as `- item` dash lines.
- `GetRemarks` renders a `<list type="number">` as `1. item` ordered lines.
- `GetRemarks` renders a `<list type="table">` as a Markdown pipe table with a header and separator row.
- A `<list>` item with both `<term>` and `<description>` renders as `**term** — description`.
- Inline elements such as `<c>` inside a `<list>` item render via the shared inline dispatch.
- A `<list>` surrounded by prose keeps a blank-line separation so the Markdown list renders correctly.
- `<inheritdoc cref="..." />` resolves documentation from the explicitly named member.
- `<inheritdoc cref="..." />` returns `null` when the cref target is absent.
- `<inheritdoc cref="..." />` returns `null` on a cyclic chain without throwing.
- `<inheritdoc path="..." />` applies an XPath filter to the resolved source member.
- `<inheritdoc path="..." />` returns `null` when the path expression matches nothing.
- `<inheritdoc cref="..." path="..." />` selects filtered sections from an explicit target.
- Bare `<inheritdoc />` with an injected chain resolves to the base member.
- Bare `<inheritdoc />` without a chain returns `null`.
- Bare `<inheritdoc />` with a chain entry pointing to an absent member returns `null`.
- Multi-hop `cref` chains resolve transitively (A → B → C yields C's docs).
- A failed first-candidate traversal does not poison the visited set for subsequent candidates in a bare `<inheritdoc />` chain.
- A bare `<inheritdoc />` chain candidate absent locally resolves via an injected external member lookup delegate.
- An explicit `cref` target absent locally resolves via an injected external member lookup delegate.
- The `path` XPath filter applies identically to content resolved from the external member lookup delegate.
- A bare `<inheritdoc />` chain candidate absent both locally and externally returns `null`.
- Cycle detection catches a resolution chain that crosses the local/external boundary.
- Constructing an `XmlDocReader` without an external member lookup (the 2-arg overload) preserves the prior local-miss behavior exactly.
- A local member that is simply undocumented (no local entry, no `<inheritdoc />` anywhere) is never satisfied by an incidentally-colliding member ID in the external member lookup delegate, even when one is configured.
- `GetSummaryMarkdown` returns trimmed summary text unchanged for a member with no `<list>` content, matching `GetSummary`'s plain-text behavior.
- `GetSummaryMarkdown` returns `null` for a member not present in the XML doc file.
- `GetSummaryMarkdown` follows a `cref` inheritdoc reference and returns the summary from the referenced target member, matching `GetSummary`'s inheritdoc-resolution behavior.
- `GetSummaryMarkdown` renders a `<list type="number">` inside a `<summary>` as real multi-line ordered Markdown items, with trailing prose as a separate paragraph.
- `GetSummaryMarkdown` renders a `<list type="bullet">` inside a `<summary>` as real multi-line dash items.
- `GetSummaryMarkdown` renders a `<list type="table">` inside a `<summary>` as a real multi-line Markdown table.
- `GetSummary` renders a `<list type="number">` inside a `<summary>` as single-line inline numbered markers `(1) ... (2) ...`.
- `GetSummary` renders a `<list type="bullet">` inside a `<summary>` as single-line inline numbered markers.
- `GetSummary` renders a `<list type="table">` inside a `<summary>` as single-line inline numbered markers.
- `GetSummary` renders a `<see cref="P:...">` reference as `` `Type.Member` `` wrapped in an inline code span.
- `GetSummary` renders a `<see cref="F:...">` reference as `` `Type.Member` `` wrapped in an inline code span.
- `GetSummary` renders a `<see cref="E:...">` reference as `` `Type.Member` `` wrapped in an inline code span.
- `GetSummary` continues to render a `<see cref="M:...">` reference as `` `Type.Member` `` wrapped in an inline code span (regression guard; method crefs were unaffected by the P/F/E type-name fix, but are included in the new code-span-wrapping behavior).
- A constructor (`#ctor`) cref renders its display text unwrapped (no inline code span). A type-only (`T:`) cref is wrapped in an inline code span the same as the `P`/`F`/`E`/`M` member crefs above (see "GetSummary wraps a plain type-only cref in a code span" in the Test Scenarios below).
- `GetRemarks` renders a `<br/>` element as a paragraph break (a blank line) between the surrounding text.
- `GetSummary` collapses a `<br/>` element to a single space, since a blank line has no meaning in a single-line context.
- `GetRemarks` renders a single-line `<code>` element as an inline backtick code span.
- `GetRemarks` renders a multi-line `<code>` element as a fenced Markdown code block surrounded by blank-line separators.
- A fenced `<code>` block's internal whitespace (multiple consecutive spaces, tabs) survives whitespace normalization byte-for-byte.
- An empty or whitespace-only `<code>` element emits nothing, mirroring the existing `<c>` empty-skip guard.
- `GetSummary` flattens a multi-line `<code>` element to a single-line inline code span rather than a fenced block.
- A fenced `<code>` block whose content contains an embedded backtick run uses a longer fence so the delimiter is unambiguous.
- `GetRemarks` leaves at most one blank line between a `<list>` and an immediately following fenced `<code>` block.
- `GetRemarks` collapses three or more consecutive `<br/>` tags to at most one blank line.
- A multi-line `<code>` element nested inside a `<list>` item's `<description>` renders as an inline span, not a fenced block.
- A multi-line `<code>` element reached through `<example>` mixed-prose accumulation (nested inside a `<para>`, not a direct `<example>` child) renders as an inline span, not a fenced block.
- Every public `XmlDocReader` method called without a `linkContext` argument (or with `linkContext: null`) renders a resolvable cref exactly as it did before cross-reference linking existed — code-span-only, never linked.
- A `<see cref>` to an intra-assembly type that will be emitted, rendered with a non-null `linkContext` resolving that type and reporting it as emitted, produces a Markdown link nested inside the code span.
- A `<see cref>` to an intra-assembly member that will be emitted, rendered with a non-null `linkContext` resolving that member and reporting it as emitted, produces a Markdown link (via the member-page index) nested inside the code span.
- A `<see cref>` to an external (non-indexed) type falls back to code-span-only rendering even with a non-null `linkContext`.
- A malformed/unresolvable `cref` string falls back to code-span-only rendering even with a non-null `linkContext`.
- A `<see cref>` to a constructor falls back to its existing unwrapped-text rendering even with a non-null `linkContext` — constructors are never linked.
- A `<seealso cref>` to a resolved, emitted intra-assembly type (nested inside `<remarks>`) produces a Markdown link nested inside the code span, confirming `<seealso>` shares the same `TryLinkifyCref` rendering path as `<see>`.

### Test Scenarios

**Constructor throws FileNotFoundException for a missing file**: Verifies that
constructing an `XmlDocReader` with a non-existent path throws
`FileNotFoundException`. This scenario is tested by
`XmlDocReader_Constructor_FileDoesNotExist_ThrowsFileNotFoundException`.

**GetSummary returns trimmed text for a present member**: Verifies that leading
and trailing whitespace is removed from the summary element's text. This scenario
is tested by `XmlDocReader_GetSummary_MemberPresent_ReturnsTrimmedText`.

**GetSummary preserves inline symbol references and language keywords**: Verifies
that `<see langword="..."/>`, `<paramref name="..."/>`, and `<see cref="..."/>`
elements are rendered as readable text in the summary. This scenario is tested by
`XmlDocReader_GetSummary_WithInlineReferences_PreservesReferencedNames`.

**GetSummary returns null for an absent member**: Verifies that a member not
present in the XML doc file returns `null` rather than throwing. This scenario is
tested by `XmlDocReader_GetSummary_MemberAbsent_ReturnsNull`.

**GetRemarks returns trimmed text for a present member**: Verifies that remarks
text is returned with whitespace normalized. This scenario is tested by
`XmlDocReader_GetRemarks_MemberPresent_ReturnsTrimmedText`.

**GetRemarks returns null when the member or element is absent**: Verifies that
`GetRemarks` returns `null` when the member does not exist in the XML doc file.
This scenario is tested by `XmlDocReader_GetRemarks_MemberAbsent_ReturnsNull`.

**GetExceptions returns all cref values for a present member**: Verifies that all
`<exception cref="...">` values are returned as a list. This scenario is tested by
`XmlDocReader_GetExceptions_MemberWithExceptions_ReturnsCrefValues`.

**GetExceptionDetails returns formatted types and descriptions**: Verifies that
exception type names are formatted (type-kind prefix stripped, primitive aliases
applied) and descriptions are included. This scenario is tested by
`XmlDocReader_GetExceptionDetails_MemberWithExceptions_ReturnsFormattedTypesAndDescriptions`.

**GetExceptionDetails flattens a multi-line `<code>`/`<br/>` description to a
single line**: Verifies that an exception description containing a multi-line
`<code>` element (and a `<br/>`) is rendered without any embedded literal
newline, since every caller writes the description into a raw pipe-delimited
Markdown table cell, where an embedded newline would corrupt the table. This
scenario is tested by
`XmlDocReader_GetExceptionDetails_DescriptionWithMultiLineCode_FlattensToSingleLineNoEmbeddedNewline`.

**GetParams returns names and descriptions in order**: Verifies that parameter
name and description pairs are returned in declaration order. This scenario is
tested by `XmlDocReader_GetParams_MemberWithParams_ReturnsNamesAndDescriptions`.

**GetParams flattens a multi-line `<code>`/`<br/>` description to a single
line**: Verifies that a parameter description containing a multi-line `<code>`
element (and a `<br/>`) is rendered without any embedded literal newline, for
the same table-cell-safety reason as `GetExceptionDetails`. This scenario is
tested by
`XmlDocReader_GetParams_DescriptionWithMultiLineCodeAndBr_FlattensToSingleLineNoEmbeddedNewline`.

**GetParams preserves significant whitespace inside an inline `<code>` span in
a description**: Verifies that multiple consecutive spaces and a tab inside a
single-line `<code>` element survive unchanged, proving the placeholder-token
protection mechanism (previously only exercised via `GetRemarks`'s fenced-block
path) also protects the inline-code-span path used by table-cell-bound
rendering. This scenario is tested by
`XmlDocReader_GetParams_DescriptionWithInlineCodeContainingInternalWhitespace_PreservesExactWhitespace`.

**GetReturns returns trimmed text for a present member**: Verifies that returns
text is returned with whitespace trimmed. This scenario is tested by
`XmlDocReader_GetReturns_MemberWithReturns_ReturnsTrimmedText`.

**GetReturns returns null when the member or element is absent**: Verifies that
`GetReturns` returns `null` when the member does not exist in the XML doc file.
This scenario is tested by `XmlDocReader_GetReturns_MemberAbsent_ReturnsNull`.

**GetExample returns trimmed text for a present member**: Verifies that example
text is returned with leading and trailing whitespace removed. This scenario is
tested by `XmlDocReader_GetExample_MemberWithExample_ReturnsTrimmedText`.

**GetExample returns null for whitespace-only content**: Verifies that an
`<example>` element containing only whitespace collapses to `null`. This scenario
is tested by `XmlDocReader_GetExample_WhitespaceOnly_ReturnsNull`.

**GetExample returns null when the member or element is absent**: Verifies that
`GetExample` returns `null` when the member does not exist in the XML doc file.
This scenario is tested by `XmlDocReader_GetExample_MemberAbsent_ReturnsNull`.

**GetExampleParts returns whole text as a code part when no code element exists**:
Verifies that when the `<example>` element has no `<code>` children, the entire
text is returned as a single code part. This scenario is tested by
`XmlDocReader_GetExampleParts_NoCodeElement_ReturnsWholeTextAsCodePart`.

**GetExampleParts separates prose from code elements**: Verifies that when both
text nodes and `<code>` children are present, prose and code are returned as
separate parts in order. This scenario is tested by
`XmlDocReader_GetExampleParts_WithCodeElement_SeparatesProseFromCode`.

**GetExampleParts returns empty list for an absent member**: Verifies that an
absent member identifier returns an empty list rather than throwing. This scenario
is tested by `XmlDocReader_GetExampleParts_MemberAbsent_ReturnsEmpty`.

**GetRemarks renders a bullet list as dash items**: Verifies that a
`<list type="bullet">` in `<remarks>` renders each `<item>` as a `- {item}` line.
This scenario is tested by
`XmlDocReader_GetRemarks_BulletList_RendersDashItems`.

**GetRemarks renders a numbered list as ordered items**: Verifies that a
`<list type="number">` renders each `<item>` as a `1. {item}` line. This scenario
is tested by `XmlDocReader_GetRemarks_NumberList_RendersOrderedItems`.

**GetRemarks renders a table list as a Markdown table**: Verifies that a
`<list type="table">` renders a header row from the `<listheader>`, a `| --- | --- |`
separator row, and one row per `<item>`. This scenario is tested by
`XmlDocReader_GetRemarks_TableList_RendersMarkdownTable`.

**GetRemarks renders a term/description item as a bold term with an em dash**:
Verifies that a `<list>` `<item>` carrying both `<term>` and `<description>` renders
as `**term** — description`. This scenario is tested by
`XmlDocReader_GetRemarks_ListItemWithTermAndDescription_RendersBoldTermDashDescription`.

**GetRemarks preserves inline rendering inside list items**: Verifies that a nested
`<c>` element inside a `<list>` item renders as an inline code span via the shared
dispatch. This scenario is tested by
`XmlDocReader_GetRemarks_ListItemWithInlineCode_PreservesInlineRendering`.

**GetRemarks keeps blank-line separation around a list**: Verifies that a `<list>`
surrounded by prose is separated by a blank line on each side so the Markdown list
renders correctly. This scenario is tested by
`XmlDocReader_GetRemarks_ListSurroundedByProse_KeepsBlankLineSeparation`.

**GetRemarks escapes pipe characters in table list cells**: Verifies that a
`<list type="table">` whose `<term>`/`<description>` content contains a literal pipe
character (for example a `<c>Flags.A | Flags.B</c>` code span) renders the cell with the
pipe escaped as `\|`, keeping the Markdown table row well-formed with exactly two
columns. This scenario is tested by
`XmlDocReader_GetRemarks_TableListCellWithPipe_EscapesPipe`.

**GetRemarks renders a nested list inline without broken Markdown**: Verifies that a
`<list>` nested inside an `<item>`/`<description>` degrades to readable single-line inline
text (a documented limitation, since a Markdown list item or table cell cannot contain a
block-level nested list) rather than emitting a stray newline that would break the
surrounding list item. This scenario is tested by
`XmlDocReader_GetRemarks_NestedListInsideItem_RendersInlineWithoutBrokenMarkdown`.

**GetExampleParts strips common indent from uniformly indented multi-line code**:
Verifies that when all content lines in a `<code>` block carry the same number of
leading spaces (from XML formatting), all of those spaces are stripped uniformly
so the output is flush-left. This scenario is tested by
`XmlDocReader_GetExampleParts_MultiLineCodeUniformIndent_StripsCommonIndent`.

**GetExampleParts strips common indent and preserves relative indentation in mixed-indent code**:
Verifies that when a `<code>` block contains lines with varying indentation (e.g. a
base of 8 spaces plus 4 more for inner blocks), the common 8-space prefix is stripped
and the 4-space relative indentation is preserved. This scenario is tested by
`XmlDocReader_GetExampleParts_MultiLineCodeMixedIndent_StripsCommonIndentPreservesRelative`.

**GetExampleParts single-line code has no regression**: Verifies that a single-line
`<code>` element with no leading whitespace is returned unchanged. This scenario is
tested by `XmlDocReader_GetExampleParts_SingleLineCode_NoRegression`.

**GetExampleParts preserves blank lines within code blocks**: Verifies that blank
lines inside a `<code>` block are preserved in the output and that they do not
contribute to the minimum-indentation calculation. This scenario is tested by
`XmlDocReader_GetExampleParts_CodeWithBlankLinesInMiddle_PreservesBlankLines`.

**GetExampleParts strips common indent from no-code-children fallback path**:
Verifies that when the `<example>` element has no `<code>` children and the entire
value is treated as a single code block, the same `DedentCode` logic is applied so
indented content is returned flush-left. This scenario is tested by
`XmlDocReader_GetExampleParts_NoCodeElement_IndentedContent_StripsCommonIndent`.

**GetSummary follows a cref inheritdoc reference**: Verifies that
`<inheritdoc cref="M:Target" />` causes the lookup to read the summary from the
named target member. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocWithCref_ReturnsSummaryFromTarget`.

**GetRemarks follows a cref inheritdoc reference**: Verifies that remarks
propagate from the cref target. This scenario is tested by
`XmlDocReader_GetRemarks_InheritDocWithCref_ReturnsRemarksFromTarget`.

**GetParams follows a cref inheritdoc reference**: Verifies that parameter
descriptions propagate from the cref target. This scenario is tested by
`XmlDocReader_GetParams_InheritDocWithCref_ReturnsParamsFromTarget`.

**GetReturns follows a cref inheritdoc reference**: Verifies that the returns
text propagates from the cref target. This scenario is tested by
`XmlDocReader_GetReturns_InheritDocWithCref_ReturnsReturnsFromTarget`.

**GetSummary returns null for a missing cref target**: Verifies that a cref
pointing to an absent member degrades to `null`. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocWithCref_MissingTarget_ReturnsNull`.

**GetSummary returns null on a cyclic cref chain**: Verifies that a cycle in
the `<inheritdoc cref="..." />` graph is detected and resolved to `null`
without throwing a stack overflow or infinite loop. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocWithCref_CyclicReference_ReturnsNull`.

**GetSummary applies a path XPath filter**: Verifies that `path="//summary"`
selects only the `<summary>` element from the resolved source and that
`<remarks>` or other sections are not included in the result. This scenario is
tested by
`XmlDocReader_GetSummary_InheritDocWithPath_ReturnsFilteredSummary`.

**GetSummary returns null for a non-matching path**: Verifies that an XPath
expression that matches nothing produces `null`. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocWithPath_NonMatchingPath_ReturnsNull`.

**GetSummary applies cref + path**: Verifies that a combination of an explicit
cref target and an XPath path filter returns only the filtered content from the
named target. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocWithCrefAndPath_ReturnsFilteredSummaryFromTarget`.

**GetSummary resolves bare inheritdoc using the injected chain**: Verifies that
a bare `<inheritdoc />` (no cref) follows the injected inheritance chain map to
return the base member's summary. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocBare_WithChain_ReturnsSummaryFromBase`.

**GetSummary returns null for bare inheritdoc without a chain**: Verifies that
bare `<inheritdoc />` degrades to `null` when no chain is supplied. This
scenario is tested by
`XmlDocReader_GetSummary_InheritDocBare_NoChain_ReturnsNull`.

**GetSummary returns null when the chain entry target is absent**: Verifies that
a chain entry pointing to a member not present in the XML doc file degrades to
`null`. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocBare_ChainMemberAbsent_ReturnsNull`.

**GetSummary resolves a multi-hop cref chain transitively**: Verifies that
A → B → C chains resolve C's summary for a query on A. This scenario is tested
by `XmlDocReader_GetSummary_InheritDocChained_ResolvesTransitively`.

**GetExceptions follows a cref inheritdoc reference**: Verifies that exception
cref values are inherited from the explicitly named cref target member. This
scenario is tested by
`XmlDocReader_GetExceptions_InheritDocWithCref_ReturnsExceptionsFromTarget`.

**GetExceptionDetails follows a cref inheritdoc reference**: Verifies that
exception type names and descriptions are inherited from the explicitly named
cref target member. This scenario is tested by
`XmlDocReader_GetExceptionDetails_InheritDocWithCref_ReturnsExceptionDetailsFromTarget`.

**GetExample follows a cref inheritdoc reference**: Verifies that example text
is inherited from the explicitly named cref target member. This scenario is
tested by `XmlDocReader_GetExample_InheritDocWithCref_ReturnsExampleFromTarget`.

**GetExampleParts follows a cref inheritdoc reference**: Verifies that structured
example parts (prose and code blocks) are inherited from the explicitly named
cref target member. This scenario is tested by
`XmlDocReader_GetExampleParts_InheritDocWithCref_ReturnsExamplePartsFromTarget`.

**Branch-local visited set prevents first candidate from blocking second candidate**:
Regression test for the branch-local visited-set fix. Verifies that when a bare
`<inheritdoc />` has multiple chain candidates and the first candidate traverses a
shared ancestor but fails (via a non-matching `path` filter), the second candidate
can independently resolve that same ancestor and return its documentation. Without
the branch-local fix, the shared ancestor would be marked as visited during the
first candidate's traversal, causing the second candidate to be blocked by the cycle
guard and returning `null` instead of the correct summary. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocBare_MultipleChainCandidates_SecondCandidateNotBlockedByFirstsVisited`.

**GetSummary resolves a bare inheritdoc chain candidate via the external member
lookup delegate**: Verifies that when a bare `<inheritdoc />` chain candidate is
absent from the local index, the injected `externalMemberLookup` delegate is
consulted and its result used, proving the fallback path added for cross-assembly
resolution. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocBare_ChainCandidateExternal_ResolvesFromExternalResolver`.

**GetSummary resolves an explicit cref target via the external member lookup
delegate**: Verifies that an explicit `<inheritdoc cref="..." />` target absent
locally resolves via the external member lookup delegate. This scenario is
tested by `XmlDocReader_GetSummary_InheritDocWithCref_ExternalTarget_ResolvesFromExternalResolver`.

**GetSummary applies the path filter to externally-resolved content**: Verifies
that `cref` + `path` selects only the filtered section from a target resolved
via the external member lookup delegate, exactly as it does for locally-resolved
content. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocWithCrefAndPath_ExternalTarget_AppliesPathFilter`.

**GetSummary returns null when both local and external candidates are absent**:
Verifies that a bare `<inheritdoc />` chain candidate missing from both the local
index and the external member lookup delegate degrades gracefully to `null`
without throwing. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocBare_ExternalCandidateAlsoMissing_ReturnsNull`.

**GetSummary detects a cycle that crosses the local/external boundary**: Verifies
that a local member's bare `<inheritdoc />` resolved externally to a member whose
own `cref` points back to the original local member is caught by the shared
`visited` set and returns `null` rather than looping indefinitely. This scenario
is tested by `XmlDocReader_GetSummary_InheritDocChain_CrossesLocalAndExternalBoundary_CycleDetected`.

**GetSummary preserves prior behavior when no external resolver is configured**:
Regression test confirming that the 2-arg constructor overload (no
`externalMemberLookup`) behaves identically to before this feature was added —
a local miss still returns `null` with no external fallback attempted. This
scenario is tested by
`XmlDocReader_GetSummary_NoExternalResolverConfigured_LocalMiss_ReturnsNullUnchanged`.

**GetSummary does not consult the external lookup for a top-level, undocumented
local member**: The external member lookup delegate is only ever consulted
while resolving an `<inheritdoc />` target (i.e. from within
`ResolveInheritdocSource`'s recursive calls). A member with no local `<member>`
entry at all — and therefore no `<inheritdoc />` element to resolve — must
return `null` from a top-level call such as `GetSummary`, even when the
external member lookup delegate has a colliding entry with a summary for that
exact member ID. This scenario is tested by
`XmlDocReader_GetSummary_LocalMemberUndocumented_ExternalLookupNotConsulted_ReturnsNull`.

**GetSummaryMarkdown returns trimmed plain text unchanged**: Verifies that for
a `<summary>` with no `<list>` content, `GetSummaryMarkdown` behaves exactly
like `GetSummary`, trimming surrounding whitespace and returning the text
unchanged. This scenario is tested by
`XmlDocReader_GetSummaryMarkdown_MemberPresent_ReturnsTrimmedText`.

**GetSummaryMarkdown returns null for an absent member**: Verifies that
`GetSummaryMarkdown` returns `null`, matching `GetSummary`, when the requested
member ID has no corresponding entry in the XML doc file. This scenario is
tested by `XmlDocReader_GetSummaryMarkdown_MemberAbsent_ReturnsNull`.

**GetSummaryMarkdown resolves `<inheritdoc cref="..."/>`**: Verifies that
`GetSummaryMarkdown` follows an explicit `cref` inheritdoc reference and
returns the summary from the referenced target member, matching `GetSummary`'s
inheritdoc-resolution behavior. This scenario is tested by
`XmlDocReader_GetSummaryMarkdown_InheritDocWithCref_ReturnsSummaryFromTarget`.

**GetSummaryMarkdown renders a numbered-list summary as multi-line ordered
Markdown**: Verifies that a `<list type="number">` inside a `<summary>` element
renders as real multi-line Markdown — a blank line before the list, each `1.`
item on its own line, and any trailing prose following `</list>` as a separate
paragraph — rather than the single-line collapse previously applied to all
summaries. This scenario is tested by
`XmlDocReader_GetSummaryMarkdown_NumberList_RendersMultiLineOrderedItems`.

**GetSummary renders a numbered-list summary as single-line inline markers**:
Verifies that the same `<list type="number">` summary, when rendered via the
single-line `GetSummary` path (used for table cells and other single-line
contexts), collapses the list to inline numbered markers
`(1) item one (2) item two (3) item three` joined with a space, instead of
silently concatenating item text with no separation. This scenario is tested by
`XmlDocReader_GetSummary_NumberList_RendersInlineNumberedMarkers`.

**GetSummaryMarkdown renders a bullet-list summary as multi-line dash items**:
Verifies the multi-line rendering path for `<list type="bullet">` inside a
`<summary>`, mirroring the number-list behavior. This scenario is tested by
`XmlDocReader_GetSummaryMarkdown_BulletList_RendersMultiLineDashItems`.

**GetSummary renders a bullet-list summary as single-line inline markers**:
Verifies the single-line inline-marker rendering for `<list type="bullet">`
inside a `<summary>`. This scenario is tested by
`XmlDocReader_GetSummary_BulletList_RendersInlineNumberedMarkers`.

**GetSummaryMarkdown renders a table-list summary as a multi-line Markdown
table**: Verifies the multi-line rendering path for `<list type="table">`
inside a `<summary>`, producing a real Markdown pipe table with header and
separator rows. This scenario is tested by
`XmlDocReader_GetSummaryMarkdown_TableList_RendersMultiLineTable`.

**GetSummary renders a table-list summary as single-line inline markers**:
Verifies the single-line inline-marker rendering for `<list type="table">`
inside a `<summary>`, confirming all three list variants (number, bullet,
table) are handled consistently for both rendering modes, and that the
`<listheader>` column labels are preserved rather than silently dropped. The
header is asserted against the exact expected output
(`**Name** — **Detail** (1) **Alpha** — First. (2) **Beta** — Second.`) to
guard against the header being double-bolded (for example `****Name** —
Detail**`) by a caller wrapping an already-bolded header a second time. This
scenario is tested by `XmlDocReader_GetSummary_TableList_RendersInlineNumberedMarkers`.

**GetSummary renders a property cref as Type.Member**: Verifies that
`<see cref="P:Namespace.Type.PropertyName" />` now always includes the
declaring type name, rendering as `` `Type.PropertyName` `` wrapped in an
inline code span rather than the bare `PropertyName`. This scenario is tested
by `XmlDocReader_GetSummary_SeeCrefToProperty_RendersTypeDotMember`.

**GetSummary renders a field cref as Type.Member**: Verifies the same fix for
`<see cref="F:..." />` field references. This scenario is tested by
`XmlDocReader_GetSummary_SeeCrefToField_RendersTypeDotMember`.

**GetSummary renders an event cref as Type.Member**: Verifies the same fix for
`<see cref="E:..." />` event references. This scenario is tested by
`XmlDocReader_GetSummary_SeeCrefToEvent_RendersTypeDotMember`.

**GetSummary continues to render a method cref as Type.Member (regression
guard)**: Verifies that `<see cref="M:..." />` method references — which
already included the type name before this fix — remain unaffected by it, and
are additionally now wrapped in an inline code span along with the other
member kinds. This scenario is tested by
`XmlDocReader_GetSummary_SeeCrefToMethod_StillRendersTypeDotMember`.

**GetSummary wraps a type-only (`T:`) cref in a code span**: Verifies that a
`<see cref="T:..." />` reference to a type with an arity marker — rather than a type member —
renders with raw, unescaped angle brackets inside a backtick code span, matching the
generic-type-parameter placeholder handling used for the member-cref case. This scenario is
tested by `XmlDocReader_GetSummary_WithSeeGenericTypeCref_FormatsWithTypeParameters`.

**GetSummary wraps a plain type-only cref in a code span (mandatory regression guard)**:
Verifies that `<see cref="T:Foo.ArgumentValidator"/>`, with no explicit display text, is wrapped
in an inline code span the same as a member cref — fixing a reported inconsistency where a
type reference rendered as plain prose right next to a member reference rendered as code. This
scenario is tested by `XmlDocReader_GetSummary_SeeCrefToTypeOnly_RendersAsInlineCodeSpan`.

**GetSummary renders a constructor (`#ctor`) cref unwrapped as the bare type name**: Verifies
that `<see cref="M:Type.#ctor" />` — a method-kind cref whose member name is the special
`#ctor` marker — renders as the declaring type name alone, with no inline code span, matching
the constructor-collapsing behavior documented on `FormatMemberReference`. This scenario is
tested by `XmlDocReader_GetSummary_SeeCrefToConstructor_RendersUnwrappedTypeName`.

**GetSummary tolerates an empty-member-target cref without throwing (mandatory
regression guard)**: Verifies that `<see cref="M:"/>` — a member-kind cref
prefix with no target name, yielding empty formatted display text that is
still classified as a member reference — is rendered as an empty string
instead of throwing `IndexOutOfRangeException` when the (would-be) inline
code-span wrapping is attempted on empty content. This scenario is tested by
`XmlDocReader_GetSummary_SeeCrefWithEmptyMemberTarget_DoesNotThrowAndRendersEmpty`.

**GetSummary renders a member cref on a generic type with unescaped angle
brackets inside its code span**: Verifies that
`` `<see cref="M:...List`1.Add(`0)" />` `` — a member cref whose declaring
type is generic — renders as `` `List<T>.Add()` `` with raw, unescaped angle
brackets, not the backslash-escaped prose form (`List\<T\>.Add()`), since the
result is wrapped in a code span and a code span's content is literal. This
scenario is tested by
`XmlDocReader_GetSummary_SeeCrefToMemberOnGenericType_RendersUnescapedAngleBracketsInCodeSpan`.

**GetSummary renders a type-only cref to a generic type with unescaped angle brackets inside its
code span**: Verifies that `` `<see cref="T:...List`1" />` `` — a type-only cref — is wrapped the
same as a member cref, rendering `` `List<T>` `` with raw, unescaped angle brackets rather than
the backslash-escaped prose form `List\<T\>`. This scenario is tested by
`XmlDocReader_GetSummary_SeeCrefToGenericTypeOnly_RendersUnescapedAngleBracketsInCodeSpan`.

**GetRemarks renders a `<br/>` element as a paragraph break**: Verifies that
`<br/>` inserts a blank-line paragraph break between the surrounding text
rather than being silently dropped. This scenario is tested by
`XmlDocReader_GetRemarks_BrElement_InsertsParagraphBreak`.

**GetSummary collapses a `<br/>` element to a single space**: Verifies that in
the single-line summary context, `<br/>` degrades to a plain space separator
with no residual markup or newline. This scenario is tested by
`XmlDocReader_GetSummary_BrElement_CollapsesToSingleSpace`.

**GetRemarks renders a single-line `<code>` element as an inline backtick
span**: Verifies that `<code>` content with no embedded newline renders as an
inline code span, not a fenced block. This scenario is tested by
`XmlDocReader_GetRemarks_SingleLineCodeElement_RendersAsInlineBacktickSpan`.

**GetRemarks renders a multi-line `<code>` element as a fenced block**:
Verifies that multi-line `<code>` content renders as a fenced Markdown code
block with its own blank-line separators from surrounding prose. This scenario
is tested by `XmlDocReader_GetRemarks_MultiLineCodeElement_RendersAsFencedBlock`.

**Fenced `<code>` block preserves internal whitespace exactly (mandatory
regression guard)**: Verifies that multiple consecutive spaces and a tab
inside a fenced code block survive `GetRemarks`'s whitespace-collapsing
normalization byte-for-byte, proving the placeholder-token protection
mechanism works correctly. This scenario is tested by
`XmlDocReader_GetRemarks_MultiLineCodeElementWithInternalMultipleSpacesAndTabs_PreservesExactWhitespace`.

**GetRemarks emits nothing for an empty or whitespace-only `<code>` element**:
Verifies the empty-skip guard mirrors the existing `<c>` behavior. This
scenario is tested by
`XmlDocReader_GetRemarks_EmptyOrWhitespaceOnlyCodeElement_EmitsNothing`.

**GetSummary flattens a multi-line `<code>` element to an inline span**:
Verifies that a fenced block is never produced in the single-line summary
context; embedded newlines are flattened to spaces inside the code span
instead. This scenario is tested by
`XmlDocReader_GetSummary_MultiLineCodeElement_FlattensToInlineBacktickSpanNotFencedBlock`.

**Single-line code flattening preserves each line's internal whitespace
(mandatory regression guard)**: Verifies that when a multi-line `<code>`
element is flattened into a single-line inline code span (as happens in a
`<summary>`), a line's significant internal whitespace — multiple consecutive
spaces, an embedded tab — survives the flattening; only the line boundary
itself collapses to a single joining space, rather than the whole result
being run through the prose-oriented whitespace-collapsing normalization used
elsewhere. This scenario is tested by
`XmlDocReader_GetSummary_MultiLineCodeElementWithInternalMultipleSpacesAndTabs_PreservesInternalWhitespace`.

**Fenced `<code>` block with an embedded backtick run uses a longer fence**:
Verifies that content containing a 3-backtick run causes the fence to widen to
4 backticks so the delimiter remains unambiguous. This scenario is tested by
`XmlDocReader_GetRemarks_CodeElementContainingBacktickRun_UsesLongerFence`.

**End-to-end `<br/>`/`<code>` reproduction renders as distinct blocks**:
Verifies the exact field-reported symptom (a generated-regex-example remarks
block mixing `<br/>` and `<code>`) no longer run "Pattern:"/"Explanation:"
together, correctly dispatches the single-line code to an inline span and the
multi-line code (containing a nested, content-less `<br/>`) to a fenced block,
and produces no blank-line run longer than one line. This scenario is tested
by `XmlDocReader_GetRemarks_BrAndCodeFromGeneratedRegexExample_RendersAsDistinctBlocks`.

**At most one blank line between a `<list>` and an immediately following
`<code>` block**: Verifies that two independently blank-line-wrapped block
separators do not compound into a 2+ blank-line run (markdownlint MD012).
This scenario is tested by
`XmlDocReader_GetRemarks_ListImmediatelyFollowedByCode_AtMostOneBlankLineBetween`.

**Multiple consecutive `<br/>` tags collapse to at most one blank line**:
Verifies that three or more consecutive `<br/>` tags never produce more than
one blank-line paragraph break. This scenario is tested by
`XmlDocReader_GetRemarks_MultipleConsecutiveBrTags_AtMostOneBlankLineBetween`.

**Nested `<code>` inside a `<list>` item's `<description>` renders inline, not
fenced**: Verifies that the fenced-vs-inline dispatch is correctly gated on
the `codeBlocks` placeholder map being supplied, not on the `singleLine` flag,
by proving a multi-line `<code>` nested in a list item renders inline and
coexists with the pre-existing nested-list inline-degradation behavior. This
scenario is tested by
`XmlDocReader_GetRemarks_NestedCodeInsideListItemDescription_RendersAsInlineSpanNotFencedBlock`.

**`<code>` reached through `<example>` mixed-prose accumulation renders
inline, not fenced**: Verifies that a multi-line `<code>` nested inside a
`<para>` within `<example>` mixed prose (not a direct `<example>`-child
`<code>`, which bypasses this dispatch entirely) falls back to an inline span
rather than a mangled fenced block, since the example-prose pipeline never
supplies a `codeBlocks` placeholder map. This scenario is tested by
`XmlDocReader_GetExampleParts_WithMixedInlineElementsContainingMultiLineCodeOutsideCodeTag_RendersAsInlineSpan`.

**No linkContext renders a resolvable cref exactly as before linking existed**:
Verifies that calling `GetSummary` without a `linkContext` argument (the
pre-existing 1-arg call shape used throughout the rest of this file, and by
every single-file-emitter call site) continues to render a `<see cref>` to a
type that would in fact be resolvable and emitted, as plain code-span text
with no link — proving the optional parameter is purely additive and changes
no existing behavior by default. This scenario is tested by
`XmlDocReader_GetSummary_NullLinkContext_RendersCodeSpanOnlyNoLink`.

**A resolved, emitted intra-assembly type renders as a linked code span**:
Verifies that `GetSummary`, given a `linkContext` whose `Targets` resolves the
cref to a `TypeDefinition` and whose `IsTypeEmitted` delegate reports `true`
for it, renders `` `[Type](path.md)` `` — a Markdown link nested inside the
code span — rather than plain code-span text. This scenario is tested by
`XmlDocReader_GetSummary_ResolvedEmittedType_RendersLinkedCodeSpan`.

**A resolved, emitted intra-assembly member renders as a linked code span**:
Verifies the member counterpart of the above: `GetSummary`, given a
`linkContext` whose `Targets` resolves the cref to an `IMemberDefinition` and
whose `IsMemberEmitted` delegate reports `true` for it, and whose
`MemberPageIndex` contains an entry for that member, renders a Markdown link
nested inside the code span. This scenario is tested by
`XmlDocReader_GetSummary_ResolvedEmittedMember_RendersLinkedCodeSpan`.

**An external type cref falls back to code-span-only even with a non-null
linkContext**: Verifies that a cref pointing at a type not indexed by
`Targets` (e.g. a framework type outside the documented assembly) renders
plain code-span text with no link, exactly as it did before this feature
existed, even though `linkContext` is non-null — proving the fallback is keyed
on resolution success, not merely on `linkContext`'s presence. This scenario
is tested by `XmlDocReader_GetSummary_ExternalTypeCref_FallsBackToCodeSpanOnly`.

**A malformed cref falls back unchanged**: Verifies that a cref string that
does not match any indexed type or member identifier renders plain code-span
text with no link, matching today's fallback for unresolvable crefs. This
scenario is tested by `XmlDocReader_GetSummary_MalformedCref_FallsBackUnchanged`.

**A constructor cref falls back unchanged (never linked)**: Verifies that a
constructor cref renders exactly as it did before this feature existed — the
bare declaring type name, with no inline code span and no link — even with a
non-null `linkContext` that would otherwise resolve the declaring type,
confirming constructors are deliberately excluded from linking because
`FormatCref` already reports `ShouldWrapInCodeSpan: false` for them, so
`TryLinkifyCref` is never reached. This scenario is tested by
`XmlDocReader_GetSummary_ConstructorCref_FallsBackUnchanged`.

**A `<seealso cref>` to a resolved, emitted type renders as a linked code
span**: Verifies that `<seealso>` (nested inside `<remarks>` text, the only
place it is ever rendered — see "Top-level `<seealso>` is never rendered" in
the technical notes) shares the exact same `TryLinkifyCref` rendering path as
`<see>`, producing a Markdown link nested inside the code span when its
target resolves and is reported as emitted. This scenario is tested by
`XmlDocReader_GetRemarks_SeeAlsoResolvedEmittedType_RendersLinkedCodeSpan`.
