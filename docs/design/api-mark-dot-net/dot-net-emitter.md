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
  emitted when no XML doc summary is available, and also used as the fallback
  cell value in parameter tables when a `<param>` tag carries no description text.
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

**Shared Helper Methods** (internal static): DotNetEmitter exposes shared helper
methods consumed by DotNetEmitterGradualDisclosure and DotNetEmitterSingleFile.
These helpers are grouped by concern:

- *Visibility filters* — `IsTypeVisible`, `IsMemberVisible`, `GetVisibleMembers`,
  `ShouldIncludeMember`: determine which types and members are included based on
  the configured visibility level and `IncludeObsolete` flag.
- *ID and file-name builders* — `BuildMemberId`, `BuildMethodId`,
  `BuildMemberFileName`, `BuildMethodFileName`, `GetMethodGroupName`: produce the
  XML-doc member IDs and sanitized file-name segments used by both emitters.
- *Signature builders* — `BuildMemberSignature`, `BuildMethodSignature`,
  `BuildPropertySignature`, `BuildFieldSignature`, `BuildEventSignature`: produce
  the human-readable C# declaration strings written into code-fence blocks.
- *Type predicates* — `IsOperator`, `IsDelegate`, `IsSpecialNameNonConstructor`,
  `IsExtensionMethod`, `IsBackingField`, `IsNamespaceDocCarrier`: categorize
  members to drive conditional rendering paths.
- *Accessibility helpers* — `GetAccessibilityKeyword` (overloads for
  `TypeDefinition`, `MethodDefinition`, `FieldDefinition`, `PropertyDefinition`,
  `EventDefinition`): map Mono.Cecil access flags to C# keywords.
- *Type utilities* — `GetMemberTypeRef`, `IsMemberTypeNullableAnnotated`,
  `StripArity`, `SanitizeFileName`: extract type references, detect nullable
  annotations, and produce filesystem-safe name segments.

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
