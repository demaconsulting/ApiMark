## XmlDocReader

![XmlDocReader Structure](ApiMarkDotNetView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

XmlDocReader reads and indexes a .NET XML documentation file for fast
member-level lookups. It builds a `Dictionary<string, XElement>` keyed by XML
doc member identifier on construction, enabling O(1) lookups for summary,
remarks, params, returns, exceptions, and example content during emit.
It also resolves `<inheritdoc />` references — bare, `cref`-attributed, and
`path`-filtered — by following the reference chain recursively with cycle
detection.

### Data Model

**_members** (private `Dictionary<string, XElement>`): Index of documentation
members keyed by their XML doc identifier string (e.g.
`T:MyNamespace.MyClass`, `M:MyNamespace.MyClass.Method(System.Int32)`). Built
once on construction; read-only thereafter.

**_inheritanceChain** (private `IReadOnlyDictionary<string, IReadOnlyList<string>>?`):
Optional map from derived member ID to an ordered list of candidate base member
IDs. Supplied by `DotNetGenerator` from Mono.Cecil metadata. Used to resolve
bare `<inheritdoc />` elements that carry no `cref` attribute. When `null`,
bare inheritdoc resolution returns `null` or empty rather than throwing.

**_externalMemberLookup** (private `Func<string, XElement?>?`): Optional
fallback delegate consulted when a member ID is not present in `_members`.
Typically backed by `ExternalXmlDocResolver.TryGetMember`, supplied by
`DotNetGenerator` when `DotNetGeneratorOptions.ReferencePaths` is non-empty.
Enables `<inheritdoc />` resolution to reach base types/members defined in
externally referenced assemblies (e.g. NuGet package dependencies). When
`null` (the default), behavior is identical to before this fallback existed.

**Duplicate-key policy**: When duplicate member names appear in the XML doc
file, the first occurrence is used and subsequent duplicates are silently
discarded. This is a defensive policy for malformed but real-world XML doc
files where the compiler emits the same member ID more than once (e.g., due to
partial-class splits or tooling bugs).

**CrefLinkContext** (external `internal sealed record`, defined in
`CrefLinkContext.cs`, not a field of `XmlDocReader` itself): Optional
cross-reference linking context, threaded as a trailing nullable parameter
through new internal overloads of the rendering methods, reached internally
from every public rendering method — see "Cross-reference linking
(CrefLinkContext)" below for its full field list and semantics.

### Key Methods

**XmlDocReader constructor**: Parses the XML documentation file and builds the
member index.

- *Parameters*: `string xmlDocPath` — path to the XML documentation file;
  `IReadOnlyDictionary<string, IReadOnlyList<string>>? inheritanceChain` —
  optional bare inheritdoc resolution map (defaults to `null`);
  `Func<string, XElement?>? externalMemberLookup` — optional external member
  lookup fallback for cross-assembly `<inheritdoc />` resolution (defaults to
  `null`).
- *Preconditions*: `xmlDocPath` must exist on disk.
- *Postconditions*: `_members` is populated; all lookups are O(1).
- *Exceptions*: Throws `FileNotFoundException` when `xmlDocPath` does not exist.

**GetSummary**: Returns trimmed single-line summary text for `memberId`, or
`null` if absent. Summary text is normalized to a single line because summaries
are by convention brief one-liner descriptions, and this method feeds compact
contexts such as table cells and bullet quick-index lines. Resolves
`<inheritdoc />` first. When the summary contains a `<list>` element (bullet,
number, or table), the list is rendered inline using single-line numbered
markers (`(1) item one (2) item two`) via `AppendInlineMarkerList` rather than
losing item separation — see "Single-line vs. multi-line list rendering" below.

**GetSummaryMarkdown**: Returns the full multi-line Markdown rendering of the
`<summary>` element for `memberId`, or `null` if absent. Unlike `GetSummary`,
this preserves real block structure: a `<list>` element renders as an actual
multi-line Markdown list/table (blank line before the list, each item on its
own line, numbered markers for `type="number"`), with any trailing prose after
`</list>` rendered as its own paragraph — exactly the same multi-line rendering
already used by `GetRemarks`. Intended for member-detail-page and type-page
bodies, which have room for full Markdown, as opposed to `GetSummary`'s
single-line contract for table cells. Resolves `<inheritdoc />` first.

**GetRemarks**: Returns trimmed remarks text for `memberId`, or `null` if
absent. May contain multiple lines. Resolves `<inheritdoc />` first.

**GetParams**: Returns parameter names and descriptions for `memberId` as
`IReadOnlyList<(string Name, string? Description)>`. Returns an empty list
when the member is absent. `<param>` elements without a `name` attribute are
silently filtered out. Resolves `<inheritdoc />` first. Description text is
rendered via `GetSingleLineDocumentationText`, not `GetDocumentationText`: every
caller writes the description into a raw pipe-delimited Markdown table cell
(`FileMarkdownWriter.WriteTable` only escapes `|`, never embedded `\n`), so a
`<br/>`/multi-line `<code>` in the description must flatten to a single line
with no embedded literal newline rather than render as a fenced code block,
which would otherwise corrupt the table.

**GetReturns**: Returns trimmed returns text for `memberId`, or `null` if
absent. Resolves `<inheritdoc />` first.

**GetExceptions**: Returns all `cref` attribute values from `<exception>`
elements for `memberId` as `IReadOnlyList<string>`. Returns an empty list when
the member is absent. Resolves `<inheritdoc />` first.

**GetExceptionDetails**: Returns exception types and descriptions from
`<exception>` elements for `memberId` as
`IReadOnlyList<(string Type, string? Description)>`. The `Type` field is
the formatted cref value (applying `FormatCref` to strip the type prefix and
format names); entries with an empty cref are filtered out. Resolves
`<inheritdoc />` first. Like `GetParams`, the `Description` field is rendered
via `GetSingleLineDocumentationText` for the same table-cell safety reason —
every caller writes it into a Markdown table cell.

**GetExample**: Returns trimmed example text for `memberId`, or `null` when
the `<example>` element is absent or contains only whitespace. Resolves
`<inheritdoc />` first.

> **Note**: `GetExample` returns raw text content using `element?.Value.Trim()`
> rather than the inline element rendering pipeline. Inline elements such as
> `<see cref>`, `<c>`, and `<paramref>` within `<example>` are silently dropped by
> `GetExample`. Use `GetExampleParts` for full inline-element rendering.

**GetExampleParts**: Returns the structured example content for `memberId` as
`IReadOnlyList<(bool IsCode, string Content)>` parts. When the `<example>`
element contains no `<code>` children, the entire text is returned as a single
code part after applying `DedentCode`. When `<code>` children are present, text
nodes become prose parts and `<code>` elements become code parts with `DedentCode`
applied to each code block. Resolves `<inheritdoc />` first.

> **`<para>` flush behavior**: When a `<para>` element is encountered among the
> mixed-content children of `<example>`, its text is rendered into the prose
> accumulator and then the accumulator is immediately flushed as a distinct prose
> part. This ensures that each `<para>` produces its own separate prose part
> rather than merging with adjacent text content.

**ResolveMemberElement** (private): Resolves the effective `<member>` element
for a given ID by following `<inheritdoc />` recursively with cycle detection.

- Returns the member element directly when no `<inheritdoc />` child is present.
- When the member ID is absent from `_members`, falls back to
  `_externalMemberLookup` (if configured) ONLY when the caller passed
  `allowExternalLookup: true` — before treating the ID as unresolved; this
  fallback is scoped to `<inheritdoc />` target resolution only. Every
  top-level entry point (`GetSummary`, `GetRemarks`, etc.) calls this method
  with the default `allowExternalLookup: false`, so a member that is simply
  undocumented locally is never satisfied by an incidentally-colliding member
  ID in an externally referenced assembly's XML doc file. Only
  `ResolveInheritdocSource`'s two recursive call sites (the `cref` branch and
  the bare-inheritdoc chain-candidate loop) pass `allowExternalLookup: true`.
  Where the fallback IS consulted, it does not change any other resolution
  semantics (cycle detection, `path` filtering, and cref-then-chain priority
  ordering apply identically to externally-resolved members).
- When `cref` is present, resolves the named target recursively.
- When no `cref` is present, tries each candidate from `_inheritanceChain` in
  order, stopping at the first that yields a result. Each candidate is tried with
  a branch-local copy of the visited set. This ensures that a failed traversal
  through one candidate — which may visit shared ancestor nodes — does not prevent
  subsequent candidates from resolving through those same ancestors.
- When `path` is present (XPath expression), evaluates it against the resolved
  source element and wraps matching nodes in a synthetic `<member>` element.
- Maintains a `HashSet<string>` of visited IDs per resolution path to break
  cycles without throwing — this cycle detection covers chains that cross the
  local/external boundary, because every recursive call (whether the member was
  found locally or externally) funnels through the same `visited` set.
- *Known limitation*: `_inheritanceChain` is built only from the primary
  assembly's Cecil metadata, so it has no entry for any member that was itself
  resolved via `_externalMemberLookup` — regardless of which assembly that
  member actually lives in. A single bare `<inheritdoc />` hop from a
  primary-assembly member into an external member resolves correctly, but if
  that external member's own entry is itself a bare `<inheritdoc />` pointing
  at a further member, the chain lookup for its ID misses and resolution stops
  there — a *second bare-inheritdoc hop* after landing in an
  externally-resolved member is not supported. An explicit `cref` at each hop
  is unaffected, since `cref` targets recurse directly through
  `ResolveMemberElement` rather than through `_inheritanceChain`.

**Whitespace normalization**: `GetDocumentationText` normalizes text by
collapsing internal whitespace within each line. `GetSingleLineDocumentationText`
additionally joins all non-empty trimmed lines into a single space-separated
string. Both normalize line endings to `\n` before processing. Both pass an
explicit `singleLine` flag down through `AppendNodeText`/`AppendElementText`/
`AppendListText` so that list rendering (see "Single-line vs. multi-line list
rendering" below) can tell which of the two contexts it is being called from.
Both also create and supply their own `codeBlocks` placeholder map (see "Fenced
code block placeholder protection" below) — `GetSingleLineDocumentationText`
needs this to protect inline backtick-code-span content (and its internal
whitespace) from its own line-join/space-collapse pass, exactly as
`GetDocumentationText` already protects fenced-block content from its
per-line collapsing — so both entry points are callable interchangeably for
table-cell-bound text such as `GetParams`/`GetExceptionDetails` descriptions.
After per-line collapsing and trimming, `NormalizeDocumentationText` also
collapses any run of two or more consecutive blank lines down to a single
blank line, before its final `Trim()` call — this guards against markdownlint
MD012 violations that would otherwise appear where adjacent block-level
separators (a `<list>`, a `<br/>`, or a fenced `<code>` block) each
independently surround themselves with a blank line, e.g. a list immediately
followed by a fenced code block. `NormalizeSingleLine` is not changed by this,
since it already discards every blank line as part of joining non-empty lines
with spaces.

#### Single-line vs. multi-line list rendering

`AppendNodeText`, `AppendElementText`, and `AppendListText` each accept a
`bool singleLine` parameter that determines how a `<list>` element renders:

- `singleLine: false` (used by `GetDocumentationText`, i.e. `GetRemarks`,
  `GetExample`/`GetExampleParts`, and the new `GetSummaryMarkdown`) — renders
  real multi-line Markdown exactly as described in the `<list>` bullet under
  "Inline Element Rendering" below: a blank line before the list, each item on
  its own line (dash, numbered, or table-row), and any trailing prose as a
  separate paragraph.
- `singleLine: true` (used by `GetSingleLineDocumentationText`, i.e.
  `GetSummary`) — delegates to `AppendInlineMarkerList`, which renders every
  list `type` variant (bullet, number, table) uniformly as single-line numbered
  markers: `(1) item one (2) item two (3) item three`, joined with a single
  space. None of the three list types can express genuine block structure on
  one line, so numbering is used consistently across all of them purely to
  preserve a readable item boundary.

This flag threads through unchanged from the top-level entry point down to
every recursive call, with one exception: nested inline content inside a
`<term>`/`<description>` (used by `type="table"` list items, via
`RenderTermDescription`/`RenderInlineElement`) is always rendered through the
`singleLine: false` branch and then collapsed with `NormalizeSingleLine`,
regardless of which context invoked the outer list. This produces the same
visible single-line result as genuine flag propagation would, but the
`singleLine` value itself is not passed into that nested call.

#### Fenced code block placeholder protection (`codeBlocks`)

`AppendNodeText`/`AppendElementText` accept a fourth, optional
`Dictionary<string, string>? codeBlocks` parameter (default `null`) used to
protect a fenced Markdown code block's literal content — its backtick fences
and internal whitespace/newlines — from `NormalizeDocumentationText`'s
per-line `CollapseWhitespace` pass and its blank-line-run collapsing, both of
which would otherwise corrupt a fenced block if applied directly to it. The
same mechanism also protects an inline backtick code span produced for a
`<code>` element's internal whitespace (multiple spaces, tabs) from
`CollapseWhitespace`/`NormalizeSingleLine` when a `codeBlocks` map is
supplied, since a single-line `<code>` element can appear in either
rendering context. A `<c>` element's rendering is NOT protected by this
mechanism — it is always appended directly via `AppendMarkdownCodeSpan`
(see the `<c>` bullet under "Inline Element Rendering" below) regardless of
whether a `codeBlocks` map is supplied, so its internal whitespace remains
subject to the caller's outer whitespace-collapsing normalization.

- **Why it exists**: A multi-line `<code>` element renders as a fenced
  Markdown code block (see the `<code>` bullet under "Inline Element
  Rendering" below). Its content must reach the final output byte-for-byte —
  including runs of multiple spaces, tabs, and blank lines that are
  semantically meaningful inside the code — but it is accumulated into the
  same `StringBuilder` as surrounding prose, which later passes through
  `NormalizeDocumentationText`/`NormalizeSingleLine`. A single-line `<code>`
  element has the identical problem for its internal whitespace when rendered
  as an inline backtick span, so the same placeholder mechanism is reused
  rather than building a second one. Instead of special-casing the
  normalizer itself to skip these regions, the real content is registered
  under a unique, single-line placeholder token (`RegisterCodeBlockPlaceholder`)
  built from a non-whitespace control character (`\u0001`) plus a literal
  prefix and a running counter; only the token is appended to the builder.
  The token is immune to `CollapseWhitespace` (which only touches whitespace
  runs), immune to `Trim()` (it neither starts nor ends with whitespace), and
  occupies exactly one line (no embedded `\n`), so it passes through every
  stage of `NormalizeDocumentationText`/`NormalizeSingleLine` completely
  unchanged. `RestoreCodeBlockPlaceholders` substitutes the real content back
  in immediately after normalization completes, inside both
  `GetDocumentationText` and `GetSingleLineDocumentationText`.
- **Lifecycle**: A new, empty `codeBlocks` map is created once per
  `GetDocumentationText`/`GetSingleLineDocumentationText` call (the only two
  call sites that create one) and passed down through the full
  `AppendNodeText`/`AppendElementText` recursion for that single call; it is
  discarded once the call returns.
- **Why `RenderInlineElement` and `BuildMixedExampleParts` never supply one**:
  Both of these call sites render content that is subsequently squashed
  through `NormalizeSingleLine` rather than `NormalizeDocumentationText` — a
  fenced code block (literal triple-backtick fences and internal newlines)
  would never be valid in that single-line context, and a placeholder token
  surviving `NormalizeSingleLine`'s line-split/trim/space-join pass would
  itself be meaningless once restored into single-line text. Both call sites
  therefore rely on the default `codeBlocks: null`, which forces
  `AppendCodeElementText`'s fenced-vs-inline decision to always take the
  inline-span branch instead (see the `<code>` bullet below) — this is
  evaluated purely from `codeBlocks != null`, deliberately not from the
  `singleLine` flag, because both of these call sites pass `singleLine: false`
  into `AppendNodeText` even though their output is later squashed by
  `NormalizeSingleLine`, not `NormalizeDocumentationText`.
- **Known limitation — `RenderInlineElement` (`<list type="table">` cells) is
  not protected**: `RenderInlineElement` renders `<term>`/`<description>`
  content for `type="table"` list items — written into a literal
  pipe-delimited Markdown table cell by `AppendListText`, the same corruption
  hazard class that motivated routing `GetParams`/`GetExceptionDetails`
  through `GetSingleLineDocumentationText`'s protected path. Because
  `RenderInlineElement` calls its own unprotected `NormalizeSingleLine`
  directly rather than going through `GetSingleLineDocumentationText`, an
  inline `<code>` span's internal whitespace inside a table-list item's
  `<description>` still collapses. This is a pre-existing limitation, not a
  regression introduced by this fix, and is intentionally left out of scope
  here — unlike `GetParams`/`GetExceptionDetails`, it was not part of the
  reported defect and protecting it would require threading a `codeBlocks` map
  through `AppendListText`'s nested-list-item rendering as well.

#### Inline Element Rendering

`GetDocumentationText` processes inline XML elements within doc comment nodes
according to the following element-to-text mappings:

- `<c>text</c>` → CommonMark backtick code span; the fence length adapts to avoid
  embedded backticks in the content per CommonMark §6.1. When the code content starts
  or ends with a backtick, a single space is inserted on each side inside the fence so
  that Markdown parsers can unambiguously identify the fence delimiter (CommonMark §6.1).
- `<br/>` → an explicit paragraph break, rendered as `"\n\n"`. Runs of two or more
  consecutive `<br/>` tags are collapsed to a single blank line by
  `NormalizeDocumentationText`'s blank-line-run collapsing (see "Whitespace
  normalization" above), so authoring several `<br/>` tags in a row never produces
  more than one blank Markdown paragraph break. In a single-line context
  (`GetSummary`), the emitted blank line is discarded along with every other blank
  line by `NormalizeSingleLine`, collapsing the `<br/>` to a single space instead.
- `<code>text</code>` → dedented via the existing `DedentCode` helper, then rendered
  as either an inline backtick code span or a fenced Markdown code block, decided by
  `AppendCodeElementText`:
  - Whitespace-only content (after dedenting) contributes nothing, mirroring the
    `<c>` empty-skip guard above.
  - A fenced block (`` ``` `` fences on their own lines, surrounded by `"\n\n"`) is
    produced only when a `codeBlocks` placeholder map was supplied (i.e. only from
    `GetDocumentationText`'s top-level call — see "Fenced code block placeholder
    protection" above) and the dedented content spans multiple lines. The fence
    length is `Math.Max(3, ComputeFenceLength(dedented))` backticks, so an embedded
    backtick run of 3 or more inside the code still cannot be confused with the
    fence delimiter.
  - Every other case (single-line content, no `codeBlocks` map supplied, as in
    nested list items/`<example>` mixed prose) renders an inline backtick code
    span via `AppendMarkdownCodeSpan` instead, flattening any embedded newlines
    through `NormalizeSingleLine` first. When a `codeBlocks` placeholder map
    IS supplied (from `GetDocumentationText` or `GetSingleLineDocumentationText`
    — including `GetSummary`), the inline span is registered as a placeholder
    token the same way a fenced block is, so its internal whitespace survives
    `CollapseWhitespace`/`NormalizeSingleLine` unchanged (see "Fenced code
    block placeholder protection" above) — this is what keeps `GetParams`/
    `GetExceptionDetails` descriptions (always written into a Markdown table
    cell, so they must use `GetSingleLineDocumentationText`, never a fenced
    block) safe for significant internal spacing.
- `<see cref="..."/>` → formatted cref value via the `FormatCref` helper (strips the
  type-kind prefix, strips the namespace path to leave just the type name, and replaces
  generic arity markers with angle-bracket type-parameter placeholders via
  `FormatTypeArity`, e.g. `List\`1` → `List<T>`,`Dictionary\`2` → `Dictionary<T1, T2>`).
  Unless the cref is a constructor (`#ctor`), the formatted text is additionally
  wrapped in an inline Markdown code span — this applies to type-only crefs as well
  as crefs to a type member (property, field, event, or non-constructor method); see
  "Member and type-only cref inline code span wrapping" below. In that case the
  generic arity placeholder is rendered with raw, unescaped angle brackets
  (`List<T>`) rather than the backslash-escaped prose form (`List\<T\>`), since a
  code span's content is literal and the escaping backslash would otherwise show up
  as a stray visible character — see "Generic cref escaping inside a code span" below.
- `<see langword="..."/>` → the `langword` attribute value directly (e.g., `null`,
  `true`, `false`).
- `<paramref name="..."/>` and `<typeparamref name="..."/>` → the `name` attribute
  value directly.
- Consecutive non-`<code>` child nodes in `<example>` → accumulated into a single
  prose text part; `<code>` child nodes → separate code parts (dedented via
  `DedentCode`).
- `<list>` → a Markdown list or table rendered by `AppendListText`, dispatched on the
  `type` attribute (`bullet`, `number`, or `table`; absent/unknown defaults to
  `bullet`). The block is wrapped in blank lines (`"\n\n"` prepended and appended) so
  that, after `NormalizeDocumentationText` trims the string boundaries and
  `FileMarkdownWriter.WriteParagraph` writes it verbatim, the list is separated from
  surrounding prose and renders as valid CommonMark:
  - `type="bullet"` → one `- {item}` line per `<item>` (dash bullet style).
  - `type="number"` → one `1. {item}` line per `<item>` (repeated `1.`, which the
    default markdownlint ordered-list style accepts).
  - `type="table"` → a Markdown pipe table: a header row from the `<listheader>`
    `<term>`/`<description>` when present (else `Term | Description`), a `| --- | --- |`
    separator row, and one `| {term} | {description} |` row per `<item>`.
  - `<item>` content: `<term>` and `<description>` are rendered to single-line text via
    the shared inline dispatch; when both are present the item renders as
    `**{term}** — {description}` (em dash), otherwise whichever is present, and a bare
    `<item>` with no `<term>`/`<description>` contributes its inline content directly.
  - `<listheader>` on a bullet/number list is emitted as a leading bold line before the
    items; on a table it supplies the column header text.
  - Nested inline elements inside `<term>`/`<description>`/`<item>` (such as `<c>`,
    `<see>`, `<paramref>`) render through the same `AppendNodeText` dispatch. Only
    top-level bullet, number, and table lists produce block-level Markdown; a `<list>`
    nested inside an `<item>`/`<description>` is collapsed to single-line inline text
    (its item text is preserved and joined with spaces) rather than rendered as a
    block-level nested list, because a Markdown table cell or single-line list item
    cannot contain a block-level list. This inline degradation keeps the surrounding
    table or list well-formed (a documented limitation).

**FormatCref** (private static): Converts a raw `cref` attribute value to a concise
display string, returning a `(string Text, bool ShouldWrapInCodeSpan)` tuple.

- *Algorithm*: Strips the type-kind prefix (`T:`, `M:`, `P:`, `F:`, `E:`); strips
  the namespace path from the remaining qualified name to leave just the type and
  member name; replaces generic arity markers (`` `1 ``, `` `2 ``) with angle-bracket
  type-parameter placeholder notation via `FormatTypeArity` (e.g., `List\`1` →
  `List<T>`,`Dictionary\`2` → `Dictionary<T1, T2>`); replaces`#ctor` with the
  declaring type name for constructor crefs; appends `()` to method crefs that
  include a parameter list.
- *Return contract*: `ShouldWrapInCodeSpan` is `false` only for a constructor
  cref (the kind-prefix dispatch delegates to `FormatMemberReference`, which
  returns `false` for its `#ctor` branch) or any other/unrecognized prefix.
  It is `true` for `T:` crefs (the result is forwarded as-is from
  `FormatTypeName`) and for `M:`/`P:`/`F:`/`E:` crefs that are not
  constructors. This single
  boolean is the sole source of truth `GetInlineReferenceText` uses to decide
  whether to wrap the returned text in an inline code span (see "Member and
  type-only cref inline code span wrapping" below) — no cref-kind parsing is
  duplicated at the call site.

**FormatMemberReference** (private): Formats a `<see cref>` reference whose
kind indicates a type member (`M:`, `P:`, `F:`, or `E:`) as `Type.Member` text,
used by `FormatCref` once the kind-prefix dispatch determines the cref targets
a member rather than a type. Returns a `(string Text, bool ShouldWrapInCodeSpan)`
tuple.

- *Parameters*: `char kind` — the single-character cref kind prefix (`M`, `P`,
  `F`, or `E`); `string target` — the qualified type-and-member portion of the
  cref value; `string parameters` — the method parameter list text (methods
  only; empty for properties/fields/events).
- *Algorithm*: Splits `target` into a declaring type name and a member name,
  formats each individually, and always renders `{formattedTypeName}.{formattedMemberName}`
  for every one of M/P/F/E — the declaring type name is never omitted. Method
  crefs additionally append the formatted parameter list.
- *Return contract*: `ShouldWrapInCodeSpan` is `false` only for the `kind == 'M'`
  constructor (`#ctor`) branch, which returns the bare type name instead of a
  `Type.Member` form; every other M/P/F/E branch (including the no-dot
  fallback, which returns the raw target unchanged) returns `true`.
- *History*: Prior to this behavior, the declaring type name was included only
  for `M:` (method) crefs, or when the declaring type happened to be a C#
  primitive; a `P:`/`F:`/`E:` cref to a non-primitive type rendered as a bare
  member name with no indication of which type it belonged to — ambiguous
  whenever multiple types in scope share a member name. The now-removed special
  case was not an intentional, documented design decision (confirmed via `git
  blame` and the absence of any test asserting the omitted-type-name behavior
  by name), so it was corrected to match the already-correct `M:` behavior for
  all four member kinds.
- *Scope note*: This fix intentionally stops at rendering the declaring type
  name as plain text. It does NOT attempt to turn the reference into a link to
  the member's own page — `XmlDocReader` has no access to `TypeLinkResolver` or
  any other page-routing context, so making the reference an actual
  cross-reference link would require an architectural change beyond this
  method's scope.

#### Member and type-only cref inline code span wrapping

`GetInlineReferenceText` wraps a `<see>`/`<seealso>` reference's display text
in an inline Markdown code span (via `FormatAsInlineCodeSpan`/
`AppendMarkdownCodeSpan`) when that text was derived from a `cref` attribute
and `FormatCref` reports `ShouldWrapInCodeSpan: true` for it — i.e. the cref
targets a type, or a property, field, event, or non-constructor method. This
makes a type or member reference read visually distinct from surrounding
prose, matching how `<c>` content is already rendered, and was driven by
real-world consumer reports where `Type.Member` text, and then a bare type
name, rendered indistinguishably from plain prose.
The following are deliberately left unwrapped:

- A `langword` attribute value (e.g. `null`, `true`, `false`) — not a
  reference to a declared symbol at all.
- Explicit inner element text (the element supplies its own display text,
  which this method always prefers verbatim over any `cref`-formatted text).
  The explicit label is still passed to `TryLinkifyCref` alongside the
  element's own `cref` attribute — see "Cross-reference linking" below — so a
  resolvable, emitted cref with a custom label (e.g. `<see
  cref="...">the validator</see>`) still becomes a real link, just without a
  code-span wrapper around the label. A constructor cref is the one
  exception: it is never linked even with an explicit label, matching the
  next bullet.
- A constructor cref (`M:...#ctor`) — `FormatMemberReference` collapses it to
  the bare type name and reports `ShouldWrapInCodeSpan: false`. The
  explicit-label branch consults this same `ShouldWrapInCodeSpan` result
  before calling `TryLinkifyCref`, so a labeled reference to a constructor
  (e.g. `<see cref="M:...#ctor">the constructor</see>`) renders its label
  unlinked too.

#### Generic cref escaping inside a code span

`FormatTypeArity` renders a generic arity marker as angle-bracket
type-parameter placeholder notation (e.g. the one-type-parameter marker
becomes `List<T>`). By default the angle brackets are backslash-escaped
(rendered as `List\<T\>`) because the result is normally embedded directly in
prose, where an unescaped angle-bracket pair would otherwise be parsed as an
(invalid) HTML tag by a Markdown renderer. A `bool forCodeSpan` parameter
(default `false`), threaded through the full call chain `FormatCref` →
`FormatMemberReference` → `FormatTypeName` → `FormatTypeArity`, switches this
off and returns the raw, unescaped form instead, because a code span's
content is rendered literally — the escaping backslash would otherwise
appear as a stray visible character in the output (rendering as
`List\<T\>.Add()` instead of the intended `List<T>.Add()`).

- `GetInlineReferenceText` passes `forCodeSpan: true` into `FormatCref`,
  since a type or member cref's result is always subsequently wrapped in a
  code span (see "Member and type-only cref inline code span wrapping" above)
  when `ShouldWrapInCodeSpan` is `true`.
- Each layer of the call chain makes its own locally-correct decision about
  whether `forCodeSpan` actually applies to the branch it is in, rather than
  blindly forwarding the caller's request:
  - `FormatCref`'s type-only (`T:`) branch forwards the caller's
    `forCodeSpan` value unchanged into `FormatTypeName`, because a type-only
    cref's result is now always wrapped in a code span by
    `GetInlineReferenceText`.
  - `FormatMemberReference`'s constructor (`#ctor`) branch always hardcodes
    `forCodeSpan: false`, because a constructor cref collapses
    to the bare type name, which is never wrapped.
  - Every other `FormatMemberReference`/`FormatTypeName` branch forwards the
    caller's `forCodeSpan` value unchanged.
  - This resolves the chicken-and-egg problem that `FormatCref` cannot know
    in advance whether a given `M:` cref will turn out to be a constructor
    (and thus unwrapped) until it is already inside `FormatMemberReference`.

**DedentCode** (private static): Removes common leading whitespace
indentation from raw `<code>` element content so the result renders flush-left in
a fenced Markdown code block. The minimum indentation is computed from the leading
whitespace of every non-blank line (blank lines are excluded from the calculation
to avoid artificially reducing the common prefix). That prefix is then stripped
from every line. Leading and trailing blank lines are removed from the final
result. Returns `string.Empty` for whitespace-only input so existing
`string.IsNullOrEmpty` guards remain effective.

**FormatAsInlineCodeSpan** (private static): Wraps already-formatted display
text in an inline Markdown code span via `AppendMarkdownCodeSpan`. Used by
`GetInlineReferenceText` to visually distinguish a type- or member-cref's
rendered text from surrounding prose — see "Member and type-only cref inline
code span wrapping" below.

#### Cross-reference linking (`CrefLinkContext`)

Every public rendering method (`GetSummary`, `GetSummaryMarkdown`,
`GetRemarks`, `GetParams`, `GetReturns`, `GetExampleParts` — explicitly NOT
`GetExceptions`/`GetExceptionDetails`, which are out of scope for linking)
gains a new `internal` overload accepting an additional optional, trailing,
nullable `CrefLinkContext? linkContext = null` parameter. The original public
single-argument overloads are unchanged and simply forward to the new
internal overload with `linkContext: null`, so the public API surface of
`XmlDocReader` is completely unaffected by this feature (`GetExample` has no
such overload — see its own entry above). This parameter is threaded,
unchanged, down through the entire private rendering chain:
`GetDocumentationText`/`GetSingleLineDocumentationText` →
`AppendNodeText` (both overloads) → `AppendElementText`/`AppendListText` →
`GetInlineReferenceText`, where it is finally consulted — only in the
`<see>`/`<seealso>` dispatch branch, via the new private helper
`TryLinkifyCref`.

**`CrefLinkContext`** (`internal sealed record`, in `CrefLinkContext.cs`):
Bundles everything `XmlDocReader` needs to resolve a `<see cref>`/`<seealso
cref>` reference to a real relative Markdown link instead of its default
code-span-only fallback. Fields: `CrefTargetResolver Targets` (resolves a raw
`cref` identifier to the intra-assembly symbol it names); `TypeLinkResolver
Resolver` (builds the actual Markdown link once a target has been resolved
and confirmed to be emitted); `IReadOnlyDictionary<string, string>
MemberPageIndex` (member XML-doc-ID → gradual-disclosure page key, consulted
by `TypeLinkResolver.LinkifyResolvedMember`); `Func<TypeDefinition, bool>
IsTypeEmitted` and `Func<IMemberDefinition, bool> IsMemberEmitted` (typically
bound to the active `DotNetEmitter`'s own visibility rules, so visibility
logic is never duplicated here); `string CurrentFolder` (folder path of the
Markdown file currently being rendered, used to compute the relative link
path). It deliberately carries only primitives, delegates, and
`ApiMark.DotNet`-internal peer types (`TypeLinkResolver`,
`CrefTargetResolver`) — never a `DotNetEmitter`/`DotNetAstModel` reference —
so `XmlDocReader` does not take on a hard dependency on the emitter/model
layer merely to support cref linking.

**TryLinkifyCref** (private static): Attempts to resolve a `cref` string
against `linkContext` and, on success, returns the already code-span-wrapped
display text wrapped in a real Markdown link, with the code span as the
link's label; otherwise returns the code-span text unchanged.

- *Parameters*: `string cref` — the raw `cref` attribute value, including its
  kind prefix; `string codeSpanText` — the already backtick-wrapped display
  text (produced by `FormatAsInlineCodeSpan` from `FormatCref`'s result) to
  use as the link label when linking; `CrefLinkContext? linkContext`.
- *Algorithm*: Returns `codeSpanText` unchanged immediately when `linkContext`
  is `null` (this is how single-file mode opts out of linking entirely —
  `DotNetEmitterSingleFile` never constructs or passes a `CrefLinkContext`,
  so every call site there always resolves this branch). Otherwise tries
  `linkContext.Targets.TryResolveType` first; if it succeeds AND
  `linkContext.IsTypeEmitted(type)` is `true`, returns
  `linkContext.Resolver.LinkifyResolvedType(type, codeSpanText,
  linkContext.CurrentFolder)`. Otherwise tries
  `linkContext.Targets.TryResolveMember`; if it succeeds AND
  `linkContext.IsMemberEmitted(member)` is `true`, returns
  `linkContext.Resolver.LinkifyResolvedMember(member, codeSpanText,
  linkContext.CurrentFolder, linkContext.MemberPageIndex)`. Falls back to
  `codeSpanText` unchanged in every other case — external type, filtered-out
  member, or malformed/unresolvable cref — identical to today's rendering.
- *Call site*: `GetInlineReferenceText` has two call sites for
  `TryLinkifyCref`, both only reached when a `cref` attribute is present:
  - The explicit-label branch (element has its own display text) calls
    `FormatCref` first purely to read its `ShouldWrapInCodeSpan`
    classification (discarding the formatted text, since the explicit label
    is always used), and only calls `TryLinkifyCref` — with that explicit
    text, unwrapped, as the label — when it is `true`. A successful link
    renders as `[the validator](path.md)`, preserving the author's chosen
    wording rather than substituting the formatted `cref` text. A labeled
    constructor cref (`ShouldWrapInCodeSpan: false`) is never linked,
    matching the no-explicit-text branch's precedent below.
  - The no-explicit-text branch calls `TryLinkifyCref` only when `FormatCref`
    already reported `ShouldWrapInCodeSpan: true` (i.e. the exact same subset
    of crefs that were already eligible for code-span wrapping before this
    feature existed — constructor crefs are excluded by construction, since
    they report `ShouldWrapInCodeSpan: false` and short-circuit before
    `TryLinkifyCref` is ever called). The code span is built first via
    `FormatAsInlineCodeSpan`, and that code-span string is then passed into
    `TryLinkifyCref` as the link's label, so a successful link renders as
    `` [`Type.Member`](path.md) `` — the code span nested inside the link's
    label.
  - In both cases, an unresolved cref falls back to the label unchanged
    (plain explicit text, or `` `Type.Member` ``), exactly as it did before
    this feature existed. The nesting is deliberately link-outside/code-span-
    inside rather than the reverse: Markdown code-span content is rendered
    literally, so a link placed inside a code span would show as inert
    `[...](...)` text rather than a clickable link.

### Error Handling

`XmlDocReader` throws `FileNotFoundException` when the XML documentation file
does not exist. Missing member entries return `null` or an empty collection
rather than throwing. Duplicate member IDs in the XML file are silently handled
by the first-wins policy. Cyclic `<inheritdoc />` chains are detected and
resolved to `null` or empty without throwing. Missing `cref` targets or absent
chain entries degrade gracefully to `null` or empty. A member ID absent from
both the local index and the external member lookup delegate (when configured)
degrades to `null`/empty identically to a purely local miss.

### Dependencies

- **System.Xml.Linq** — used to parse and navigate the XML documentation file.
- **System.Xml.XPath** — used to evaluate the `path` XPath attribute in
  `<inheritdoc path="..." />` elements.
- **CrefTargetResolver** / **TypeLinkResolver** — consulted (via
  `CrefLinkContext`, an optional parameter) by `TryLinkifyCref` to resolve and
  render `<see cref>`/`<seealso cref>` references as real Markdown links when
  possible — see "Cross-reference linking (CrefLinkContext)" above. Both
  dependencies are optional at the type level: `XmlDocReader` compiles and
  behaves identically when every caller omits `linkContext` (passing
  `null`), so these are not hard dependencies.

### Callers

- **DotNetGenerator.Parse** — constructs an `XmlDocReader` from the configured
  `XmlDocPath`, the inheritance chain built from Mono.Cecil metadata, and (when
  `DotNetGeneratorOptions.ReferencePaths` is non-empty) an `ExternalXmlDocResolver`
  instance's `TryGetMember` method as the external member lookup delegate.
- **DotNetEmitterGradualDisclosure** — calls all getter methods when writing
  member detail pages, passing a `CrefLinkContext` built from the ambient
  `DotNetAstModel`'s `CrefTargets`/`MemberPageIndex`/`Resolver` and the
  emitter's own visibility delegates, so resolvable intra-assembly crefs
  render as real links.
- **DotNetEmitterSingleFile** — calls getter methods when writing member
  sections in the single-file output, never passing a `CrefLinkContext` (all
  calls omit the parameter, defaulting it to `null`), so single-file output
  remains code-span-only exactly as before this feature existed.

### External Interfaces

N/A — this is an internal class with no external interfaces exposed beyond its assembly.
