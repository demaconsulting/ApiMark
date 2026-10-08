## CrefTargetResolver

![CrefTargetResolver Structure](ApiMarkDotNetView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

CrefTargetResolver resolves a raw XML-doc `cref` attribute string (e.g.
`T:MyNamespace.MyClass` or `M:MyNamespace.MyClass.DoWork(System.Int32)`) to the
Mono.Cecil type or member it names, when that symbol is declared in a single,
specific assembly. It is the first stage of full `<see cref>`/`<seealso cref>`
cross-reference linking: before a reference can be rendered as a real Markdown
link, the engine must know whether a symbol with that exact XML-doc identifier
even exists in the assembly being documented (as opposed to an external
framework type, or a malformed/misspelled cref string).

This resolver intentionally applies no visibility, obsolete, or
exclude-pattern filtering — it answers only "does a symbol with this
XML-doc-ID string exist in this assembly". Deciding whether a resolved symbol
will actually be *emitted* as a page under the active visibility settings is a
separate, caller-supplied concern (see `CrefLinkContext`'s
`IsTypeEmitted`/`IsMemberEmitted` delegates in the `XmlDocReader` design doc).
Keeping these concerns separate lets a caller distinguish "no such symbol"
(external assembly, or a malformed cref) from "the symbol exists but is
filtered out of the generated documentation" — both cases fall back to the
pre-existing code-span-only rendering, but for different reasons.

### Data Model

CrefTargetResolver is immutable after construction; all indexing happens once,
in the constructor.

- *_types* (`Dictionary<string, TypeDefinition>`): Index of type definitions
  keyed by their XML-doc type identifier (e.g. `T:Namespace.Type`), built via
  `DotNetEmitter.BuildTypeId`.
- *_members* (`Dictionary<string, IMemberDefinition>`): Index of member
  definitions keyed by their XML-doc member identifier (e.g.
  `M:Namespace.Type.Method`, `P:Namespace.Type.Property`), built via
  `DotNetEmitter.BuildMemberId`.

No new identifier-formatting or parsing logic is introduced: both indexes are
built with the exact same builders `XmlDocReader` uses to key its own
documentation index, so a dictionary lookup against either index is
byte-for-byte compatible with the identifier format that appears in a raw
`cref="..."` attribute value for an intra-assembly target.

### Key Methods

**CrefTargetResolver constructor**: Builds both indexes by walking every type
(including nested types, at every depth) and member declared in the supplied
assembly.

- *Parameters*: `AssemblyDefinition assembly` — the assembly whose types and
  members to index.
- *Algorithm*: Iterates `assembly.MainModule.GetTypes()` — Mono.Cecil's
  flattening enumerator, which already includes nested types at every depth,
  so no separate recursive walk is needed. For each type, computes its type ID
  via `DotNetEmitter.BuildTypeId` and adds it to `_types` (first occurrence
  wins on a duplicate — tolerated defensively the same way `XmlDocReader`
  tolerates duplicate member IDs in the XML documentation file, though this
  should not occur for well-formed assemblies). For each of the type's
  `Methods`/`Properties`/`Fields`/`Events`, computes the member ID via
  `DotNetEmitter.BuildMemberId` and adds it to `_members`, skipping any member
  for which `BuildMemberId` returns `string.Empty` (a member kind the builder
  does not recognize — indexing an empty key would be collision-prone).

**CrefTargetResolver.TryResolveType**: Attempts to resolve a `cref` string to a
type declared in the indexed assembly.

- *Parameters*: `string crefId` — the raw `cref` attribute value, including its
  `T:` kind prefix (e.g. `T:MyNamespace.MyClass`); `out TypeDefinition type` —
  the resolved type when the method returns `true`.
- *Returns*: `true` when a type with this exact identifier was indexed;
  otherwise `false`.

**CrefTargetResolver.TryResolveMember**: Attempts to resolve a `cref` string to
a member declared in the indexed assembly.

- *Parameters*: `string crefId` — the raw `cref` attribute value, including its
  `M:`/`P:`/`F:`/`E:` kind prefix (e.g.
  `M:MyNamespace.MyClass.DoWork(System.Int32)`); `out IMemberDefinition member`
  — the resolved member when the method returns `true`.
- *Returns*: `true` when a member with this exact identifier was indexed;
  otherwise `false`.

### Error Handling

CrefTargetResolver does not throw. An unresolvable `crefId` (external
assembly, malformed string, or any other reason) simply yields `false` from
`TryResolveType`/`TryResolveMember`, matching the existing fallback behavior
for crefs that cannot be linked.

### Dependencies

- **Mono.Cecil** — CrefTargetResolver walks `AssemblyDefinition`,
  `ModuleDefinition.GetTypes()`, `TypeDefinition`, and `IMemberDefinition`.
- **DotNetEmitter.BuildTypeId** / **DotNetEmitter.BuildMemberId** — reused
  as-is to compute index keys; CrefTargetResolver never duplicates any of
  their XML-doc-ID formatting logic, so any future change to how those
  builders encode an ID (e.g. the indexer parameter-list and generic
  positional-notation fixes) is automatically reflected here without any
  change to this unit.

### Callers

- **DotNetAstModel** — constructs a `CrefTargetResolver` eagerly in its own
  constructor (needs only the assembly, so no deferred post-construction step
  is required, unlike `MemberPageIndex`) and exposes it via the `CrefTargets`
  property.
- **XmlDocReader** — consults `CrefLinkContext.Targets.TryResolveType`/
  `TryResolveMember` from `GetInlineReferenceText` to decide whether a `<see
  cref>`/`<seealso cref>` reference can participate in cross-reference
  linking at all, before consulting the caller-supplied
  `IsTypeEmitted`/`IsMemberEmitted` delegates to decide whether it actually
  will be.

### External Interfaces

N/A — this is an internal class with no external interfaces exposed beyond its assembly.
