## TypeLinkResolver

![TypeLinkResolver Structure](../generated/ApiMarkDotNetView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

TypeLinkResolver resolves Mono.Cecil `TypeReference` instances to Markdown link
text suitable for table cells in generated API documentation. It applies
composite type handling (Nullable\<T\>, arrays, generic instances) before
determining whether a leaf type is intra-assembly (emitted as a relative
Markdown link), a C# primitive or System type (plain text), or a non-System
external type (plain text, tracked for the External Types section).

Linkification is applied only in table cells — never inside fenced code blocks,
because Markdown links do not render inside fences.

### Data Model

TypeLinkResolver is stateless with respect to type-link resolution itself.
Mutable state is carried only via the caller-supplied `ISet<ExternalTypeInfo>`
parameter.

- *_rootNamespaces* (`IReadOnlyList<string>`): Root namespaces of the assembly,
  used to derive relative documentation paths via
  `DotNetEmitter.GetNamespaceFolderPath`.
- *_generateLinks* (`bool`): When `false`, intra-assembly type references are
  rendered as plain text instead of Markdown links. Used by
  `DotNetEmitterSingleFile` where relative file links are meaningless and
  same-name anchors across types would collide.
- *PrimitiveFullNames* (private static `HashSet<string>`): Full CLR names of
  C# primitive types that are always rendered as their keyword alias and never
  tracked as external dependencies.

**ExternalTypeInfo** (internal record): Represents a non-System external type
reference collected during table cell generation.

- *SimplifiedName* (`string`): The type's simplified display name (may include
  escaped generic angle brackets).
- *Namespace* (`string`): The type's .NET namespace.
- *Ordering*: Implements `IComparable<ExternalTypeInfo>` for deterministic
  alphabetical ordering by `SimplifiedName` then `Namespace` — used when emitting
  the External Types table to ensure a stable, predictable sort order so the
  generated section is reproducible across runs. The record also implements four
  comparison operators (`<`, `<=`, `>`, `>=`) as mechanical companions to
  `IComparable<ExternalTypeInfo>`.

### Key Methods

**TypeLinkResolver constructor**: Accepts the root namespaces and an optional
`generateLinks` flag.

- *Parameters*:
  - `IReadOnlyList<string> rootNamespaces` — the root namespaces identified in
    the assembly being documented; forwarded to
    `DotNetEmitter.GetNamespaceFolderPath` when computing target page paths.
  - `bool generateLinks = true` — when `true` (default), intra-assembly types
    are rendered as relative Markdown links; when `false`, all type references
    render as plain text. Used by the single-file emitter.
- *Postconditions*: The resolver is ready to call `Linkify`.

**TypeLinkResolver.Linkify**: Resolves a `TypeReference` to a Markdown string.

- *Parameters*:
  - `TypeReference typeRef` — the Mono.Cecil type reference to resolve.
  - `string currentFolder` — folder path of the containing Markdown file,
    relative to the documentation root; used to compute relative `href` values.
  - `string contextNamespace` — namespace of the owning type; forwarded to
    `TypeNameSimplifier.Simplify`.
  - `ISet<ExternalTypeInfo> externalTypes` — mutable accumulator for non-System
    external type references. The caller creates this set per output file and
    emits the External Types section after all table rows have been written.
  - `bool isNullableAnnotated = false` — when `true`, the member carrying this
    type reference has a nullable-reference annotation.
- *Returns*: A Markdown string (link, plain name, or generic composite).
- *Algorithm*:
  1. Null `typeRef` → return `string.Empty`.
  2. `GenericParameter` → return the parameter name (plus `?` if nullable annotated).
  3. `Nullable<T>` generic instance → recurse on the inner type with nullable flag set.
  4. `ArrayType` → recurse on element type, append `[]` (plus `?` if nullable annotated).
  5. `GenericInstanceType` → `LinkifyGenericType`: links the container when
     intra-assembly, tracks external otherwise.
  6. Primitive or `System.Nullable<>` open type → `TypeNameSimplifier.Simplify`,
     append `?` if nullable annotated.
  7. Intra-assembly (scope is `ModuleDefinition`) → relative Markdown link when `_generateLinks`
     is `true`; plain-text type name when `_generateLinks` is `false`.
  8. Non-System external → track in `externalTypes`, return plain name.
- *Intra-assembly detection*: `TypeReference.Scope is ModuleDefinition`.

### Error Handling

`Linkify` returns `string.Empty` for a null `typeRef` rather than throwing.
No other exceptions are thrown by TypeLinkResolver itself; exceptions from
`TypeNameSimplifier.Simplify` propagate unchanged.

### Dependencies

- **TypeNameSimplifier** — called to produce simplified type names for primitive
  types and as display text for generic arguments. `TypeNameSimplifier.FlattenArity`
  converts a backtick arity suffix to a plain numeric suffix (e.g. `List\`1` →
  `List1`) for use in file-path generation, while`TypeNameSimplifier.StripArity`
  removes the backtick suffix entirely (e.g. `List\`1` → `List`) for display name
  generation. These two methods serve distinct purposes and must not be substituted
  for each other.
- **DotNetEmitter.GetNamespaceFolderPath** — called to compute the documentation
  folder path for intra-assembly types.
- **Mono.Cecil** — TypeLinkResolver operates on Mono.Cecil type reference objects.

### Callers

The direct callers of TypeLinkResolver are the two sub-emitters and the generator
that constructs it. `DotNetEmitter` does not call TypeLinkResolver directly; it holds
a `DotNetAstModel` that carries the resolver, making it available to sub-emitters via
`model.Resolver`.

- **DotNetGenerator** — constructs `DotNetAstModel`, which holds the
  `TypeLinkResolver` instance created during `Parse`.
- **DotNetEmitterGradualDisclosure** — calls `Linkify` for each type reference
  encountered in member table rows.
- **DotNetEmitterSingleFile** — constructs a TypeLinkResolver with
  `generateLinks: false` for parameter type display in single-file output.

### External Interfaces

N/A — this is an internal class with no external interfaces exposed beyond its assembly.
