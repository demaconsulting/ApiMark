## XmlDocReader

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

**GetExampleParts**: Returns the structured example content for `memberId` as
`IReadOnlyList<(bool IsCode, string Content)>` parts. When the `<example>`
element contains no `<code>` children, the entire text is returned as a single
code part after applying `DedentCode`. When `<code>` children are present, text
nodes become prose parts and `<code>` elements become code parts with `DedentCode`
applied to each code block. Resolves `<inheritdoc />` first.

**ResolveMemberElement** (private): Resolves the effective `<member>` element
for a given ID by following `<inheritdoc />` recursively with cycle detection.

- Returns the member element directly when no `<inheritdoc />` child is present.
- When `cref` is present, resolves the named target recursively.
- When no `cref` is present, tries each candidate from `_inheritanceChain` in
  order, stopping at the first that yields a result.
- When `path` is present (XPath expression), evaluates it against the resolved
  source element and wraps matching nodes in a synthetic `<member>` element.
- Maintains a `HashSet<string>` of visited IDs per resolution path to break
  cycles without throwing.

**Whitespace normalization**: `GetDocumentationText` normalizes text by
collapsing internal whitespace within each line. `GetSingleLineDocumentationText`
additionally joins all non-empty trimmed lines into a single space-separated
string. Both normalize line endings to `\n` before processing.

**Code block dedentation** (`DedentCode`, private static): Removes common leading
indentation from raw `<code>` element content so the result renders flush-left in
a fenced Markdown code block. The minimum indentation is computed from the leading
whitespace of every non-blank line (blank lines are excluded from the calculation
to avoid artificially reducing the common prefix). That prefix is then stripped
from every line. Leading and trailing blank lines are removed from the final
result. Returns `string.Empty` for whitespace-only input so existing
`string.IsNullOrEmpty` guards remain effective.

**cref formatting** (`FormatCref`, private static): Strips the type-kind
prefix (`T:`, `M:`, `P:`, `F:`, `E:`) from a cref value and formats the
result as a readable name. Constructor crefs (`#ctor`) are replaced by the
type name. Method crefs with parameters append `()`. Type crefs are simplified
using `FormatTypeName`, which applies C# primitive aliases for well-known CLR
names.

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
