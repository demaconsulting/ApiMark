## DotNetAstModel

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

DotNetAstModel is an immutable data class that holds all parsed .NET assembly
data required during the emit phase. It is created exclusively by
`DotNetGenerator.Parse` and transferred to `DotNetEmitter`. All properties are
read-only after construction and the collections they expose use read-only
interfaces (`IReadOnlyList`, `IReadOnlyDictionary`), making the model's entire
observable state immutable after construction. The emitter can safely share the
model across its internal helper methods without defensive copies.

The three context records defined in the same file — `TypePageWriteContext`,
`MethodDocContext`, and `NamespaceDocContext` — reduce parameter counts on the
helper methods by bundling constant values that are threaded through multiple
call levels. A fourth record, `NamespaceDescription`, carries the summary,
remarks, and example parts extracted from a `NamespaceDoc` carrier.

### Data Model

**DotNetAstModel** (internal sealed class): Holds all data that survives the
boundary between parse and emit.

- *Assembly* (`AssemblyDefinition`): The Mono.Cecil assembly definition held
  open for the duration of emit. Ownership is transferred to the model on
  construction; the `AssemblyDefinition` is disposed via a `using (Model.Assembly)` block in `DotNetEmitter.Emit` after the emit run completes or throws.
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
  each bundling the summary, remarks, and structured example parts.
- *Resolver* (`TypeLinkResolver`): Type link resolver initialized with the
  root namespaces for gradual-disclosure output.
- *Options* (`DotNetGeneratorOptions`): Generator configuration options
  including assembly path, XML doc path, visibility, and obsolete filter.

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

**MethodDocContext** (internal sealed record): Bundles the per-method
documentation writing context passed to `DotNetEmitterGradualDisclosure` so
callers do not need to thread five constant parameters through each call site.

- *NamespaceName* (`string`): Namespace of the owning type.
- *XmlDocs* (`XmlDocReader`): Documentation index.
- *Resolver* (`TypeLinkResolver`): Type link resolver.
- *CurrentFolder* (`string`): Folder path of the containing Markdown file.
- *ExternalTypes* (`ISet<ExternalTypeInfo>`): Accumulator for external type references found during table cell generation.

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
documentation sourced from a `NamespaceDoc` carrier class so that all three parts
surface on namespace output the same way they do for a type.

- *Summary* (`string?`): Single-line namespace summary, or `null` when absent.
- *Remarks* (`string?`): Namespace remarks text, or `null` when absent.
- *ExampleParts* (`IReadOnlyList<(bool IsCode, string Content)>`): Structured
  example parts, each flagged as code or prose; empty when no `<example>` is present.

### Key Methods

**DotNetAstModel constructor**: Accepts all parsed data and stores it in
read-only properties.

- *Parameters*: `AssemblyDefinition assembly`, `XmlDocReader xmlDocs`,
  `IReadOnlyList<string> allNamespaces`, `IReadOnlyDictionary<string, IReadOnlyList<TypeDefinition>> byNamespace`,
  `IReadOnlyList<string> rootNamespaces`, `IReadOnlyDictionary<string, NamespaceDescription> namespaceDescriptions`,
  `TypeLinkResolver resolver`, `DotNetGeneratorOptions options`.
- *Preconditions*: No parameter may be null.
- *Postconditions*: All properties are initialized; no mutation is possible.

### Error Handling

DotNetAstModel does not throw after construction. All validation is the
responsibility of `DotNetGenerator.Parse` before constructing the model.

### Dependencies

- **Mono.Cecil** — AssemblyDefinition and TypeDefinition are Mono.Cecil types.
- **XmlDocReader** — held by reference for per-member documentation lookups.
- **TypeLinkResolver** — held by reference for type-to-link resolution.

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
