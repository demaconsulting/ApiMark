<!-- cspell:ignore notnull -->

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

The breadth of this static helper surface (paths, visibility, IDs, signatures, constant
escaping, nullability, and symbol classification) is intentional and left as a single unit:
every helper is a small pure function over Mono.Cecil definitions that both sub-emitters, the
generator, and the coverage checker share, and splitting them would only add indirection
without isolating any state or changing a dependency direction.

### Data Model

**DotNetEmitter** holds a single `DotNetAstModel` reference (the `Model`
property), which is the pre-parsed assembly data provided by
`DotNetGenerator.Parse`. No other mutable state is held; all format-specific
behavior is delegated to the sub-emitter instances.

The class also defines five `internal const` string values shared by both
sub-emitters (the first three below plus `GlobalNamespaceFolder` and
`GlobalNamespaceDisplayName`, used by `GetNamespaceFolderPath` and
`GetNamespaceDisplayName` for the empty global namespace):

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
  `ArgumentNullException` for any null argument); opens a `using (Model.Assembly)` /
  `using (Model.AssemblyResolver)` block to ensure both are disposed together once emit
  completes (whether it completes normally or by throwing) — the resolver may still be
  consulted for lazy metadata resolution for as long as the assembly is alive, so it must
  share the same disposal scope; if `config.Format == OutputFormat.SingleFile`, creates
  and calls `DotNetEmitterSingleFile.Emit`; otherwise creates and calls
  `DotNetEmitterGradualDisclosure.Emit`.
- *Known limitation*: this disposal makes each `DotNetEmitter` instance single-use —
  once `Emit` returns (or throws), a second call on the same instance would operate on a
  disposed `AssemblyDefinition`/`IAssemblyResolver`. This is a narrower guarantee than
  `IApiEmitter`'s documented intent that the same parsed data can drive multiple output
  formats without re-parsing. It is an accepted, pre-existing tradeoff (the single-Emit
  disposal of `Model.Assembly` predates cross-assembly `<inheritdoc/>` support and was
  only extended here to also cover `Model.AssemblyResolver`): the only current caller
  (`ApiMark.Tool`'s `Program.Main`) invokes `Emit` exactly once per parsed model, and
  eagerly releasing the Mono.Cecil-held file handles as soon as emit completes is
  preferable to holding them open for a process's entire remaining lifetime on the
  chance a caller might want a second format later.

**DotNetEmitter.GetNamespaceFolderPath** (internal static): Computes the
file-system folder path for a namespace, treating the root namespace as atomic.

- *Parameters*: `string namespaceName`, `IReadOnlyList<string> rootNamespaces`.
- *Returns*: `string` — the folder path. For a root namespace the full dotted
  name is the path segment (e.g. `ApiMark.DotNet.Fixtures`). For a child
  namespace the root prefix is kept and subsequent segments use forward slashes
  (e.g. `ApiMark.DotNet.Fixtures/Inner`). The empty (global) namespace always maps
  to the synthetic folder `global-namespace`, independent of the root list.

**DotNetEmitter.GetNamespaceDisplayName** (internal static): Returns the display name
for a namespace — `(global namespace)` for the empty namespace, otherwise the name itself.
`GetImmediateChildNamespaces("")` returns no children (the global namespace is a sibling
root) and never reports the empty namespace as a child.

**DotNetEmitter.BuildTypeSignature** (internal static): Builds a human-readable
C# declaration signature for a type definition.

- *Parameters*: `TypeDefinition type`, `string contextNamespace`.
- *Returns*: `string` — e.g. `public class Name`, `public abstract class Name`,
  `public interface Name<T>`, or `public class Name : BaseClass, IInterface`.
- *Algorithm*:
  1. A delegate type (`IsDelegate`) is dispatched to `BuildDelegateSignature` and returns
     immediately.
  2. `IsRecordType` is evaluated once. The keyword is `interface`, `enum`, `record struct` or
     `struct` (value types), or `record` or `class`.
  3. *Modifier selection*: for value types other than enums, the modifier prefix is `readonly`
     (type carries `IsReadOnlyAttribute`) followed by `ref` (type carries `IsByRefLikeAttribute`).
     For the `class` and `record` keywords only: `IsAbstract && IsSealed` → `static`
     (C# static classes compile to abstract+sealed); `IsAbstract` → `abstract`; `IsSealed` →
     `sealed`; otherwise none. Interfaces and enums receive no modifier. Records therefore
     participate in class modifier selection (`public sealed record`), and record structs
     receive the struct modifiers (`public readonly record struct`).
  4. The name is the arity-stripped type name plus `<T, ...>` for generic parameters.
  5. The base list holds the direct base type (omitting `System.Object`, `System.ValueType`,
     `System.Enum`, and `System.MulticastDelegate`) followed by directly declared interfaces.
     For a record, the interface `IEquatable<Self>` (checked by `IsSelfEquatable`) is skipped
     because the compiler implies it and source never spells it out; hand-written classes and
     other interfaces on a record are kept. Base types are simplified without nullability
     metadata, because base-type and interface references carry no nullable annotations that
     affect the rendered declaration.
  6. The result is `public {modifiers}{keyword} {name}[ : {bases}]`.

**Shared Helper Methods** (internal static): DotNetEmitter exposes shared helper
methods consumed by DotNetEmitterGradualDisclosure and DotNetEmitterSingleFile.
These helpers are grouped by concern:

- *Namespace path helpers* — `GetNamespaceFolderPath`, `GetImmediateChildNamespaces`,
  `SplitPath`: compute namespace folder paths and enumerate direct child namespaces.
- *Visibility filters* — `IsTypeVisible`, `GetVisibleNestedTypes`, `IsMemberVisible`,
  `IsMemberPublic`, `IsMemberPublicOrProtected`, `IsPropertyPublicOrProtected`,
  `GetCandidateMembers`, `GetVisibleMembers`, `ShouldIncludeMember`: determine
  which types and members are included based on the configured visibility level
  and `IncludeObsolete` flag. `GetVisibleNestedTypes` always excludes
  compiler-generated nested types (the cached-lambda class, closures/display
  classes, async/iterator state machines, etc.), even at `ApiVisibility.All`:
  they carry no source-level documentation, and several of their names contain
  characters (`<`, `>`, `|`) that are invalid in Windows file paths, which would
  otherwise break output generation. `GetCandidateMembers` returns the
  structurally eligible superset of a type's methods, properties, fields, and
  events — before any visibility/obsolete policy is applied — and is itself
  consumed both by `GetVisibleMembers` (`GetVisibleMembers(type) =>
  GetCandidateMembers(type).Where(ShouldIncludeMember)`) and directly by
  `DotNetEmitterGradualDisclosure.BuildMemberPageIndex`, which needs the same
  structural superset to build its cref-linking member-page index.
  `GetVisibleMembers` likewise excludes compiler-generated properties — most
  notably a record's synthesized `EqualityContract` property (used by the
  generated `Equals` to distinguish types in an inheritance hierarchy), which
  has no source line to attach a `<summary>` to — via the same
  `IsCompilerGenerated` check already applied to methods.
- *Type/member classification* — `IsOperator`, `IsSpecialNameNonConstructor`,
  `IsCompilerGeneratedField`, `IsDelegate`, `IsExtensionMethod`,
  `IsCompilerGenerated(ICustomAttributeProvider)`, `IsCompilerGenerated(TypeDefinition)`,
  `IsGeneratedCode(ICustomAttributeProvider)`, `IsImplicitDefaultConstructor`,
  `IsObsolete`, `IsNamespaceDocCarrier`: categorize types
  and members to drive conditional rendering paths. `IsCompilerGeneratedField`
  treats a field as compiler-generated when its name contains angle brackets
  (auto-property and primary-constructor-property backing fields) *or* it
  carries `CompilerGeneratedAttribute` — the latter check is required for a
  field-like event's backing field, which is named exactly like the event
  itself (no angle brackets) and is otherwise indistinguishable from a
  hand-written field sharing that name. `IsGeneratedCode` checks for
  `System.CodeDom.Compiler.GeneratedCodeAttribute`, which source generators
  (e.g. the `[GeneratedRegex]` partial-method generator) attach to their
  generated members; it is defined here alongside the other
  `ICustomAttributeProvider`-based attribute checks but is consumed by
  `DocumentationCoverageChecker`, not by `DotNetEmitter` itself, to accept a
  `<remarks>` as documentation for such members. `IsImplicitDefaultConstructor` is
  consumed only by `DocumentationCoverageChecker` (to exempt the compiler-synthesized
  implicit parameterless constructor from `--enforce-docs`), not by either sub-emitter —
  the generated Markdown still lists the constructor like any other member. Its algorithm:
  an instance, parameterless constructor is classified as implicit only when the module has
  loaded debug symbols and every one of its sequence-point ranges (the IL span from one
  sequence point up to the next, or the end of the method body) contains exactly one `stfld`
  instruction against an instance field of its own declaring type — each field/property
  initializer folded into the implicit constructor contributes exactly one such range. Any
  explicit constructor fails this check: a conventional `{ }` body contributes ranges with
  zero stores (its braces/statements), and a single source statement that stores more than
  one field (e.g. a tuple-deconstruction assignment) contributes a range with more than one
  store — both of which a simpler aggregate-count comparison could miss. Without loaded
  symbols (`ModuleDefinition.HasSymbols` is `false`) it conservatively returns `false`
  rather than guessing. A qualifying `stfld` is one whose field belongs to the
  constructor's own declaring type, as determined by the private helper
  `IsFieldOfDeclaringType(FieldReference, TypeDefinition)`: it compares `field.DeclaringType`
  to the constructor's declaring type by reference first, then falls back to resolving
  `field.DeclaringType` and comparing the resolved definition — mirroring how
  `DotNetGenerator`'s base-type inheritance walk treats type resolution. That resolution
  is wrapped in a try/catch for `Mono.Cecil.AssemblyResolutionException`, because a field
  inherited from a base type in an assembly that isn't supplied via `ReferencePaths` (e.g.
  a `protected` field on an external base class) is a legitimate, already-tolerated
  scenario elsewhere in the codebase; an unresolvable field is simply treated as not
  belonging to the declaring type (i.e. not a qualifying store) rather than letting the
  exception abort `--enforce-docs` checking for the whole assembly. A compiler-synthesized
  implicit default constructor always emits an IL body (at minimum, a call to the base class
  constructor), so the method returns `false` for any constructor whose
  `MethodDefinition.HasBody` is `false` (e.g. a metadata-only/reference-assembly stub, or an
  `extern`/P/Invoke-declared constructor) before even inspecting sequence points — such a
  constructor is never the compiler-synthesized one, regardless of how many (necessarily
  zero) sequence points it reports.
- *ID and file-name builders* — `GetMemberDisplayName`, `BuildTypeId`, `BuildMemberId`,
  `BuildPropertyId`, `BuildMethodId`, `GetSanitizedMemberFileName`, `BuildMethodDisplayName`,
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
  `BuildFieldSignature` appends `" = " + FormatConstantValue(field.Constant)` to the
  signature when `field.HasConstant` is `true`; this also covers enum member fields,
  because Mono.Cecil stores enum member constants using the enum's underlying
  primitive type, so the same formatter renders both plain `const` fields and enum
  members correctly.
- *Constant-value formatting* — `FormatConstantValue`, `EscapeStringLiteral`,
  `EscapeCharLiteral`, `EscapeControlCharacter`: `FormatConstantValue`
  pattern-matches the boxed constant by runtime type — `null` renders as
  `"null"`; `string` renders double-quoted and escaped char-by-char via
  `EscapeStringLiteral` (handling `\\` and `"` itself, delegating every other
  character to `EscapeControlCharacter`); `char` renders single-quoted and
  escaped via `EscapeCharLiteral` (handling `\\` and `'` itself, delegating
  every other character to `EscapeControlCharacter`); `bool` renders as
  lowercase `true`/`false`; `double`/`float` NaN and the two infinities render as
  `double.NaN`, `double.PositiveInfinity`, `double.NegativeInfinity` (and the `float.`
  equivalents) because the invariant-culture text is not valid C#; and every other numeric primitive renders via
  `Convert.ToString` with `CultureInfo.InvariantCulture` and no numeric type
  suffixes (no `L`/`f`/`m`), to keep the signature readable as plain C#-like
  literal text. `EscapeControlCharacter` is the single shared escaping
  table used by both the string and char paths: it renders the named short
  escapes (`\0`, `\a`, `\b`, `\f`, `\n`, `\r`, `\t`, `\v`) for their
  corresponding control characters, falls back to a `\uXXXX` escape for any
  other character classified as a control character by `char.IsControl`
  (covering the full Unicode control range — ASCII C0 controls, `DEL`
  (`0x7f`), and the C1 controls `U+0080`-`U+009F`) so that no raw control
  byte is ever embedded in generated documentation text, and otherwise
  returns the character unchanged.
- *Accessibility helpers* — `GetAccessibilityKeyword(MethodDefinition)`,
  `GetAccessibilityKeyword(FieldDefinition)`, `GetAccessibilityKeyword(EventDefinition)`,
  `GetOperatorCSharpName`, `GetOperatorSymbol`: map Mono.Cecil access flags to C#
  keywords; property accessibility is determined directly from get/set accessor
  visibility rather than a dedicated overload.
- *Nullable/annotation helpers* — `GetMemberTypeRef` extracts a member's primary type
  reference; `CreateNullability`, `CreateMemberTypeNullability`, and `ResolveNullableContext`
  build `NullabilityCursor` instances from Mono.Cecil custom attribute data (see the Key Methods
  entries below).
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
  display names), while `FlattenArity` replaces the backtick with a hyphen and keeps the digit
  (for filesystem-safe path segments, e.g. `Foo-1`).

**DotNetEmitter.FlattenArity** (internal static): Converts a generic type's IL
name to a filesystem-safe form by replacing the backtick with a hyphen while retaining
the arity digit.

- *Parameters*: `string name` — the raw IL type name that may contain a backtick
  arity suffix (e.g. `Dictionary\`2`).
- *Returns*: `string` — the name with the backtick replaced by a hyphen and the arity digit
  preserved (e.g. `Dictionary\`2` → `Dictionary-2`). Returns the input unchanged
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

**DotNetEmitter.IsRecordType** (internal static): Reports whether a type was declared with the
C# `record` or `record struct` keyword.

- *Parameters*: `TypeDefinition type`.
- *Returns*: `bool`.
- *Algorithm*: Interfaces and enums are never records. A value type is a record struct when it
  declares a compiler-synthesized `PrintMembers(StringBuilder)` method; a reference type is a
  record when it declares the compiler-synthesized `<Clone>$` method.

**DotNetEmitter.FormatParameterDefault** (internal static): Renders the default value of an
optional parameter as C#-like text, or `null` when there is none.

- *Parameters*: `ParameterDefinition parameter`.
- *Returns*: `string?` — the value text, or `null` for a non-optional parameter without a constant.
- *Default-value resolution order*:
  1. A metadata constant (`HasConstant`) is rendered by `FormatParameterConstant`.
  2. Otherwise, a parameter that is not optional has no default (`null`).
  3. Otherwise the parameter's attributes are scanned in order: `[DecimalConstant]` (five
     arguments; converted by bit pattern, rendered with the invariant culture), `[DateTimeConstant]`
     (rendered as `new DateTime(ticks)`), then `[DefaultParameterValue]` (the argument, unwrapped
     from a boxed `CustomAttributeArgument` when needed, rendered by `FormatParameterConstant`).
  4. An optional parameter with none of these renders `default`.
- *Constant rendering* (`FormatParameterConstant`): a `null` constant renders `default` for a
  non-nullable value type and `null` for a reference type or `Nullable<T>`; an enum (or
  `Nullable<enum>`) renders `EnumType.Member` when a literal with the same underlying value
  exists and `(EnumType)n` otherwise; everything else uses `FormatConstantValue`. Enum
  resolution uses `TryResolve` (see Error Handling).

**DotNetEmitter.WithDefault** (internal static): Appends `= value` to a rendered parameter
declaration (`bool indented` → `bool indented = true`) using `FormatParameterDefault`, or returns
the declaration unchanged when there is no default. Used by method and operator signatures only;
display names and file names never include defaults.

**DotNetEmitter.BuildGenericConstraints** (internal static): Builds the `where` clauses for a
method's generic parameters.

- *Parameters*: `IEnumerable<GenericParameter> parameters`, `string contextNamespace`.
- *Returns*: `string` — a leading-space clause per constrained parameter, or empty.
- *Algorithm*: For each parameter, constraint parts are emitted in C# order: one of `unmanaged`
  (an `IsUnmanagedAttribute` on the parameter or a constraint modreq), `struct` (not-nullable
  value type), `class` (reference type), or `notnull` (see below); then each type constraint
  simplified by `TypeNameSimplifier` (the implicit `System.ValueType` is skipped for `struct` and
  `unmanaged`); then `new()` unless `struct` or `unmanaged` already implies it.
  A parameter with no parts produces no clause.
- *notnull*: encoded only through nullable metadata. A parameter is `notnull` when it has no
  `class`, `struct`, `unmanaged`, or non-`System.Object` type constraint and its effective
  nullable annotation is `1`: its own `NullableAttribute` when present, otherwise the inherited
  `NullableContext` of its owning method or type. An unconstrained parameter in a nullable
  context carries an explicit annotation `2` and therefore renders no clause.

**DotNetEmitter.GetReturnRefKeyword** and **DotNetEmitter.GetRefKindKeyword** (internal static):
`GetReturnRefKeyword(TypeReference)` returns `"ref "` for a `ByReferenceType` return and empty
otherwise. `GetRefKindKeyword(ParameterDefinition)` returns empty for a by-value parameter and, for
a by-reference parameter, `"out "` when `IsOut`, `"in "` when `IsIn`, otherwise `"ref "`.

**DotNetEmitter.IsExtensionMethod** (internal static): Returns `true` for a static method that
carries `System.Runtime.CompilerServices.ExtensionAttribute`; signature builders use it to prefix
the first parameter with `this`.

**DotNetEmitter.CreateNullability**, **CreateMemberTypeNullability**, and
**ResolveNullableContext** (internal static): `CreateNullability(attributes, owner)` returns a
`NullabilityCursor` for one type position, combining that position's own `NullableAttribute` with
the owner's inherited context (`ResolveNullableContext`). `CreateMemberTypeNullability(member)`
selects the right attribute source for a member's primary type (method return parameter,
property, field, or event). `ResolveNullableContext(owner)` searches for the context byte in this
order: the method's own `NullableContextAttribute` (methods only), the declaring type, each
enclosing type outward, the module, and the assembly (defensive), defaulting to `0` (oblivious).
The byte meanings are `0` oblivious, `1` not annotated, `2` annotated; positions whose flag is
absent or `0` render without annotations. See `NullabilityCursor` and `TypeNameSimplifier` for how
the cursor is consumed one position at a time.

**DotNetEmitter.ToXmlDocTypeName** (internal static, two overloads): Converts a Mono.Cecil
type reference to an XML documentation ID string suitable for member-key lookup in
`XmlDocReader`.

- *`string cecilFullName` overload*: Purely textual. Strips generic arity markers
  (backtick + digit(s)) from type names and replaces angle-bracket generic
  instantiation notation (found in Cecil full-name representations) with XML-doc
  `{` / `}` type-parameter format; normalizes nested-type separators from `/` (Cecil)
  to `.` (XML-doc format); converts a trailing byref `&` to `@`. Retained for callers
  (and tests) that only have a `TypeReference.FullName` string available, but cannot
  correctly resolve a generic parameter (its `FullName` is just its source name, e.g.
  `"T"`, which carries no positional information).
- *`TypeReference typeRef` overload*: Operates on the live Cecil object graph.
  Recognizes `ByReferenceType` (recurses into the element type, appends `@`),
  `GenericParameter` (emits XML-doc positional notation — `` `N `` for a type's own
  generic parameter at position `N`, `` ``N `` for a generic method's own parameter),
  `GenericInstanceType` (recurses into each generic argument so a generic
  parameter nested inside one, e.g. the `T` in `List<T>`, is also resolved
  positionally), and `ArrayType` (recurses into the element type so a generic
  parameter used as an array element, e.g. `T[]`, is also resolved positionally;
  appends `[]` for a single-dimension array or `[0:,0:,...]`, one `0:` per
  dimension, for a multi-dimensional array); falls back to the string overload for
  the element type name itself and for any other type reference kind. Preferred
  whenever the Cecil `TypeReference` is available (i.e. everywhere except the
  dedicated unit test exercising the string overload directly).
- *Used by*: `BuildMethodId`, `BuildPropertyId`, and `DotNetGenerator.BuildMethodIdFromReference`
  when constructing XML-doc member IDs for method/property lookups in `XmlDocReader`.

#### Parameter, modifier, and accessor rendering

- `FormatParameter` is the single formatter for parameters (ref kind, `params`, nullability, name,
  default, extension `this`); signatures, delegate signatures, row names, and heading names all
  use it so they cannot drift apart. Headings use the type-only form; rows and signatures use
  names and defaults.
- `GetInheritanceModifiers` derives `virtual`, `override`, `sealed override`, and `abstract` from
  the IL flags; interface members, static members, constructors, and final newslot virtuals
  (interface implementations) get none.
- `HidesBaseMember` mirrors compiler warning CS0108: a non-override method (same name and parameter
  types), property, or event with the same name as an accessible member in the resolved base-class chain
  is rendered with `new`. Unresolvable base types are skipped. Static events render `static` from
  their add/remove accessor. Fields are deliberately out of scope: `BuildFieldSignature` does not call
  `HidesBaseMember`, so `new` is not rendered for hiding fields, because field hiding is rare and
  metadata gives no reliable marker.
- `GetAccessibilityPrefix` yields a marker such as `protected` for non-public members in
  overview rows. It deliberately omits `virtual`/`override`; those appear in the signature.
- `BuildMemberSignature` and `BuildPropertyAccessors` take an `ApiVisibility` (default `All`) and
  omit accessors not visible at that tier (private/internal setters at the public tier).

#### Inherited members, record constructors and inheritdoc placeholders

`CollectInheritedMembers` walks a type's base chain (or base interfaces for an interface) with
`TryResolve` and returns one group per declaring type. It skips constructors, operators, `Finalize`,
record-synthesized members, members invisible at the active tier, obsolete members when not requested, and
members overridden or hidden by a more-derived type (matched by name and parameter types). Implicit roots
(`Object`, `ValueType`, `Enum`, `MulticastDelegate`) are not listed. A base that cannot be resolved produces
an unresolved group, rendered as a note that names `--reference-paths`. The rows are written by both
emitters as an "Inherited Members" section; they never enter `MemberPageIndex`, `CrefLinkIndices`, or
documentation coverage, and the declaring type's link never adds to the External Types table.

`IsRecordPrimaryConstructor` identifies a record's parameterized, non-copy constructor whose parameters all
match properties; `GetMemberSummary` replaces its summary, when identical to the type summary, with
"Initializes a new instance of the `{Type}` record." (`record struct` for structs). Other constructor
summaries are kept. `GetEffectiveSummary`/`GetEffectiveSummaryMarkdown` of `XmlDocReader` supply the text
"Inherits documentation from base member." for a member with an unresolved bare `<inheritdoc/>`.

### Error Handling

`DotNetEmitter.Emit` throws `ArgumentNullException` when any of `factory`, `config`, or
`context` is null. All other exceptions (Mono.Cecil I/O errors, XmlDocReader errors)
propagate unchanged to the caller. Both the `AssemblyDefinition` and the
`IAssemblyResolver` are always disposed together, via nested `using` blocks, regardless
of success or failure — see the known single-use limitation noted under
**DotNetEmitter.Emit** above.

Signature rendering is deliberately tolerant of incomplete metadata and never throws for it:

- `TryResolve` wraps `TypeReference.Resolve()` and returns `null` when Mono.Cecil raises
  `AssemblyResolutionException` (for example an enum or `Nullable<T>` argument type that lives in
  an assembly not supplied via `ReferencePaths`). Default-value rendering then treats the type as
  a non-enum and renders the raw underlying constant instead of `EnumType.Member`.
- A malformed `[DecimalConstant]` attribute (overflowing, wrongly typed, or out-of-range scale
  arguments) renders the default marker `default` rather than throwing.
- A delegate with no `Invoke` method renders as `public delegate void Name()`.
- `IsImplicitDefaultConstructor` tolerates `AssemblyResolutionException` in the same way (see the
  classification helpers above).

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
  `GetMemberDisplayName`, `FlattenArity`, `GetCandidateMembers`, and others)
  throughout gradual-disclosure emission; `GetCandidateMembers` is called
  directly by `BuildMemberPageIndex` to obtain the same structurally eligible
  member superset used by `GetVisibleMembers`.
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

### Inherited Members, `new`, and Constraints

Inherited members are listed per base type, nearest first, at the configured tier: `Public` lists
public members, `PublicAndProtected` adds protected and protected internal members, and `All` lists every
non-private member (private members are never inherited). Overridden or hidden members are listed only at the
most-derived declaring type. A base page is linked only when `IsTypeDocumented` is true (a visible top-level type,
or a visible nested type of a documented type); otherwise the heading is a code span.

The `new` modifier is recovered from the compiler's hiding rule (CS0108) for fields, constants,
properties, events, and methods: a method hides base methods with identical parameter types and any base
non-method of the same name; other members hide any accessible base member of the same name.
Generic constraints are rebuilt from metadata flags and attributes (`notnull`, `unmanaged`, `struct`, `class`,
`class?`, type constraints with their nullable annotation, `new()`).

**Irreducible limits** (metadata does not record the source construct; the emitter prefers a missing
modifier over a wrong one):

- `new` on a member that hides nothing observable is not recorded, so it cannot be reproduced; and a hiding
  member whose base type cannot be resolved (missing reference assembly) is rendered without `new`.
- `internal` and `private protected` base members of another assembly are treated as inaccessible.
- A method that differs from a base method only by generic arity or modifiers (`ref` vs `out`) is matched by
  parameter-type full names only.
- `notnull` is inferred from a nullable annotation of 1 on an otherwise unconstrained parameter, so code
  compiled in a nullable-oblivious context loses it; `allows ref struct`, `default` constraints on overrides,
  and explicit-interface-implementation constraints are not rendered.
- Default interface method nuances (sealed or re-abstracted members) are not distinguished.
