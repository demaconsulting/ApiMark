## DotNetGenerator

![DotNetGenerator Structure](ApiMarkDotNetView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

DotNetGenerator implements IApiGenerator for C#/.NET assemblies. It reads a
compiled .dll assembly via Mono.Cecil and pairs its type and member metadata with
documentation from the associated XML documentation file. It applies visibility
filtering, builds an inheritance chain for `<inheritdoc />` resolution, and
constructs a `DotNetAstModel` wrapped in a `DotNetEmitter` — which is responsible
for format selection and Markdown writing. The split across implementation units is:

- **DotNetGenerator.cs** — thin `IApiGenerator` that parses the assembly and
  returns a `DotNetEmitter`. Also defines the `ApiVisibility` enum and the
  `DotNetGeneratorOptions` configuration value object alongside the generator
  they configure.
- **DotNetAstModel.cs** — `DotNetAstModel` data class holding all parsed
  namespace and type data, plus the context records used during page generation.
  See DotNetAstModel Design for full details.
- **DotNetEmitter.cs** — `IApiEmitter` dispatcher and shared helper methods
  (signature builders, visibility filters, XML-doc extractors).
- **DotNetEmitterGradualDisclosure.cs** — all gradual-disclosure page writers
  (namespace, type, member, operator, and external-types pages).
- **DotNetEmitterSingleFile.cs** — all single-file page writers.
- **TypeLinkResolver.cs** — resolves Mono.Cecil type references to Markdown link
  text for use in table cells. Also defines the `ExternalTypeInfo` internal record
  that is produced and consumed by the resolver.
  See TypeLinkResolver Design for full details.
- **TypeNameSimplifier.cs** — simplifies CLR type names into idiomatic C# display
  text. See TypeNameSimplifier Design for full details.
- **XmlDocReader.cs** — reads and indexes the XML documentation file for indexed
  per-member lookups. See XmlDocReader Design for full details.

### Data Model

**DotNetGeneratorOptions**: Configuration value object passed to the DotNetGenerator
constructor. All properties must be set before the constructor is called.

**DotNetGeneratorOptions.AssemblyPath**: `string` — absolute or relative path to
the compiled .NET assembly (.dll) to document.

**DotNetGeneratorOptions.XmlDocPath**: `string` — path to the XML documentation
file produced by the C# compiler alongside the assembly. Must exist on disk;
DotNetGenerator throws `FileNotFoundException` if absent.

**DotNetGeneratorOptions.Visibility**: `ApiVisibility` enum — controls which members
are included in the output. Values: `Public` (public members only),
`PublicAndProtected` (public and protected members), `All` (all members regardless
of access modifier).

**DotNetGeneratorOptions.IncludeObsolete**: `bool` — when false, types and members marked
with `[Obsolete]` are excluded from the output.

**DotNetGeneratorOptions.ExcludePatterns**: `IReadOnlyList<string>` — wildcard
(`*`) patterns identifying namespaces and types to exclude from the output.
Defaults to an empty list. Each pattern is matched (case-sensitive, ordinal)
against a candidate type's full namespace-qualified name and its containing
namespace; a match on either excludes the type.

**ExternalTypeInfo**: See `TypeLinkResolver` design for the `ExternalTypeInfo` data record.

**DotNetAstModel** (internal sealed class): Holds all pre-parsed assembly data
bridging the parse and emit phases. See DotNetAstModel Design for the full
list of properties and context records.

### Key Methods

**DotNetGenerator constructor**: Accepts and stores a DotNetGeneratorOptions
instance for use during Parse.

- *Parameters*: `DotNetGeneratorOptions options` — fully populated options object.
- *Preconditions*: `options` must not be null.
- *Postconditions*: The generator instance is ready to call Parse.

**DotNetGenerator.Parse**: Reads the assembly and XML documentation file into
memory and returns a `DotNetEmitter` ready to emit.

- *Parameters*: `IContext context` — output channel for informational messages.
  DotNetGenerator emits two progress messages during parsing: one before opening the
  assembly (naming the assembly file) and one after type collection (reporting the
  type count and namespace count) via `IContext.WriteLine`.
- *Returns*: `IApiEmitter` — a `DotNetEmitter` holding all parsed namespace, type,
  and member data.
- *Preconditions*: `AssemblyPath` and `XmlDocPath` must exist on disk; `context`
  must not be null. `Parse` throws `ArgumentNullException` when `context` is null.
- *Postconditions*: The returned emitter holds the parsed `AssemblyDefinition`,
  namespace index, an XML documentation reader, and the inheritance-chain map built
  from Mono.Cecil metadata and passed to `XmlDocReader` for bare `<inheritdoc />`
  resolution. No output files have been written.
 If `BuildInheritanceChain` or `XmlDocReader` construction throws, the
 `AssemblyDefinition` is disposed before the exception propagates (resource leak
 prevention via try/catch).
- *NamespaceDoc processing*: After collecting all visible types, `Parse` calls
  `DotNetEmitter.IsNamespaceDocCarrier` on each type. Carrier types (those named
  `NamespaceDoc` with `internal static` modifiers) are excluded from the type
  listings passed to the emitter. Their XML documentation is extracted via the
  `BuildNamespaceDescription` helper — which reads the summary, remarks, and
  structured example parts (`XmlDocReader.GetSummary`, `GetRemarks`, and
  `GetExampleParts`) using the type's XML-doc ID — and bundled into a
  `NamespaceDescription`. The result is stored in the `NamespaceDescriptions`
  dictionary of `DotNetAstModel`, keyed by namespace name, for use when writing
  namespace pages.
- *Visibility note*: At the top-level type enumeration stage, `PublicAndProtected`
  behaves identically to `Public` because C# does not permit protected top-level
  types. The distinction between `Public` and `PublicAndProtected` is applied at
  the member level by `DotNetEmitter`.
- *Exclude-pattern filtering*: `Parse` compiles `ExcludePatterns` once (via a
  private `CompileExcludePatterns` helper that converts each `*`-wildcard pattern
  to an anchored, `Regex.Escape`-based regular expression) and adds one more
  `.Where` predicate to the same filter chain that already applies visibility,
  obsolete, and compiler-generated filtering. A type is excluded when a compiled
  pattern matches its full namespace-qualified name or its containing namespace.
  Because `byNamespace` and `allNamespaces` are derived exclusively from this
  filtered type set, a namespace whose every type is excluded (or otherwise
  filtered) never becomes a dictionary key and therefore never appears in any
  generated index or page — the same mechanism that already applies to
  obsolete-only namespaces when `IncludeObsolete` is false.
- *TypeLinkResolver scope*: `TypeLinkResolver` is constructed with `rootNamespaces` only (not all namespaces), which constrains which type references can produce intra-assembly Markdown links; types whose namespace does not map to a known root namespace path fall back to plain text.
- *NamespaceDoc selection*: When multiple `NamespaceDoc` carrier types exist in the same namespace, `BuildNamespaceDescription` selects the first non-empty summary, the first non-empty remarks, and the first non-empty example parts independently via `FirstOrDefault`; the historical "first non-empty summary wins" behavior is preserved and extended to remarks and examples.

**DotNetGenerator.BuildInheritanceChain** (private static): Builds a member-ID to ordered
base-member-ID map from Mono.Cecil metadata for use during `<inheritdoc />` resolution.

- *Parameters*: `AssemblyDefinition assembly` — the parsed assembly whose types are traversed.
- *Returns*: `IReadOnlyDictionary<string, IReadOnlyList<string>>` — a map from each
  member's XML-doc ID to an ordered list of XML-doc IDs of base/interface members that
  should be checked (in priority order) when resolving a bare `<inheritdoc />`.
- *When it runs*: During `Parse`, before constructing `XmlDocReader`; the resulting map
  is passed directly to the `XmlDocReader` constructor.
- *Algorithm*:
- Iterates every `TypeDefinition` in the assembly — including nested types — using
   `assembly.MainModule.GetTypes()` (which returns all types recursively) and delegates
   to `BuildTypeInheritanceEntries`.
- `BuildTypeInheritanceEntries` calls `CollectMethodInheritanceTargets`,
    `CollectPropertyInheritanceTargets`, and `CollectEventInheritanceTargets`.
- Method targets are resolved using `FindMatchingMethodDefinition` (matches by parameter
    count and type name) and `BuildMethodIdFromReference` (reconstructs the XML-doc ID).
- Property accessor→property mapping uses `MapAccessorReferenceToPropertyId`; event
    accessor→event mapping uses `MapAccessorReferenceToEventId`.
- The ordering rule places the direct base-class override first, followed by each
    explicit or implicit interface target in declaration order.
- *Known limitation*: Complex generic signatures may not always map perfectly to XML-doc
  IDs because Mono.Cecil `FullName` uses `/` for nested-type separators whereas XML-doc
  format uses `.`; resolution failures are silently treated as a no-op. `XmlDocReader`
  degrades gracefully when a lookup finds no matching chain entry.

### Error Handling

`Parse` first checks whether `context` is null, throwing `ArgumentNullException` if
so. It then checks whether `AssemblyPath` exists on disk, throwing `FileNotFoundException`
if absent. It then checks whether `XmlDocPath` exists, throwing `FileNotFoundException` if
absent. Only after both checks pass does it invoke Mono.Cecil to open the assembly. Missing
XML documentation entries for a member produce empty documentation fields rather than an error.
`ArgumentNullException` is thrown by the `DotNetGenerator` constructor when `options`
is null.

### Dependencies

- **IApiGenerator** — DotNetGenerator implements this interface from ApiMarkCore.
- **IApiEmitter** — DotNetEmitter implements this interface from ApiMarkCore.
- **IMarkdownWriterFactory** — DotNetEmitter receives an IMarkdownWriterFactory
  through Emit and calls CreateMarkdown to obtain each IMarkdownWriter. It does
  not implement IMarkdownWriter itself. (DotNetGenerator creates DotNetEmitter,
  which owns this dependency.)
- **DotNetEmitter** — provides the static helper methods `IsNamespaceDocCarrier`,
  `IsCompilerGenerated`, `IsObsolete`, and `BuildTypeId` called by DotNetGenerator
  during Parse to classify and identify types in the assembly.
- **TypeNameSimplifier** — DotNetEmitter calls TypeNameSimplifier to convert
  Mono.Cecil type references to idiomatic C# type names in output. (DotNetGenerator
  creates DotNetEmitter, which owns this dependency.)
- **XmlDocReader** — DotNetGenerator constructs an XmlDocReader from XmlDocPath during
  Parse to parse and index the XML documentation file for indexed per-member lookups.
- **TypeLinkResolver** — constructed during Parse with `rootNamespaces` and stored in
  `DotNetAstModel.Resolver` for use during emission.
- **Mono.Cecil** — used to read assembly metadata without loading the assembly into
  the current process — see Mono.Cecil Integration Design.

### Callers

- **ApiMarkTask** — constructs DotNetGenerator from MSBuild properties and calls
  Parse, then Emit.
- **Program** — constructs DotNetGenerator from CLI options and calls Parse,
  then Emit.

### External Interfaces

DotNetGenerator implements `IApiGenerator` (from `ApiMark.Core`), which exposes `Parse(IContext)`
returning `IApiEmitter`. This contract is the integration point between the language-agnostic
Core pipeline and the .NET-specific parsing logic. See the ApiMarkCore system design for the
full `IApiGenerator` contract.
