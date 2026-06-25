## CppTypeLinkResolver

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

CppTypeLinkResolver resolves simplified C++ type strings to Markdown table-cell
content. It links documented intra-library types, leaves primitives and `std::`
types unchanged, and accumulates non-`std` external type references for later
emission in page-level `External Types` tables.

### Data Model

**`_knownTypes`** (private): `IReadOnlyDictionary<string, string>` mapping fully
qualified C++ names (`::`) to documentation page keys (`/`).

**`Primitives`** (private static): `HashSet<string>` containing primitive and
fundamental C++ type names that must always remain plain text.

**CppExternalTypeInfo** (internal record): ordered external-type entry recorded by
`Linkify` when the base type is not known locally.

- `TypeString`: `string` — short type name rendered in the table.
- `Namespace`: `string` — external namespace using `::` separators.
- *Ordering*: comparison sorts first by `TypeString`, then by `Namespace`, so page
  output is deterministic.

### Key Methods

- **Linkify** — returns the original string unchanged for null/whitespace input,
  primitives, and `std::` types; resolves exact qualified matches first; falls back
  to an unambiguous short-name match; when no known type matches and the stripped
  name has a non-empty non-`std` namespace, records a `CppExternalTypeInfo` entry.
- **FindPageKey** — performs exact qualified lookup first, then a short-name scan
  that returns null when the short name is ambiguous.
- **StripQualifiers** — repeatedly removes leading and trailing `const`,
  `volatile`, reference, pointer, and template-argument syntax until the base name
  stabilizes.
- **ExtractNamespace** — returns everything before the last `::`, or an empty
  string for unqualified names.

### Error Handling

N/A - the unit performs no I/O and returns inputs unchanged for unsupported or
empty values.

### External Interfaces

N/A - in-process utility class only.

### Dependencies

- **System.IO.Path** — computes relative Markdown paths.
- **System.Collections.Generic** — stores known types and external-type sets.

### Callers

- **CppGenerator** — constructs the resolver from the flattened known-type map.
- **CppEmitter** — stores and forwards the resolver.
- **CppEmitterGradualDisclosure** — calls `Linkify` for return-type, parameter,
  field-type, and alias-type cells.
