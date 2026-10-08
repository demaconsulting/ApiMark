## DotNetAstModel

![DotNetAstModel Structure](ApiMarkDotNetView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

DotNetAstModel is a data class that holds all parsed .NET assembly data
required during the emit phase. It is created exclusively by
`DotNetGenerator.Parse` and transferred to `DotNetEmitter`. All properties are
read-only — set either at construction or, for `MemberPageIndex`/
`EmittedTypeIds`, once via `SetMemberPageIndex` shortly after construction
(see Key Methods below) — and the collections they expose use read-only
interfaces (`IReadOnlyList`, `IReadOnlyDictionary`), so no caller can mutate
the model's collections directly. The emitter can safely share the model
across its internal helper methods without defensive copies.

The four context records defined in the same file — `TypePageWriteContext`,
`MethodDocContext`, `NamespaceDocContext`, and `NamespaceDescription` — reduce
parameter counts on the helper methods by bundling constant values that are
threaded through multiple call levels: the first three bundle per-page-type
writing context (`NamespaceDescription` carries the winning summary/remarks/
example member IDs extracted from a `NamespaceDoc` carrier, for on-demand,
link-aware rendering). A fifth record,
`DotNetAstModelArgs`, bundles the `DotNetAstModel` constructor's own
parameters (see Key Methods below).

### Data Model

**DotNetAstModel** (internal sealed class): Holds all data that survives the
boundary between parse and emit.

- *Assembly* (`AssemblyDefinition`): The Mono.Cecil assembly definition held
  open for the duration of emit. Ownership is transferred to the model on
  construction; the `AssemblyDefinition` is disposed via a `using (Model.Assembly)` block in `DotNetEmitter.Emit` after the emit run completes or throws.
- *AssemblyResolver* (`IAssemblyResolver`): The Mono.Cecil assembly resolver
  used to resolve externally referenced assemblies during inheritance analysis
  and lazy metadata resolution. Ownership is transferred to the model on
  construction; disposed alongside `Assembly` via a `using (Model.AssemblyResolver)`
  block in `DotNetEmitter.Emit`, in the same disposal scope, because the
  resolver may still be consulted for lazy metadata resolution for as long as
  `Assembly` is alive.
- *XmlDocs* (`XmlDocReader`): Pre-built XML documentation reader for O(1)
  per-member lookups by XML doc identifier string.
- *AllNamespaces* (`IReadOnlyList<string>`): All namespace names present in the assembly,
  ordered alphabetically (ordinal).
- *ByNamespace* (`IReadOnlyDictionary<string, IReadOnlyList<TypeDefinition>>`): Visible types
  grouped by their namespace name.
- *RootNamespaces* (`IReadOnlyList<string>`): Root namespace names identified during
  parse. Used by `DotNetEmitter.GetNamespaceFolderPath` to compute file-system
  paths.
- *NamespaceDescriptions* (`IReadOnlyDictionary<string, NamespaceDescription>`):
  Optional per-namespace documentation sourced from `NamespaceDoc` carrier types,
  each holding the winning carrier's summary/remarks/example member IDs for
  on-demand, link-aware rendering.
- *Resolver* (`TypeLinkResolver`): Type link resolver initialized with the
  root namespaces for gradual-disclosure output.
- *Options* (`DotNetGeneratorOptions`): Generator configuration options
  including assembly path, XML doc path, visibility, and obsolete filter.
- *CrefTargets* (`CrefTargetResolver`): Visibility-agnostic index resolving a
  raw `<see cref>`/`<seealso cref>` value to the type or member it names when
  that symbol is declared in this assembly. Built eagerly in the constructor
  (needs only `Assembly`).
- *MemberPageIndex* (`IReadOnlyDictionary<string, string>`): Global index
  mapping a member's XML-doc cref ID to its gradual-disclosure page path
  (`"{folder}/{fileNameNoExt}"`), covering every member that will actually be
  emitted as a page. Empty until `SetMemberPageIndex` is called; used by
  `TypeLinkResolver.LinkifyResolvedMember`.
- *EmittedTypeIds* (`IReadOnlySet<string>`): Set of XML-doc type IDs for every
  type that will actually be emitted as a page in gradual-disclosure mode
  (top-level visible types plus visible nested types, transitively). Empty
  until `SetMemberPageIndex` is called.

**TypePageWriteContext** (internal sealed record): Bundles the per-type-page
writing context that is constant across all member pages generated for a single
type. Reduces parameter counts on helper methods that emit individual member
pages and table rows.

- *Factory* (`IMarkdownWriterFactory`): The factory for creating per-file writers.
- *NamespaceName* (`string`): The full namespace name of the type.
- *NamespaceFolderPath* (`string`): Pre-computed folder path for the namespace.
- *Type* (`TypeDefinition`): The type definition being documented.
- *XmlDocs* (`XmlDocReader`): Documentation index for member lookups.
- *Resolver* (`TypeLinkResolver`): Type link resolver for table cells.
- *LinkContext* (`CrefLinkContext`): Cross-reference resolution context for
  `<see cref>`/`<seealso cref>` linking, scoped to this type's own page
  folder (`NamespaceFolderPath`). Member pages living in a deeper folder
  derive their own context from this one via a `with` expression overriding
  `CrefLinkContext.CurrentFolder`.

**MethodDocContext** (internal sealed record): Bundles the per-method
documentation writing context passed to `DotNetEmitterGradualDisclosure` so
callers do not need to thread five constant parameters through each call site.

- *NamespaceName* (`string`): Namespace of the owning type.
- *XmlDocs* (`XmlDocReader`): Documentation index.
- *Resolver* (`TypeLinkResolver`): Type link resolver.
- *CurrentFolder* (`string`): Folder path of the containing Markdown file.
- *ExternalTypes* (`ISet<ExternalTypeInfo>`): Accumulator for external type references found during table cell generation.
- *LinkContext* (`CrefLinkContext`): Cross-reference resolution context for
  `<see cref>`/`<seealso cref>` linking, scoped to `CurrentFolder`.

**NamespaceDocContext** (internal sealed record): Bundles the per-assembly
namespace documentation context that is constant across all namespace page
writes in a single generation run.

- *AllNamespaces* (`IReadOnlyList<string>`): All namespaces in alphabetical order.
- *ByNamespace* (`IReadOnlyDictionary<string, IReadOnlyList<TypeDefinition>>`): Types grouped by namespace.
- *RootNamespaces* (`IReadOnlyList<string>`): Root namespaces for path computation.
- *NamespaceDescriptions* (`IReadOnlyDictionary<string, NamespaceDescription>`): Optional per-namespace documentation.
- *XmlDocs* (`XmlDocReader`): Documentation index.
- *Resolver* (`TypeLinkResolver`): Type link resolver.

**NamespaceDescription** (internal sealed record): Bundles the namespace-level
documentation sourced from a `NamespaceDoc` carrier class, as member IDs rather
than pre-rendered text, so rendering can be deferred until a `CrefLinkContext`
scoped to the correct output folder is available (namespace descriptions are
selected early in `DotNetGenerator.Parse`, before the cref-link index exists).

- *SummaryMemberId* (`string?`): Member ID of the carrier whose `<summary>` won,
  or `null` when absent.
- *RemarksMemberId* (`string?`): Member ID of the carrier whose `<remarks>` won,
  or `null` when absent.
- *ExampleMemberId* (`string?`): Member ID of the carrier whose `<example>` won,
  or `null` when absent.
- *GetSummary(XmlDocReader, CrefLinkContext?)* / *GetRemarks(XmlDocReader,
  CrefLinkContext?)* / *GetExampleParts(XmlDocReader, CrefLinkContext?)*:
  On-demand accessors that resolve the corresponding member ID's text via
  `XmlDocReader.GetSummary`/`GetRemarks`/`GetExampleParts`, optionally
  resolving `<see cref>`/`<seealso cref>` references to Markdown links when a
  `CrefLinkContext` is supplied; return `null`/empty when no member ID was set.

**DotNetAstModelArgs** (internal sealed record): Bundles the `DotNetAstModel`
constructor's own parameters into a single value, so the constructor takes one
parameter instead of nine and the DotNetAstModelArgs record's own XML doc
comments remain the authoritative per-field description.

- *Assembly* (`AssemblyDefinition`), *AssemblyResolver* (`IAssemblyResolver`),
  *XmlDocs* (`XmlDocReader`), *AllNamespaces* (`IReadOnlyList<string>`),
  *ByNamespace* (`IReadOnlyDictionary<string, IReadOnlyList<TypeDefinition>>`),
  *RootNamespaces* (`IReadOnlyList<string>`),
  *NamespaceDescriptions* (`IReadOnlyDictionary<string, NamespaceDescription>`),
  *Resolver* (`TypeLinkResolver`), *Options* (`DotNetGeneratorOptions`) — one
  field per constructor parameter of `DotNetAstModel`, described above under
  the `DotNetAstModel` entry.

### Key Methods

**DotNetAstModel constructor**: Accepts a `DotNetAstModelArgs` bundling all
parsed data and stores each field in a read-only property.

- *Parameters*: `DotNetAstModelArgs args`.
- *Preconditions*: No field of `args` may be null.
- *Postconditions*: All properties are initialized from `args`; no further
  mutation of the assigned properties is possible, aside from the deferred
  `MemberPageIndex`/`EmittedTypeIds` population performed by
  `SetMemberPageIndex` below.

**SetMemberPageIndex**: Sets `MemberPageIndex` and `EmittedTypeIds` after
construction. Called exactly once, by `DotNetGenerator.Parse`, immediately
after the owning `DotNetEmitter` has been constructed — deferred because the
index depends on visibility rules that are instance methods on
`DotNetEmitter`, which does not exist yet while the model's constructor runs.

- *Parameters*: `IReadOnlyDictionary<string, string> memberPageIndex`,
  `IReadOnlySet<string> emittedTypeIds`.
- *Postconditions*: `MemberPageIndex` and `EmittedTypeIds` reflect the
  supplied values for the remainder of the emit run.

### Error Handling

DotNetAstModel does not throw after construction. All validation is the
responsibility of `DotNetGenerator.Parse` before constructing the model.

### Dependencies

- **Mono.Cecil** — AssemblyDefinition, TypeDefinition, and IAssemblyResolver are
  Mono.Cecil types.
- **XmlDocReader** — held by reference for per-member documentation lookups.
- **TypeLinkResolver** — held by reference for type-to-link resolution.
- **CrefTargetResolver** — held by reference (`CrefTargets`) for cref-target
  resolution; constructed eagerly by the model constructor itself.

### Callers

- **DotNetGenerator.Parse** — constructs DotNetAstModel and returns it wrapped
  in a DotNetEmitter.
- **DotNetEmitter** — holds a DotNetAstModel reference and passes it to the
  sub-emitters.
- **DotNetEmitterGradualDisclosure** — reads all model properties during
  gradual-disclosure emission.
- **DotNetEmitterSingleFile** — reads all model properties during single-file
  emission.

### External Interfaces

N/A — this is an internal class with no external interfaces exposed beyond its assembly.
