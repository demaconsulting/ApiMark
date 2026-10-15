## TypeLinkResolver

![TypeLinkResolver Structure](ApiMarkDotNetView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

TypeLinkResolver resolves Mono.Cecil `TypeReference` instances to Markdown link
text suitable for table cells in generated API documentation. It applies
composite type handling (Nullable\<T\>, arrays, generic instances) before
determining whether a leaf type is intra-assembly (emitted as a relative
Markdown link), a C# primitive or System type (plain text), an external type from an
assembly mapped through the external link map (a link into that assembly's documentation,
not tracked), or an unmapped non-System external type (plain text, tracked for the External
Types section).

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
- *_externalLinks* (private `Dictionary<string, ExternalLinkTarget>`): Map from a
  referenced assembly's simple name to the location of its generated documentation.
  Always a case-insensitive (`OrdinalIgnoreCase`) copy of the caller-supplied
  dictionary, regardless of the comparer the caller's dictionary uses.
- *PrimitiveFullNames* (private static `HashSet<string>`): Full CLR names of
  C# primitive types that are always rendered as their keyword alias and never
  tracked as external dependencies.

**ExternalLinkTarget** (nested internal record): Describes where another
assembly's documentation lives.

- *ApiPath* (`string`): A relative path from this documentation's root to the other
  API root (e.g. `../OtherLib/api`), or an absolute `http(s)` base URL. A `file:` URI
  or rooted file-system path is also handled defensively (see External Assembly Links).
- *RootNamespaces* (`IReadOnlyList<string>`): The other assembly's root namespaces,
  used to derive namespace folders exactly as that assembly's own run did.

**NullabilityCursor** (internal class, declared in `TypeNameSimplifier.cs`):
Supplies the per-position nullable flags (0 oblivious, 1 not annotated, 2 annotated)
of one member in pre-order. It is consumed by `Linkify` and is not thread-safe; one
cursor is created per member.

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

**TypeLinkResolver constructor**: Accepts the root namespaces, an optional
`generateLinks` flag, and an optional external-links map.

- *Parameters*:
  - `IReadOnlyList<string> rootNamespaces` — the root namespaces identified in
    the assembly being documented; forwarded to
    `DotNetEmitter.GetNamespaceFolderPath` when computing target page paths.
  - `bool generateLinks = true` — when `true` (default), intra-assembly types
    are rendered as relative Markdown links; when `false`, all type references
    render as plain text and the external-links map is ignored. Used by the
    single-file emitter.
  - `IReadOnlyDictionary<string, ExternalLinkTarget>? externalLinks = null` —
    map from referenced assembly name to documentation location. Copied into a
    case-insensitive dictionary.
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
  - `bool isNullableAnnotated = false` — when `true`, the outermost type position
    is annotated nullable. This public overload wraps the flag in
    `NullabilityCursor.ForOutermostFlag` and delegates to the cursor overload below,
    so positions after the first are oblivious.
- *Returns*: A Markdown string (link, plain name, or generic composite).

**TypeLinkResolver.Linkify (NullabilityCursor overload)** (internal): The same
resolution with a `NullabilityCursor? nullability` parameter in place of the
boolean, applying a per-position annotation to the outer type, array elements,
and generic arguments. This is the overload the emitters call.

- *Algorithm*:
  1. Null `typeRef` → return `string.Empty`.
  2. `ByReferenceType` (`ref`/`out`/`in`) → recurse on the element type. The
     keyword is the caller's responsibility; the wrapper consumes **no** flag.
  3. Every other position consumes exactly one flag from the cursor, in pre-order
     (outer type first, then array element or generic arguments left to right).
     When the cursor is null or its flags are exhausted, the cursor's default
     (the inherited `NullableContext`, or oblivious) applies.
     `TypeNameSimplifier.IsAnnotated(typeRef, flag)` turns the flag into a boolean:
     a value type (and a generic parameter constrained to `struct`) is **never**
     annotated, so `int` never receives `?` even when flag 2 is supplied or the
     legacy `isNullableAnnotated: true` is passed.
  4. `GenericParameter` → return the parameter name (plus `?` if annotated).
  5. `Nullable<T>` generic instance → the wrapper's flag is consumed in step 3;
     recurse on the inner type (which consumes its own, ignored, flag) and append `?`.
  6. `ArrayType` → recurse on the element type, append the rank suffix (`[]`, `[,]`),
     plus `?` if the array position is annotated.
  7. `GenericInstanceType` → `LinkifyGenericType`: links the container when
     intra-assembly; otherwise tries `TryLinkExternalType` and falls back to tracking
     the container as external. Arguments are linkified recursively, then the
     angle-bracketed list and any `?` are appended.
  8. Primitive or `System.Nullable<>` open type → `TypeNameSimplifier.Simplify`,
     append `?` if annotated.
  9. Intra-assembly (scope is `ModuleDefinition`) → relative Markdown link when
     `_generateLinks` is `true`; plain-text type name when `false`.
  10. Non-System external → `TryLinkExternalType`: when the type's assembly is mapped
     and links are enabled, return a link into that documentation and do not track
     the type; otherwise track it in `externalTypes` and return the plain name.
- *Intra-assembly detection*: `TypeReference.Scope is ModuleDefinition`.

**TypeLinkResolver.TryLinkExternalType** (private): Builds a link for a type whose
scope is an `AssemblyNameReference` present in `_externalLinks`. Returns `null` when
links are disabled or the assembly is not mapped. See External Assembly Links.

**TypeLinkResolver.BuildTypePageKey** (private static; type file names come from `DotNetEmitter.GetTypeFileName`, which applies `FileNameSanitizer` and the collision rules): Computes a type's page key
for a given set of root namespaces. `GetTypePageKey` calls it with this assembly's
root namespaces; `TryLinkExternalType` calls it with the mapped assembly's.

**TypeLinkResolver.LinkifyResolvedType**: Resolves a `cref` target already
known to be a type declared in the assembly being documented to a Markdown
link wrapping an already-formatted display text — used for cross-reference
linking of `<see cref>`/`<seealso cref>` references, as distinct from
`Linkify`'s table-cell type-reference resolution.

- *Parameters*: `TypeDefinition type` — the resolved intra-assembly type;
  `string displayText` — the already-formatted display text (e.g. from
  `XmlDocReader.FormatCref`) to wrap in the Markdown link; `string
  currentFolder` — folder path of the containing Markdown file.
- *Returns*: `[displayText](relative/path.md)`, or `displayText` unchanged
  when `_generateLinks` is `false` (single-file mode).
- *Algorithm*: Reuses `GetTypePageKey`/`ComputeRelativePath` — the same
  helpers `Linkify` already uses for intra-assembly `TypeReference` values,
  since `TypeDefinition` is itself a `TypeReference`. Callers are expected to
  have already confirmed (via `CrefTargetResolver` and the emitter's
  visibility rules) that `type` will actually be emitted as a page before
  calling this method; it performs no such check itself and always computes a
  link when link generation is enabled.

**TypeLinkResolver.LinkifyResolvedMember**: Resolves a `cref` target already
known to be a member declared in the assembly being documented to a Markdown
link wrapping an already-formatted display text, using the gradual-disclosure
member-to-page index.

- *Parameters*: `IMemberDefinition member` — the resolved intra-assembly
  member; `string displayText` — the already-formatted display text (e.g.
  from `XmlDocReader.FormatMemberReference`); `string currentFolder` — folder
  path of the containing Markdown file; `IReadOnlyDictionary<string, string>
  memberPageIndex` — map from a member's XML-doc identifier (see
  `DotNetEmitter.BuildMemberId`) to its page key.
- *Returns*: `[displayText](relative/path.md)`, or `displayText` unchanged
  when `_generateLinks` is `false`, or when `member` has no entry in
  `memberPageIndex` (e.g. filtered out of the generated documentation).
- *Why a lookup table and not a closed-form computation*: unlike
  `LinkifyResolvedType`, there is no closed-form page-key computation for a
  member — in gradual-disclosure mode, members may share a page with their
  overloads, with same-named-but-different-kind members (case-collision
  grouping), or with every other operator overload of the same type;
  decisions made per-type while generating that type's own pages (see
  `DotNetEmitterGradualDisclosure.BuildMemberPageIndex`). `memberPageIndex` is
  therefore required to resolve the member's actual page path.
- *Anchors*: A page key may end in `#anchor` (shared overload and operators
  pages). The fragment is split off before the relative path is computed and
  re-appended to the link target, so the link opens the exact overload heading.

**TypeLinkResolver.GetTypePageKey** (private): Computes the documentation page
key (relative path without extension) for an intra-assembly `TypeReference`,
correctly handling nested types of arbitrary nesting depth.

- *Problem*: Mono.Cecil gives nested types an **empty** `TypeReference.Namespace`
  — the namespace is carried only by the outermost non-nested ancestor. Using
  `typeRef.Namespace` directly for a nested type therefore produced an empty
  namespace folder and a broken link, regardless of whether the nested type was
  referenced as a method parameter, return type, field type, or generic type
  argument.
- *Algorithm*:
  1. Walk `TypeReference.DeclaringType` from `typeRef` upward, collecting every
     ancestor (including `typeRef` itself) into a list ordered outermost to
     innermost.
  2. Derive the namespace folder via `DotNetEmitter.GetNamespaceFolderPath` using
     the **outermost** ancestor's `Namespace` — the only member of the chain
     guaranteed to carry a namespace (empty for global-namespace types, which map
     to the `global-namespace` folder).
  3. Join every ancestor's name, outermost to innermost, each flattened via
     `TypeNameSimplifier.FlattenArity`, with `/` separators.
  4. Prefix the joined path with the namespace folder (when non-empty) to
     produce the final page key, e.g. `MyLib/Outer/Inner` or, for two levels of
     nesting, `MyLib/Outer/Middle/Inner`.
- *Non-nested types*: For a non-nested type, the chain has exactly one element,
  so the algorithm naturally reduces to the pre-existing non-nested behavior
  with no special-casing required.
- *Consistency*: This mirrors exactly how
  `DotNetEmitterGradualDisclosure.WriteNestedTypesSection` writes nested type
  pages to disk (`{NamespaceFolderPath}/{ParentTypeName}/{NestedTypeName}`,
  recursively for arbitrary nesting depth), so every link produced by
  `GetTypePageKey` resolves to a page that the emitter actually creates.

#### Tuples

`Linkify` renders tuple types through `TypeNameSimplifier.BuildTuple`, linking each element, and uses
`ConsumesNullableFlag` so flag positions stay aligned with the signature block.

#### External cref links

`TryLinkifyExternalCref(cref, displayText, currentFolder)` handles a `cref` that
`CrefTargetResolver` cannot resolve inside the documented assembly. It parses the namespace and type from
the cref (a member cref uses its declaring type), picks the `ExternalLinkTarget` whose `RootNamespaces`
entry is the longest namespace prefix, and builds the same link as a type reference to that target. A member
cref links to its type page. It returns `null` when links are disabled, nothing is mapped, or no namespace
matches, and `XmlDocReader` then keeps the plain code span. Nested types are linked as if declared at
namespace level.

### Error Handling

`Linkify` returns `string.Empty` for a null `typeRef` rather than throwing.
No other exceptions are thrown by TypeLinkResolver itself; exceptions from
`TypeNameSimplifier.Simplify` propagate unchanged.

A type from an assembly with no entry in the external-links map is not an error: it
keeps the table-only behavior (plain name, tracked as external). An `ApiPath` that is
not a clean relative path or `http(s)` URL is handled defensively rather than
rejected here (rejection with a warning happens in `DotNetGenerator`): `file:` URIs
are used as-is and rooted paths are converted to `file:` URIs instead of being made
relative. Link targets are always percent-encoded for characters that would break
Markdown (see External Assembly Links).

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
- **XmlDocReader** — calls `LinkifyResolvedType`/`LinkifyResolvedMember` (via
  `CrefLinkContext.Resolver`) from `GetInlineReferenceText` when a resolved
  `<see cref>`/`<seealso cref>` target will actually be emitted as a page.

### External Interfaces

N/A — this is an internal class with no external interfaces exposed beyond its assembly.

### External Assembly Links

An optional map of assembly name to `ExternalLinkTarget` (API path plus the other assembly root
namespaces) lets `TypeLinkResolver` emit `[Name](../../other/api/{namespace}/{Type}.md)` links for
types whose scope is a mapped assembly, for plain, generic, and nested types. Mapped types are not
added to the External Types set. Unmapped types and single-file output (`generateLinks: false`,
which ignores the map entirely) are unchanged.

Algorithm for `TryLinkExternalType`:

1. Look up the type's assembly name (case-insensitive); return `null` when links are
   disabled or there is no mapping.
2. Compute the page key with the mapped assembly's root namespaces (nested types
   append each declaring-type name; generic arity is flattened, e.g. `Box1`).
3. Normalize the `ApiPath`: backslashes become `/`, trailing `/` is trimmed.
4. Choose the form:
   - `http://`, `https://`, or `file:` prefix → used as the base unchanged.
   - Rooted file-system path (`/x` or `C:/x`) → converted to a `file://` base; it is
     never made relative.
   - Otherwise (relative) → strip leading `./` segments, then prefix one `../` per
     segment of `currentFolder` so the link climbs from the current page to the
     documentation root before descending into the other API root.
5. Append `/{pageKey}.md` and percent-encode spaces, parentheses, and angle brackets
   in the whole target so it cannot terminate the Markdown link early.

For generic instances the container is linked and the type arguments are linkified
individually. Links are never verified to exist.

External types are tracked using `TypeNameSimplifier.Simplify` (aliases, nested generics, arrays)
and escaped with `EscapeMarkdownHeading`.

**External nested and generic crefs**: `ExternalLinkTarget` carries the mapped assembly's full namespace
list. `TryLinkifyExternalCref` takes the longest real-namespace prefix of the cref's type name and treats the
remainder as the chain of enclosing types, flattening each segment with `FlattenArity`, so
`NestedGenericHost`1.Inner`1` links to `NestedGenericHost-1/Inner-1.md`. Without a namespace list it
falls back to the previous first-segment rule.
