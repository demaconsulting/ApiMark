## DotNetGenerator

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

DotNetGenerator implements IApiGenerator for C#/.NET assemblies. It reads a
compiled .dll assembly via Mono.Cecil and pairs its type and member metadata with
documentation from the associated XML documentation file. It applies visibility
filtering, uses TypeNameSimplifier to produce idiomatic C# type names, and writes
the complete gradual-disclosure Markdown tree through IMarkdownWriterFactory. Every
visible member always receives its own dedicated detail page, making all navigation
paths fully deterministic.

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

### Key Methods

**DotNetGenerator constructor**: Accepts and stores a DotNetGeneratorOptions
instance for use during Generate.

- *Parameters*: `DotNetGeneratorOptions options` — fully populated options object.
- *Preconditions*: `options` must not be null; `AssemblyPath` and `XmlDocPath` must
  be non-empty strings.
- *Postconditions*: The generator instance is ready to call Generate.

**DotNetGenerator.Generate**: Reads the assembly and XML documentation file, then
writes the full Markdown output tree.

- *Parameters*: `IMarkdownWriterFactory factory` — factory used to create each
  Markdown output file.
- *Returns*: `void`
- *Preconditions*: `AssemblyPath` and `XmlDocPath` must exist on disk; `factory`
  must not be null.
- *Postconditions*: The factory has produced a complete Markdown tree for the
  configured assembly. Output file naming follows these conventions:
  - `factory.CreateMarkdown("", "api")` — assembly entrypoint listing namespaces.
  - `factory.CreateMarkdown(namespaceName, namespaceName)` — namespace summary
    listing visible types.
  - `factory.CreateMarkdown(namespaceName, typeSimpleName)` — type page with
    grouped sub-tables (Constructors / Properties / Methods / Fields / Events),
    all members linked to their dedicated pages.
  - `factory.CreateMarkdown($"{namespaceName}/{typeSimpleName}", memberName)` —
    dedicated file for every visible member.

Execution steps: call `AssemblyDefinition.ReadAssembly(AssemblyPath)` to open the
assembly via Mono.Cecil; parse XmlDocPath and index entries by member identifier
string; filter types by Visibility and IncludeObsolete; write the assembly
entrypoint via `CreateMarkdown("", "api")`; for each visible namespace write the
namespace file; for each visible type write a dedicated page per member and emit
grouped sub-tables with links; dispose the AssemblyDefinition.

### Error Handling

DotNetGenerator throws `FileNotFoundException` explicitly when XmlDocPath does not
exist on disk (checked before opening the assembly). If AssemblyPath does not exist
or is not a valid .NET assembly, Mono.Cecil raises an exception that propagates
unchanged to the caller (ApiMarkTask or Program). Missing XML documentation entries
for a member produce empty documentation fields rather than an error.

### Dependencies

- **IApiGenerator** — DotNetGenerator implements this interface from ApiMarkCore.
- **IMarkdownWriterFactory** — DotNetGenerator receives an IMarkdownWriterFactory
  through Generate and calls CreateMarkdown to obtain each IMarkdownWriter. It does
  not implement IMarkdownWriter itself.
- **TypeNameSimplifier** — DotNetGenerator calls TypeNameSimplifier to convert
  Mono.Cecil type references to idiomatic C# type names in output.
- **XmlDocReader** — DotNetGenerator constructs an XmlDocReader from XmlDocPath during
  Generate to parse and index the XML documentation file for O(1) per-member lookups.
- **Mono.Cecil** — used to read assembly metadata without loading the assembly into
  the current process — see Mono.Cecil Integration Design.

### Callers

- **ApiMarkTask** — constructs DotNetGenerator from MSBuild properties and calls
  Generate.
- **Program** — constructs DotNetGenerator from CLI options and calls Generate.
