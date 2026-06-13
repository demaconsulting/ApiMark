## XmlDocReader

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

XmlDocReader reads and indexes a .NET XML documentation file for fast
member-level lookups. It builds a `Dictionary<string, XElement>` keyed by XML
doc member identifier on construction, enabling O(1) lookups for summary,
remarks, params, returns, exceptions, and example content during emit.

### Data Model

**_members** (private `Dictionary<string, XElement>`): Index of documentation
members keyed by their XML doc identifier string (e.g.
`T:MyNamespace.MyClass`, `M:MyNamespace.MyClass.Method(System.Int32)`). Built
once on construction; read-only thereafter.

**Duplicate-key policy**: When duplicate member names appear in the XML doc
file, the first occurrence is used and subsequent duplicates are silently
discarded. This is a defensive policy for malformed but real-world XML doc
files where the compiler emits the same member ID more than once (e.g., due to
partial-class splits or tooling bugs).

### Key Methods

**XmlDocReader constructor**: Parses the XML documentation file and builds the
member index.

- *Parameters*: `string xmlDocPath` — path to the XML documentation file.
- *Preconditions*: `xmlDocPath` must exist on disk.
- *Postconditions*: `_members` is populated; all lookups are O(1).
- *Exceptions*: Throws `FileNotFoundException` when `xmlDocPath` does not exist.

**GetSummary**: Returns trimmed single-line summary text for `memberId`, or
`null` if absent. Summary text is normalized to a single line because summaries
are by convention brief one-liner descriptions.

**GetRemarks**: Returns trimmed remarks text for `memberId`, or `null` if
absent. May contain multiple lines.

**GetParams**: Returns parameter names and descriptions for `memberId` as
`IReadOnlyList<(string Name, string? Description)>`. Returns an empty list
when the member is absent.

**GetReturns**: Returns trimmed returns text for `memberId`, or `null` if
absent.

**GetExceptions**: Returns all `cref` attribute values from `<exception>`
elements for `memberId` as `IReadOnlyList<string>`. Returns an empty list when
the member is absent.

**GetExceptionDetails**: Returns exception types and descriptions from
`<exception>` elements for `memberId` as
`IReadOnlyList<(string Type, string? Description)>`. The `Type` field is
the formatted cref value (applying `FormatCref` to strip the type prefix and
format names); entries with an empty cref are filtered out.

**GetExample**: Returns trimmed example text for `memberId`, or `null` when
the `<example>` element is absent or contains only whitespace.

**GetExampleParts**: Returns the structured example content for `memberId` as
`IReadOnlyList<(bool IsCode, string Content)>` parts. When the `<example>`
element contains no `<code>` children, the entire text is returned as a single
code part. When `<code>` children are present, text nodes become prose parts
and `<code>` elements become code parts.

**Whitespace normalization**: `GetDocumentationText` normalizes text by
collapsing internal whitespace within each line. `GetSingleLineDocumentationText`
additionally joins all non-empty trimmed lines into a single space-separated
string. Both normalize line endings to `\n` before processing.

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
by the first-wins policy.

### Dependencies

- **System.Xml.Linq** — used to parse and navigate the XML documentation file.

### Callers

- **DotNetGenerator.Parse** — constructs an XmlDocReader from the configured
  XmlDocPath and stores it in DotNetAstModel.
- **DotNetEmitterGradualDisclosure** — calls all getter methods when writing
  member detail pages.
- **DotNetEmitterSingleFile** — calls getter methods when writing member
  sections in the single-file output.
