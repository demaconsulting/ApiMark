## DotNetEmitter

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

DotNetEmitter implements `IApiEmitter` and acts as a dispatcher between the
two format-specific sub-emitters: `DotNetEmitterGradualDisclosure` and
`DotNetEmitterSingleFile`. It reads `EmitConfig.Format` and forwards the
factory, config, and context to the appropriate sub-emitter. It also provides
shared static helper methods (type signature builders, visibility filters,
namespace path helpers, and combined-member-page writers) used by both
sub-emitters.

### Data Model

**DotNetEmitter** holds a single `DotNetAstModel` reference (the `Model`
property), which is the pre-parsed assembly data provided by
`DotNetGenerator.Parse`. No other mutable state is held; all format-specific
behavior is delegated to the sub-emitter instances.

The class also defines three `internal const` string values shared by both
sub-emitters:

- *DescriptionColumnHeader*: `"Description"` — column header for all generated
  Markdown tables.
- *NoDescriptionPlaceholder*: `"*No description provided.*"` — placeholder
  emitted when no XML doc summary is available.
- *ConstructorMethodName*: `".ctor"` — the .NET metadata method name used for
  all instance constructors.

### Key Methods

**DotNetEmitter.Emit** (public, implements IApiEmitter): Emits the complete
Markdown documentation tree in the format specified by `config.Format`.

- *Parameters*: `IMarkdownWriterFactory factory` — factory for creating per-file
  Markdown writers; must not be null. `EmitConfig config` — output configuration.
  `IContext context` — forwarded to the selected sub-emitter.
- *Returns*: `void`
- *Algorithm*: Validates that `factory` is not null (throws `ArgumentNullException`
  otherwise); opens a `using (Model.Assembly)` block to ensure disposal; if
  `config.Format == OutputFormat.SingleFile`, creates and calls
  `DotNetEmitterSingleFile.Emit`; otherwise creates and calls
  `DotNetEmitterGradualDisclosure.Emit`.

**DotNetEmitter.GetNamespaceFolderPath** (internal static): Computes the
file-system folder path for a namespace, treating the root namespace as atomic.

- *Parameters*: `string namespaceName`, `IReadOnlyList<string> rootNamespaces`.
- *Returns*: `string` — the folder path. For a root namespace the full dotted
  name is the path segment (e.g. `ApiMark.DotNet.Fixtures`). For a child
  namespace the root prefix is kept and subsequent segments use forward slashes
  (e.g. `ApiMark.DotNet.Fixtures/Inner`).

**DotNetEmitter.BuildTypeSignature** (internal static): Builds a human-readable
C# declaration signature for a type definition.

- *Parameters*: `TypeDefinition type`, `string contextNamespace`.
- *Returns*: `string` — e.g. `public class Name`, `public interface Name<T>`,
  or `public class Name : BaseClass, IInterface`.

### Error Handling

`DotNetEmitter.Emit` throws `ArgumentNullException` when `factory` is null.
All other exceptions (Mono.Cecil I/O errors, XmlDocReader errors) propagate
unchanged to the caller. The `AssemblyDefinition` is always disposed in a
`finally` block regardless of success or failure.

### Dependencies

- **IApiEmitter** — DotNetEmitter implements this interface from ApiMarkCore.
- **EmitConfig** — DotNetEmitter reads `Format` and `HeadingDepth`.
- **IMarkdownWriterFactory** — received through `Emit`; forwarded to sub-emitters.
- **DotNetAstModel** — held by reference; passed to sub-emitters.
- **DotNetEmitterGradualDisclosure** — sub-emitter created and called by Emit.
- **DotNetEmitterSingleFile** — sub-emitter created and called by Emit.
- **TypeNameSimplifier** — used by `BuildTypeSignature` to produce idiomatic
  C# type names.

### Callers

- **DotNetGenerator.Parse** — constructs a DotNetEmitter wrapping the parsed
  DotNetAstModel and returns it to the caller as an IApiEmitter.
- **ApiMarkTask** — calls `Emit` on the returned IApiEmitter.
- **Program** — calls `Emit` on the returned IApiEmitter.
