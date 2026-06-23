## CppTypeLinkResolver

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

CppTypeLinkResolver resolves C++ type strings to Markdown link text suitable for
table cells in the generated API documentation. It produces one of three outcomes
for each type string:

1. **Intra-library type**: a relative Markdown link `[Name](relative/path.md)` when
   the stripped base name matches a known documented type (exact qualified match or
   unambiguous short-name fallback).
2. **Primitive or `std::` type**: emitted as plain text and not tracked as external.
3. **Non-std external type**: emitted as plain text and added to the caller-supplied
   `CppExternalTypeInfo` set for later emission in the "External Types" section.

Links are emitted only in table cells — never inside fenced code blocks.

### Data Model

**`_knownTypes`** (private): `IReadOnlyDictionary<string, string>` — maps
fully-qualified C++ type names using `::` separators (e.g. `"fixtures::SampleClass"`)
to documentation page keys using `/` separators (e.g. `"fixtures/SampleClass"`).
Built in `CppGenerator.Parse` and supplied at construction time.

**`Primitives`** (private static): `HashSet<string>` — set of C++ primitive type
names (`void`, `bool`, `int`, etc.) that are always plain text and never tracked as
external.

### Key Methods

**CppTypeLinkResolver.Linkify** (public): Resolves a type string to Markdown link
text or plain text.

- *Parameters*: `string cppTypeString` — the simplified C++ type string to resolve.
  `string currentFolder` — folder path of the output Markdown file, used to compute
  relative hrefs. `ISet<CppExternalTypeInfo> externalTypes` — mutable accumulator
  for non-std external type references.
- *Returns*: A Markdown link string or the original `cppTypeString` unchanged.
- *Algorithm*:
  1. Return `cppTypeString` unchanged when it is null or whitespace.
  2. Strip qualifiers via `StripQualifiers` to isolate the base type name.
  3. Return unchanged when stripped is empty, a primitive, or starts with `std::`.
  4. Look up the stripped name via `FindPageKey` (exact match, then short-name
     fallback).
  5. When found: compute the relative path from `currentFolder` to the page key,
     replace the short name in the original string with a Markdown link.
  6. When not found and the type has a non-std namespace: add to `externalTypes`
     and return the original string.

**FindPageKey** (private): Looks up the page key for a stripped type name.

- Tries exact qualified-name match first; falls back to short-name scan that
  returns `null` when two or more known types share the same unqualified name
  (ambiguous).

**StripQualifiers** (internal static): Removes C++ cv-qualifiers (`const`,
`volatile`), reference qualifiers (`&`, `&&`), pointer qualifiers (`*`), trailing
`const`, and template arguments from a type string to isolate the base type name.

**ExtractNamespace** (private static): Returns the namespace portion of a qualified
C++ name (everything before the last `::`), or an empty string for unqualified names.

### Error Handling

N/A - CppTypeLinkResolver performs no I/O and throws no exceptions under normal
operation. Null or whitespace type strings are returned unchanged without error.

### External Interfaces

N/A - CppTypeLinkResolver is an in-process utility class with no external
dependencies or outbound interfaces. All input is supplied via constructor and
method parameters.

### Dependencies

N/A - CppTypeLinkResolver depends only on the BCL (`System.IO.Path`,
`System.Collections.Generic`). It has no dependency on ApiMarkCore or other
ApiMark units.

### Callers

- **CppEmitter** — constructs `CppTypeLinkResolver` in `CppGenerator.Parse`
  from the `knownTypes` dictionary built from `namespaceDecls`, and forwards it
  to both format-specific emitters.
- **CppEmitterGradualDisclosure** — calls `Linkify` in all methods that write
  type and return-type cells in Markdown tables.
