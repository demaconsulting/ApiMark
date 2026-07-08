## XmlDocReader

![XmlDocReader Structure](../generated/ApiMarkDotNetView.svg)

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

**Duplicate-key policy**: When duplicate member names appear in the XML doc
file, the first occurrence is used and subsequent duplicates are silently
discarded. This is a defensive policy for malformed but real-world XML doc
files where the compiler emits the same member ID more than once (e.g., due to
partial-class splits or tooling bugs).

### Key Methods

**XmlDocReader constructor**: Parses the XML documentation file and builds the
member index.

- *Parameters*: `string xmlDocPath` — path to the XML documentation file;
  `IReadOnlyDictionary<string, IReadOnlyList<string>>? inheritanceChain` —
  optional bare inheritdoc resolution map (defaults to `null`).
- *Preconditions*: `xmlDocPath` must exist on disk.
- *Postconditions*: `_members` is populated; all lookups are O(1).
- *Exceptions*: Throws `FileNotFoundException` when `xmlDocPath` does not exist.

**GetSummary**: Returns trimmed single-line summary text for `memberId`, or
`null` if absent. Summary text is normalized to a single line because summaries
are by convention brief one-liner descriptions. Resolves `<inheritdoc />` first.

**GetRemarks**: Returns trimmed remarks text for `memberId`, or `null` if
absent. May contain multiple lines. Resolves `<inheritdoc />` first.

**GetParams**: Returns parameter names and descriptions for `memberId` as
`IReadOnlyList<(string Name, string? Description)>`. Returns an empty list
when the member is absent. `<param>` elements without a `name` attribute are
silently filtered out. Resolves `<inheritdoc />` first.

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
`<inheritdoc />` first.

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
- When `cref` is present, resolves the named target recursively.
- When no `cref` is present, tries each candidate from `_inheritanceChain` in
  order, stopping at the first that yields a result. Each candidate is tried with
  a branch-local copy of the visited set. This ensures that a failed traversal
  through one candidate — which may visit shared ancestor nodes — does not prevent
  subsequent candidates from resolving through those same ancestors.
- When `path` is present (XPath expression), evaluates it against the resolved
  source element and wraps matching nodes in a synthetic `<member>` element.
- Maintains a `HashSet<string>` of visited IDs per resolution path to break
  cycles without throwing.

**Whitespace normalization**: `GetDocumentationText` normalizes text by
collapsing internal whitespace within each line. `GetSingleLineDocumentationText`
additionally joins all non-empty trimmed lines into a single space-separated
string. Both normalize line endings to `\n` before processing.

#### Inline Element Rendering

`GetDocumentationText` processes inline XML elements within doc comment nodes
according to the following element-to-text mappings:

- `<c>text</c>` → CommonMark backtick code span; the fence length adapts to avoid
  embedded backticks in the content per CommonMark §6.1. When the code content starts
  or ends with a backtick, a single space is inserted on each side inside the fence so
  that Markdown parsers can unambiguously identify the fence delimiter (CommonMark §6.1).
- `<see cref="..."/>` → formatted cref value via the `FormatCref` helper (strips the
  type-kind prefix, strips the namespace path to leave just the type name, and replaces
  generic arity markers with angle-bracket type-parameter placeholders via
  `FormatTypeArity`, e.g. `List\`1` → `List<T>`,`Dictionary\`2` → `Dictionary<T1, T2>`).
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
display string.

- *Algorithm*: Strips the type-kind prefix (`T:`, `M:`, `P:`, `F:`, `E:`); strips
  the namespace path from the remaining qualified name to leave just the type and
  member name; replaces generic arity markers (`` `1 ``, `` `2 ``) with angle-bracket
  type-parameter placeholder notation via `FormatTypeArity` (e.g., `List\`1` →
  `List<T>`,`Dictionary\`2` → `Dictionary<T1, T2>`); replaces`#ctor` with the
  declaring type name for constructor crefs; appends `()` to method crefs that
  include a parameter list.

**Code block dedentation** (`DedentCode`, private static): Removes common leading
indentation from raw `<code>` element content so the result renders flush-left in
a fenced Markdown code block. The minimum indentation is computed from the leading
whitespace of every non-blank line (blank lines are excluded from the calculation
to avoid artificially reducing the common prefix). That prefix is then stripped
from every line. Leading and trailing blank lines are removed from the final
result. Returns `string.Empty` for whitespace-only input so existing
`string.IsNullOrEmpty` guards remain effective.

### Error Handling

`XmlDocReader` throws `FileNotFoundException` when the XML documentation file
does not exist. Missing member entries return `null` or an empty collection
rather than throwing. Duplicate member IDs in the XML file are silently handled
by the first-wins policy. Cyclic `<inheritdoc />` chains are detected and
resolved to `null` or empty without throwing. Missing `cref` targets or absent
chain entries degrade gracefully to `null` or empty.

### Dependencies

- **System.Xml.Linq** — used to parse and navigate the XML documentation file.
- **System.Xml.XPath** — used to evaluate the `path` XPath attribute in
  `<inheritdoc path="..." />` elements.

### Callers

- **DotNetGenerator.Parse** — constructs an `XmlDocReader` from the configured
  `XmlDocPath` and the inheritance chain built from Mono.Cecil metadata.
- **DotNetEmitterGradualDisclosure** — calls all getter methods when writing
  member detail pages.
- **DotNetEmitterSingleFile** — calls getter methods when writing member
  sections in the single-file output.

### External Interfaces

N/A — this is an internal class with no external interfaces exposed beyond its assembly.
