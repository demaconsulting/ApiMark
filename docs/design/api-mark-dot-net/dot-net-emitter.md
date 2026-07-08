## DotNetEmitter

![DotNetEmitter Structure](ApiMarkDotNetView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

DotNetEmitter implements `IApiEmitter` and acts as a dispatcher between the
two format-specific sub-emitters: `DotNetEmitterGradualDisclosure` and
`DotNetEmitterSingleFile`. It reads `EmitConfig.Format` and forwards the
factory, config, and context to the appropriate sub-emitter. It also provides
shared static helper methods (type signature builders, visibility filters,
and namespace path helpers) used by both sub-emitters.

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
- *Algorithm*: Validates that `factory`, `config`, and `context` are not null (throws
  `ArgumentNullException` for any null argument); opens a `using (Model.Assembly)` block
  to ensure disposal; if `config.Format == OutputFormat.SingleFile`, creates and calls
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
- *Returns*: `string` — e.g. `public class Name`, `public abstract class Name`,
  `public interface Name<T>`, or `public class Name : BaseClass, IInterface`.
- *Algorithm (modifier selection)*: For `class` keyword types only:
  - `IsAbstract && IsSealed` → `static` modifier (C# static classes compile to
    abstract+sealed in IL).
  - `IsAbstract && !IsSealed` → `abstract` modifier.
  - `IsSealed && !IsAbstract` → `sealed` modifier.
  - Otherwise → no modifier (regular class).
  Delegates, interfaces, enums, and structs are handled by earlier branches and
  never enter the modifier-selection block.

**Shared Helper Methods** (internal static): DotNetEmitter exposes shared helper
methods consumed by DotNetEmitterGradualDisclosure and DotNetEmitterSingleFile.
These helpers are grouped by concern:

- *Namespace path helpers* — `GetNamespaceFolderPath`, `GetImmediateChildNamespaces`,
  `SplitPath`: compute namespace folder paths and enumerate direct child namespaces.
- *Visibility filters* — `IsTypeVisible`, `GetVisibleNestedTypes`, `IsMemberVisible`,
  `IsMemberPublic`, `IsMemberPublicOrProtected`, `IsPropertyPublicOrProtected`,
  `GetVisibleMembers`, `ShouldIncludeMember`: determine which types and members are
  included based on the configured visibility level and `IncludeObsolete` flag.
- *Type/member classification* — `IsOperator`, `IsSpecialNameNonConstructor`,
  `IsCompilerGeneratedField`, `IsDelegate`, `IsExtensionMethod`,
  `IsCompilerGenerated(ICustomAttributeProvider)`, `IsCompilerGenerated(TypeDefinition)`,
  `IsObsolete`, `IsNamespaceDocCarrier`: categorize types and members to drive
  conditional rendering paths.
- *ID and file-name builders* — `GetMemberDisplayName`, `BuildTypeId`, `BuildMemberId`,
  `BuildMethodId`, `GetSanitizedMemberFileName`, `BuildMethodDisplayName`,
  `BuildMethodFileName`, `GetMethodGroupDisplayName`, `GetMethodGroupName`: produce
  the XML-doc member IDs, display names, and sanitized file-name segments used by
  both emitters.
- *Signature builders* — `BuildTypeSignature`, `BuildDelegateSignature`,
  `BuildMemberSignature`, `BuildMethodSignature`, `BuildPropertySignature`,
  `BuildPropertyAccessors`, `BuildFieldSignature`, `BuildEventSignature`,
  `BuildOperatorSignature`: produce the human-readable C# declaration strings
  written into code-fence blocks. Conversion operators produce signatures of the form
  `public static implicit operator TargetType(SourceType)` or
  `public static explicit operator TargetType(SourceType)`, with the return type
  appearing after the `operator` keyword rather than before the method name.
- *Accessibility helpers* — `GetAccessibilityKeyword(MethodDefinition)`,
  `GetAccessibilityKeyword(FieldDefinition)`, `GetAccessibilityKeyword(EventDefinition)`,
  `GetOperatorCSharpName`, `GetOperatorSymbol`: map Mono.Cecil access flags to C#
  keywords; property accessibility is determined directly from get/set accessor
  visibility rather than a dedicated overload.
- *Nullable/annotation helpers* — `GetMemberTypeRef`, `IsMemberTypeNullableAnnotated`,
  `HasNullableAnnotation`: extract type references and detect nullable reference
  annotations from Mono.Cecil custom attribute data.
- *Utility* — `StripArity`, `FlattenArity`, `ToXmlDocTypeName`: strip or reformat
  generic arity markers, and convert Cecil full names to XML-doc ID encoding.

**DotNetEmitter.StripArity** (internal static): Removes the generic arity suffix (e.g.
`` `1 ``) from a type name.

- *Parameters*: `string name` — the raw IL type name that may contain a backtick arity
  suffix (e.g. `Dictionary\`2`).
- *Returns*: `string` — the name with the backtick and arity digit removed entirely
  (e.g. `Dictionary\`2` → `Dictionary`). Returns the input unchanged when no backtick is
  present.
- *Algorithm*: Locates the first backtick character in `name`; if found, returns the
  substring before it; otherwise returns the input unchanged. Does not delegate to
  `TypeNameSimplifier`; the implementation is self-contained in `DotNetEmitter`.
- *Contrast with `FlattenArity`*: `StripArity` removes the arity digit entirely (for
  display names), while `FlattenArity` removes only the backtick but preserves the digit
  (for filesystem-safe path segments).

**DotNetEmitter.FlattenArity** (internal static): Converts a generic type's IL
name to a filesystem-safe display form by removing the backtick while retaining
the arity digit.

- *Parameters*: `string name` — the raw IL type name that may contain a backtick
  arity suffix (e.g. `Dictionary\`2`).
- *Returns*: `string` — the name with the backtick removed but the arity digit
  preserved (e.g. `Dictionary\`2` → `Dictionary2`). Returns the input unchanged
  when no backtick is present.
- *Note*: This method delegates to `TypeNameSimplifier.FlattenArity`.
- *Used by*: Both sub-emitters when constructing folder and file paths for generic
  types, so that `List\`1` produces a filesystem path segment `List1` rather than
  embedding a backtick in the path.

**DotNetEmitter.BuildPropertyAccessors** (internal static): Builds the accessor
portion of a property signature.

- *Parameters*: `PropertyDefinition prop` — the property to build accessors for.
- *Returns*: `string` — the accessor string (e.g. `get; set;`, `get; init;`,
  `get; private set;`).
- *Algorithm*: Collects `get;` (when a getter exists) and `set;` or `init;` (when a
  setter exists) into a list. For the getter, a less-permissive-than-property accessor
  prefix is included; for the setter, the keyword is `init;` when the `SetMethod`
  return type carries a `RequiredModifierType` for
  `System.Runtime.CompilerServices.IsExternalInit` (the Mono.Cecil representation of a
  C# 9+ init-only setter), otherwise `set;`. An accessor prefix is included only when
  the accessor's declared accessibility is strictly less permissive than the property's
  declared accessibility.

**DotNetEmitter.ToXmlDocTypeName** (internal static): Converts a Mono.Cecil
`TypeReference` full name to an XML documentation ID string suitable for member-key
lookup in `XmlDocReader`.

- *Parameters*: `string cecilFullName` — the Mono.Cecil `TypeReference.FullName` string.
- *Returns*: `string` — the normalized XML-doc type name string.
- *Algorithm*: Strips generic arity markers (backtick + digit(s)) from type names and
  replaces square-bracket generic parameter notation (found in some Cecil full-name
  representations) with XML-doc `{` / `}` type-parameter format; normalizes
  nested-type separators from `/` (Cecil) to `.` (XML-doc format).
- *Used by*: `BuildMethodId` when constructing the XML-doc member ID for method lookups
  in `XmlDocReader`.

### Error Handling

`DotNetEmitter.Emit` throws `ArgumentNullException` when any of `factory`, `config`, or
`context` is null. All other exceptions (Mono.Cecil I/O errors, XmlDocReader errors)
propagate unchanged to the caller. The `AssemblyDefinition` is always disposed in a
`finally` block regardless of success or failure.

### Dependencies

- **IApiEmitter** — DotNetEmitter implements this interface from ApiMarkCore.
- **EmitConfig** — DotNetEmitter reads `Format` to select the sub-emitter; `HeadingDepth` is consumed by `DotNetEmitterSingleFile` to offset all heading levels and is not used by `DotNetEmitterGradualDisclosure`, which writes separate files each beginning at H1.
- **IMarkdownWriterFactory** — received through `Emit`; forwarded to sub-emitters.
- **DotNetAstModel** — held by reference; passed to sub-emitters.
- **DotNetEmitterGradualDisclosure** — sub-emitter created and called by Emit.
- **DotNetEmitterSingleFile** — sub-emitter created and called by Emit.
- **TypeNameSimplifier** — used by `BuildTypeSignature` and related signature-builder
  helpers to produce idiomatic C# type names; `DotNetEmitter.FlattenArity` delegates
  to `TypeNameSimplifier.FlattenArity` for file-system-safe generic type name segments.
- **Mono.Cecil** — DotNetEmitter uses Mono.Cecil types (`TypeDefinition`, `MethodDefinition`,
  `PropertyDefinition`, `FieldDefinition`, `EventDefinition`, `TypeReference`, `RequiredModifierType`)
  directly in the signatures and bodies of its static helper methods (signature builders,
  XML-doc ID builders, nullable annotation helpers, and member classification helpers). See
  the OTS Mono.Cecil integration design (`docs/design/ots/mono-cecil.md`).

### Callers

- **DotNetGenerator.Parse** — constructs a DotNetEmitter wrapping the parsed
  DotNetAstModel and returns it to the caller as an IApiEmitter. During Parse,
  DotNetGenerator also calls four static helpers on DotNetEmitter directly:
  `IsNamespaceDocCarrier` (to identify NamespaceDoc carrier types for exclusion
  and namespace description extraction), `IsCompilerGenerated` (to exclude
  compiler-generated types from the visible type list), `IsObsolete` (to filter
  obsolete types when `IncludeObsolete` is false), and `BuildTypeId` (to construct
  the XML-doc member ID for each NamespaceDoc carrier's summary lookup).
- **DotNetEmitterGradualDisclosure** — holds a `_emitter` reference and calls
  shared static helpers (`BuildTypeSignature`, `GetNamespaceFolderPath`,
  `GetMemberDisplayName`, `FlattenArity`, and others) throughout gradual-disclosure
  emission.
- **DotNetEmitterSingleFile** — holds a `_emitter` reference and calls shared
  static helpers (`BuildTypeSignature`, `GetNamespaceFolderPath`,
  `GetMemberDisplayName`, and others) throughout single-file emission.
- **TypeLinkResolver** — calls the static helper `GetNamespaceFolderPath` to
  compute documentation folder paths for intra-assembly type links.
- **ApiMarkTask** — calls `Emit` on the returned IApiEmitter.
- **Program** — calls `Emit` on the returned IApiEmitter.

### External Interfaces

DotNetEmitter implements `IApiEmitter` (from `ApiMark.Core`), which exposes a single
`Emit(IMarkdownWriterFactory, EmitConfig, IContext)` method. This contract is the integration
point between the language-agnostic Core pipeline and the .NET-specific emission logic.
See the ApiMarkCore system design for the full `IApiEmitter` contract.
