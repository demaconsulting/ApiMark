## TypeNameSimplifier

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

TypeNameSimplifier converts Mono.Cecil type references into idiomatic, readable C#
type names for use in Markdown output. It applies a deterministic set of
simplification rules in a fixed priority order so that generated documentation
matches the naming conventions a C# developer would write by hand.

### Data Model

N/A — TypeNameSimplifier is a stateless helper class. It exposes a single
simplification method and holds no mutable state across calls.

**Primitives** (`private static readonly Dictionary<string, string>`): maps full
CLR type names to their C# keyword aliases (e.g. `System.Int32` → `int`,
`System.String` → `string`, `System.Void` → `void`, and all other standard C#
keyword aliases). Checked in Rule 1, but composite arms (array, nullable, generic)
are evaluated first; primitive-alias resolution applies to leaf types only.

**WellKnownNamespaces** (`private static readonly HashSet<string>`): contains the
namespace prefixes that are stripped when displaying generic or plain types.
Current entries: `System.Collections.Generic` and `System.Threading.Tasks`. This
field is the intended place to add new prefixes as the tool evolves.

### Key Methods

**TypeNameSimplifier.Simplify**: Produces a simplified C# type name from a
Mono.Cecil TypeReference.

- *Parameters*:
  - `TypeReference typeRef` — Mono.Cecil type reference to simplify.
  - `string contextNamespace` — namespace of the type currently being documented;
    used for context-relative name shortening.
  - `bool isNullableAnnotated` (optional, default `false`) — when `true`, a `?`
    suffix is appended for reference types carrying a `NullableAttribute(2)`
    annotation (C# 8+ nullable reference type syntax). This information is stored
    on the containing member by the compiler, not on the `TypeReference` itself;
    callers must inspect member custom attributes and pass `true` when byte value
    2 is found.
- *Returns*: `string` — simplified C# type name string.
- *Preconditions*: `typeRef` must not be null.
- *Postconditions*: The returned string is a valid C# type name that a developer
  would recognize; all applicable simplification rules have been applied.

Simplification rules applied in order:

1. **C# primitive aliases** — CLR primitive types are replaced by their C# keyword
   equivalents: `System.Int32` → `int`, `System.String` → `string`,
   `System.Boolean` → `bool`, `System.Void` → `void`, and so on for all standard
   C# keyword aliases. These aliases apply only to the primitive leaf types reached
   after composite arms (array, nullable, generic) have been evaluated. Plain
   (non-generic, non-aliased) types reduce to their simple name; `WellKnownNamespaces`
   governs namespace stripping for generic type arguments only.
2. **Array syntax** — `System.ArrayType` with element type `T` is rendered as `T[]`
   using the Mono.Cecil `ArrayType.ElementType` property; the element type is itself
   simplified recursively.
3. **Nullable value types** — `System.Nullable<T>` is rendered as `T?` rather than
   `Nullable<T>`; the inner type is simplified recursively.
4. **Well-known namespace stripping** — the `WellKnownNamespaces` set lists
   namespace prefixes whose types are commonly understood without qualification.
   Current entries: `System.Collections.Generic` (so `List<T>`,
   `Dictionary<K,V>`, `IEnumerable<T>` etc. appear unqualified) and
   `System.Threading.Tasks` (so `Task` and `Task<T>` appear unqualified). Adding a
   new entry to `WellKnownNamespaces` is the only change required to extend this
   behavior.
5. **Context namespace stripping** — if a type's namespace matches or is nested
   under `contextNamespace`, the shared prefix is removed. For example, if the
   context is `A.B.C`, the type `A.B.C.Foo` becomes `Foo` and `A.B.C.D.Bar`
   becomes `D.Bar`. Types in unrelated namespaces are not shortened by this rule.
6. **Generic argument simplification** — generic type arguments are simplified
   recursively by applying all of the above rules, producing compact forms such as
   `List<MyType>` rather than
   `System.Collections.Generic.List<MyNamespace.MyType>`.
7. **Nullable reference type suffix** — reference types annotated as nullable by
   the Mono.Cecil nullable annotation receive a `?` suffix, e.g. `string?`.

**TypeNameSimplifier.StripArity** (internal static): Removes the generic arity
suffix from a type name.

- *Parameters*: `string name` — raw type name that may contain a backtick arity
  suffix (e.g. `List\`1`).
- *Returns*: `string` — the name without the arity suffix (e.g. `List`).
- *Callers*: TypeLinkResolver (when computing external type display names and
  type page keys), DotNetEmitterGradualDisclosure (when building type page names
  and member sanitized file names).

**TypeNameSimplifier.FlattenArity** (internal static): Converts the IL backtick
arity suffix to a plain numeric suffix, producing a file-system-safe name that
still distinguishes generic types by parameter count.

- *Parameters*: `string name` — raw IL type name that may contain a backtick arity
  suffix (e.g. `Foo\`2`).
- *Returns*: `string` — the name with the backtick removed but the arity count
  preserved (e.g. `Foo2`). Unchanged when no backtick is present.
- *Callers*: TypeLinkResolver (when computing type page keys via `GetTypePageKey`),
  and DotNetEmitterGradualDisclosure (via the `DotNetEmitter.FlattenArity` delegate
  wrapper) when building namespace page links, type page names, member page subfolder
  paths, and operator page paths. This list is non-exhaustive — any code in the
  DotNet system that constructs file-system paths for types may call this method.

### Error Handling

TypeNameSimplifier does not throw exceptions for well-formed Mono.Cecil type
references. If a type reference is in an unexpected or malformed state, Simplify
returns the unqualified type name as a safe fallback rather than throwing. Callers
can rely on always receiving a non-null string.

### Dependencies

- **Mono.Cecil** — TypeNameSimplifier operates on Mono.Cecil `TypeReference`
  objects; it requires Mono.Cecil to be available at compile time — see Mono.Cecil
  Integration Design.

### Callers

- **DotNetEmitter** — calls TypeNameSimplifier.Simplify for every type reference
  encountered while building method signatures, property types, field types, and
  parameter lists during Markdown generation, passing the current type's namespace
  as `contextNamespace`.
- **TypeLinkResolver** — calls TypeNameSimplifier.Simplify to produce the plain-text
  display name of primitive and System types used as link display text, and calls
  TypeNameSimplifier.StripArity and FlattenArity when computing type page keys and
  external type display names.
