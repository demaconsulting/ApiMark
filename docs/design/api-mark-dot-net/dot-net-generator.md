## DotNetGenerator

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

DotNetGenerator implements IApiGenerator for C#/.NET assemblies. It reads a
compiled .dll assembly via Mono.Cecil and pairs its type and member metadata with
documentation from the associated XML documentation file. It applies visibility
filtering, uses TypeNameSimplifier to produce idiomatic C# type names, and —
depending on `EmitConfig.Format` — either writes a gradual-disclosure Markdown
tree (one file per concept) or a single-file Markdown document through
`IMarkdownWriterFactory`. Every visible member always receives its own dedicated
detail page in gradual-disclosure mode, making all navigation paths fully
deterministic.

The implementation is split across five files in the `ApiMark.DotNet` package:

- **DotNetGenerator.cs** — thin `IApiGenerator` that parses the assembly and
  returns a `DotNetEmitter`.
- **DotNetAstModel.cs** — `DotNetAstModel` data class holding all parsed
  namespace and type data, plus the context records used during page generation.
- **DotNetEmitter.cs** — `IApiEmitter` dispatcher and shared helper methods
  (signature builders, visibility filters, XML-doc extractors).
- **DotNetEmitterGradualDisclosure.cs** — all gradual-disclosure page writers
  (namespace, type, member, operator, and external-types pages).
- **DotNetEmitterSingleFile.cs** — all single-file page writers.

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

**DotNetGeneratorOptions.IncludeObsolete**: `bool` — when false, members marked
with `[Obsolete]` are excluded from the output.

**ExternalTypeInfo** (internal record): Represents a non-standard external type
reference collected during table cell generation.

- *Properties*: `SimplifiedName` (display form, may include escaped generic
  angle brackets), `Namespace` (the type's .NET namespace).
- *Ordering*: implements `IComparable<ExternalTypeInfo>` by `SimplifiedName` so
  `SortedSet<ExternalTypeInfo>` produces alphabetically ordered tables.

### Key Methods

**DotNetGenerator constructor**: Accepts and stores a DotNetGeneratorOptions
instance for use during Parse.

- *Parameters*: `DotNetGeneratorOptions options` — fully populated options object.
- *Preconditions*: `options` must not be null; `AssemblyPath` and `XmlDocPath` must
  be non-empty strings.
- *Postconditions*: The generator instance is ready to call Parse.

**DotNetGenerator.Parse**: Reads the assembly and XML documentation file into
memory and returns a `DotNetEmitter` ready to emit.

- *Parameters*: `IContext context` — output channel for diagnostic and progress
  messages emitted during parsing.
- *Returns*: `IApiEmitter` — a `DotNetEmitter` holding all parsed namespace, type,
  and member data.
- *Preconditions*: `AssemblyPath` and `XmlDocPath` must exist on disk; `context`
  must not be null.
- *Postconditions*: The returned emitter holds the parsed `AssemblyDefinition`,
  namespace index, and XML documentation reader. No output files have been written.

**DotNetEmitter.Emit** (implements `IApiEmitter`): Writes the full Markdown output
tree using the format specified by `config.Format`.

- *Parameters*: `IMarkdownWriterFactory factory` — factory used to create each
  Markdown output file; must not be null. `EmitConfig config` — output
  configuration. `IContext context` — not used by the emitter but satisfies the
  interface contract.
- *Returns*: `void`
- *Preconditions*: `factory` must not be null.
- *Postconditions (GradualDisclosure)*: The factory has produced a complete
  Markdown tree for the configured assembly. Output file naming follows:
  - `factory.CreateMarkdown("", "api")` — assembly entrypoint.
  - `factory.CreateMarkdown(namespaceName, namespaceName)` — namespace summary.
  - `factory.CreateMarkdown(namespaceName, typeSimpleName)` — type page.
  - `factory.CreateMarkdown($"{namespaceName}/{typeSimpleName}", memberName)` —
    dedicated file for every visible member.
- *Postconditions (SingleFile)*: A single `api.md` is created via
  `factory.CreateMarkdown("", "api")` containing the full documentation tree
  at heading levels `HeadingDepth` (assembly), `HeadingDepth+1` (namespace),
  `HeadingDepth+2` (type), `HeadingDepth+3` (member). Each type section includes
  a bullet list of members (`- **Name**: summary`) before the H(depth+3) member
  sections. No group headings (Constructors, Methods, etc.) or convention appendix
  are emitted.
- The `AssemblyDefinition` is always disposed in a `finally` block after Emit
  completes or throws.

**DotNetEmitter.BuildTypeSignature** (internal static): Builds a human-readable C#
declaration signature for a type definition, including direct base class and
interface names when present.

- *Parameters*: `TypeDefinition type` — the type to represent;
  `string contextNamespace` — the namespace used to simplify base type and
  interface names.
- *Returns*: `string` — a declaration of the form `public class Name`,
  `public interface Name<T>`, or `public class Name : BaseClass, IInterface`
  when direct inheritance is present.
- *Algorithm*: Determines the keyword (`class`, `interface`, `enum`, or `struct`)
  from the type's flags; computes the `sealed` or `static` modifier for classes;
  strips generic arity from the type name and appends generic parameter names when
  present; collects the direct base class (skipping `System.Object`,
  `System.ValueType`, `System.Enum`, and `System.MulticastDelegate`) and all
  directly declared interfaces using TypeNameSimplifier to produce idiomatic C#
  names; appends `: BaseClass, IInterface` when the collected list is non-empty.

**DotNetEmitter.WriteCombinedMemberPage** (internal static): Writes a single combined
Markdown page for a group of members whose sanitized file names collide on
case-insensitive filesystems.

- *Parameters*: `IMarkdownWriterFactory factory`, `string namespaceName`,
  `string namespaceFolderPath`, `TypeDefinition type`, `string lowerKey` — the
  shared lowercase file name key used as the page file name and H3 heading,
  `IReadOnlyList<IMemberDefinition> members` — the ordered collision group (at
  least two elements), `XmlDocReader xmlDocs` — documentation index.
- *Returns*: `void`
- *Algorithm*: Creates `{namespaceFolderPath}/{type.Name}/{lowerKey}.md` via the
  factory; writes an H3 heading using `lowerKey`; for each member writes an H4
  heading of the form `{displayName} ({kindLabel})`; for `MethodDefinition`
  members delegates to `WriteMethodDocumentation`; for all other member kinds
  writes the signature, summary, returns, exceptions, remarks, and example
  sections directly.

**DotNetEmitter.IsPureMethodOverloadGroup** (internal static): Returns true when all
members in a group are methods sharing the same exact case-sensitive sanitized
file name, indicating a classical method overload group rather than a
case-insensitive collision.

- *Parameters*: `IReadOnlyList<IMemberDefinition> group` — candidate group sharing
  a lowercase key; `TypeDefinition type` — declaring type required by
  `GetSanitizedMemberFileName`.
- *Returns*: `bool` — true when every element is a `MethodDefinition` and all
  share the same exact (ordinal) file name; false otherwise.
- *Algorithm*: Returns false immediately if any element is not a
  `MethodDefinition`; computes the sanitized file name for the first element and
  checks that all remaining elements produce the same value under ordinal
  comparison.

**DotNetEmitter.GetMemberKindLabel** (internal static): Maps an `IMemberDefinition` to
a short human-readable kind string used in combined page H4 headings.

- *Parameters*: `IMemberDefinition member` — the member to classify.
- *Returns*: `string` — one of `"Field"`, `"Property"`, `"Event"`,
  `"Constructor"`, `"Method"`, or `"Member"` (fallback for unknown kinds).
- *Algorithm*: Pattern-matches on the concrete type: `FieldDefinition` → `"Field"`;
  `PropertyDefinition` → `"Property"`; `EventDefinition` → `"Event"`;
  `MethodDefinition` with name `.ctor` → `"Constructor"`; `MethodDefinition` →
  `"Method"`; all other types → `"Member"`.

**TypeLinkResolver** (internal): Resolves Mono.Cecil `TypeReference` instances
to Markdown link text for use in table cells.

- *Constructor*: Accepts `IReadOnlyList<string> rootNamespaces` — forwarded to
  `DotNetEmitter.GetNamespaceFolderPath` when computing target page paths.
- **Linkify** method: resolves a `TypeReference` to a Markdown link string.
  - *Parameters*: `TypeReference typeRef`, `string currentFolder` (path of the
    containing file), `string contextNamespace`, `ISet<ExternalTypeInfo>
    externalTypes` accumulator, optional `bool isNullableAnnotated`.
  - *Returns*: a Markdown link when the type is intra-assembly; the original
    simplified name otherwise; external non-System types are tracked in
    `externalTypes`.
  - *Rules*: `Nullable<T>` → `T?` via recursion; array types → `elementText[]`;
    generic instance types linkify the container when intra-assembly; primitives
    and `System.*` types render as plain text; non-System external types are
    added to the accumulator.
  - Intra-assembly detection: `TypeReference.Scope is ModuleDefinition`.

**DotNetEmitter.WriteExternalTypesSection** (internal static): Emits the
`## External Types` section at the bottom of a page when at least one external
type was referenced in table cells.

- *Parameters*: `IMarkdownWriter writer`, `SortedSet<ExternalTypeInfo>
  externalTypes`.
- *Algorithm*: Returns immediately when the set is empty; otherwise writes an
  H2 heading `"External Types"` and a two-column table (`Type`, `Namespace`).

### Error Handling

DotNetGenerator throws `FileNotFoundException` explicitly when XmlDocPath does not
exist on disk (checked before opening the assembly). If AssemblyPath does not exist
or is not a valid .NET assembly, Mono.Cecil raises an exception that propagates
unchanged to the caller (ApiMarkTask or Program). Missing XML documentation entries
for a member produce empty documentation fields rather than an error.
`ArgumentNullException` is thrown by `DotNetEmitter.Emit` when `factory` is null.

### Dependencies

- **IApiGenerator** — DotNetGenerator implements this interface from ApiMarkCore.
- **IApiEmitter** — DotNetEmitter implements this interface from ApiMarkCore.
- **EmitConfig** — DotNetEmitter reads `EmitConfig.Format` and
  `EmitConfig.HeadingDepth` to determine the output structure.
- **IMarkdownWriterFactory** — DotNetEmitter receives an IMarkdownWriterFactory
  through Emit and calls CreateMarkdown to obtain each IMarkdownWriter. It does
  not implement IMarkdownWriter itself.
- **TypeNameSimplifier** — DotNetEmitter calls TypeNameSimplifier to convert
  Mono.Cecil type references to idiomatic C# type names in output.
- **XmlDocReader** — DotNetGenerator constructs an XmlDocReader from XmlDocPath during
  Parse to parse and index the XML documentation file for O(1) per-member lookups.
- **Mono.Cecil** — used to read assembly metadata without loading the assembly into
  the current process — see Mono.Cecil Integration Design.

### Callers

- **ApiMarkTask** — constructs DotNetGenerator from MSBuild properties and calls
  Parse, then Emit.
- **Program** — constructs DotNetGenerator from CLI options and calls Parse,
  then Emit.
