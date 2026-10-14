## TypeNameSimplifier

![TypeNameSimplifier Structure](ApiMarkDotNetView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

TypeNameSimplifier converts Mono.Cecil type references into idiomatic, readable C#
type names for use in Markdown output. It applies a deterministic set of
simplification rules in a fixed priority order so that generated documentation
matches the naming conventions a C# developer would write by hand.

### Data Model

TypeNameSimplifier is a stateless static class: it holds no mutable state across
calls. Its data model consists of two lookup tables and the `NullabilityCursor`
helper type that is declared in the same source file and is part of this unit.

**Primitives** (`private static readonly Dictionary<string, string>`): maps full
CLR type names to their C# keyword aliases (e.g. `System.Int32` → `int`,
`System.String` → `string`, `System.Void` → `void`, and all other standard C#
keyword aliases). Checked in Rule 1, but composite arms (array, nullable, generic)
are evaluated first; primitive-alias resolution applies to leaf types only.

**WellKnownNamespaces** (`private static readonly HashSet<string>`): contains the
namespace prefixes that are stripped when simplifying the container name of
**generic instance types** inside `BuildGenericName`. Current entries:
`System.Collections.Generic` and `System.Threading.Tasks`. This field governs
namespace stripping only for generic type containers — plain (non-generic,
non-aliased) types always reduce to their simple name via the fallthrough arm of
`Simplify` regardless of namespace, so `WellKnownNamespaces` does not affect
plain type rendering. This field is the intended place to add new prefixes as
the tool evolves.

**NullabilityCursor** (`internal sealed class`, same source file): walks the
nullable-reference flags the C# compiler encodes for a member's type. The compiler
emits one flag per position of the type tree in pre-order — the outer type first,
then the array element or the generic arguments left to right. A by-reference
wrapper is not a position and consumes no flag. Flag values are `0` (oblivious),
`1` (not annotated) and `2` (annotated, rendered with `?`).

- `ForOutermostFlag(bool annotated)` — creates a cursor whose first position is
  `2` when annotated and `0` otherwise; all later positions are `0`. Used by the
  `bool` overload of `Simplify`.
- `ForAttributes(IEnumerable<CustomAttribute> attributes, byte context)` — reads the
  member's `NullableAttribute`. A single-byte argument applies to every position.
  A byte-array argument is indexed by position. With no attribute (or an
  unrecognized argument) every position uses `context`, the inherited
  `NullableContext` byte.
- `Next()` — returns the flag for the next position and advances. When a flag array
  is present it is consumed in order; once the array is exhausted, and whenever
  there is no array, `Next()` returns the context default supplied at construction
  (not `0` and not the last array value).

The cursor is not thread-safe; one cursor is created per type position by the
caller (`DotNetEmitter.CreateNullability`, which resolves the inherited context from
the method, its declaring and enclosing types, the module and the assembly).

### Key Methods

**TypeNameSimplifier.Simplify (bool overload)**: Produces a simplified C# type name
from a Mono.Cecil TypeReference.

- *Parameters*:
  - `TypeReference typeRef` — Mono.Cecil type reference to simplify.
  - `string contextNamespace` — namespace of the type currently being documented;
    used for context-relative name shortening.
  - `bool isNullableAnnotated` (optional, default `false`) — when `true`, the
    outermost position is treated as annotated (flag `2`) and receives a `?`
    suffix when it is a reference type. It is a convenience wrapper that creates a
    cursor with `NullabilityCursor.ForOutermostFlag`.
- *Returns*: `string` — simplified C# type name string.
- *Preconditions*: `typeRef` must not be null.
- *Postconditions*: The returned string is a C# type name; all applicable
  simplification rules have been applied.

**TypeNameSimplifier.Simplify (cursor overload, internal)**: Same as above, but takes
a `NullabilityCursor?` instead of the boolean. Every type position (except
by-reference wrappers) consumes one flag, so annotations on generic arguments and
array elements are rendered on the correct position. A `null` cursor means no
annotations. Both overloads delegate to the private `SimplifyCore`, which
recurses through the type tree.

**TypeNameSimplifier.IsAnnotated** (internal static): decides whether a position
with a given flag renders `?`. True only when the flag is `2` and the type is not a
value type and not a generic parameter with the `struct` (not-nullable value type)
constraint. A flag of `2` inherited from `NullableContext` therefore never leaks
onto value types or struct-constrained parameters.

Simplification rules, numbered 0 through 7 (eight rules), applied in this order:

0. **By-reference unwrapping** — a Mono.Cecil `ByReferenceType` (used for `ref`,
   `out` and `in` parameters) is unwrapped to its element type, which is then
   simplified. The wrapper consumes no nullable flag. Rendering the `ref`/`out`/`in`
   keyword is the caller's responsibility (`DotNetEmitter.GetRefKindKeyword`).
1. **C# primitive aliases** — CLR primitive types are replaced by their C# keyword
   equivalents: `System.Int32` → `int`, `System.String` → `string`,
   `System.Boolean` → `bool`, `System.Void` → `void`, and so on for all standard
   C# keyword aliases. These aliases apply only to the primitive leaf types reached
   after composite arms (array, nullable, generic) have been evaluated. Plain
   (non-generic, non-aliased) types reduce to their simple name; `WellKnownNamespaces`
   governs namespace stripping for generic type containers only.
2. **Array syntax** — `System.ArrayType` with element type `T` is rendered as `T[]`
   using the Mono.Cecil `ArrayType.ElementType` property; the element type is itself
   simplified recursively. Multi-dimensional arrays use rank-aware bracket notation:
   a one-dimensional array produces `T[]`, a two-dimensional array produces `T[,]`, a
   three-dimensional array produces `T[,,]`, and so on — computed as
   `"[" + new string(',', rank - 1) + "]"`.
3. **Nullable value types** — `System.Nullable<T>` is rendered as `T?` rather than
   `Nullable<T>`; the inner type is simplified recursively.
4. **Well-known namespace stripping** — the `WellKnownNamespaces` set lists
   namespace prefixes whose types are commonly understood without qualification.
   Current entries: `System.Collections.Generic` (so `List<T>`,
   `Dictionary<TKey, TValue>`, `IEnumerable<T>` etc. appear unqualified) and
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
7. **Per-position nullable reference suffix** — every type position consumes one
   flag from the `NullabilityCursor` in pre-order (outer type, then array element or
   generic arguments left to right). A position whose flag is `2` and which is
   a reference type gets a `?` suffix, e.g. `string?`, `List<T?>?` or
   `Func<Wire, WireStyle?>?`. For arrays the array position and the element
   position are independent (`string[]?` versus `string?[]`). `Nullable<T>`
   (Rule 3) renders `T?` regardless of the flag; it consumes two flags — one for the
   `Nullable<T>` position itself and one for its type argument `T` — so any following
   positions stay aligned with the compiler's pre-order encoding. An empty (global) context
   namespace strips no namespace prefix and does not throw.

**TypeNameSimplifier.StripArity** (internal static): Removes the generic arity
suffix from a type name.

- *Parameters*: `string name` — raw type name that may contain a backtick arity
  suffix (e.g. `List\`1`).
- *Returns*: `string` — the name without the arity suffix (e.g. `List`).
- *Callers*: TypeLinkResolver (when computing external type display names and
  type page keys).

> **Note**: DotNetEmitter contains a parallel self-contained `StripArity`
> implementation that shares the same algorithm but does not delegate to this
> method.

**TypeNameSimplifier.FlattenArity** (internal static): Converts the IL backtick
arity suffix to a plain numeric suffix, producing a file-system-safe name that
still distinguishes generic types by parameter count.

- *Parameters*: `string name` — raw IL type name that may contain a backtick arity
  suffix (e.g. `Foo\`2`).
- *Returns*: `string` — the name with the backtick removed but the arity count
  preserved (e.g. `Foo2`). Unchanged when no backtick is present.
- *Callers*: TypeLinkResolver (when computing type page keys via `GetTypePageKey`),
  and DotNetEmitter (via the `DotNetEmitter.FlattenArity` delegate wrapper) when
  building namespace page links, type page names, member page subfolder paths, and
  operator page paths. This list is non-exhaustive — any code in the DotNet system
  that constructs file-system paths for types may call this method.

### Error Handling

TypeNameSimplifier performs no explicit validation and has no fallback path. It
does not throw for well-formed Mono.Cecil type references, and a null
`contextNamespace` is tolerated (no context prefix is stripped); an empty
`contextNamespace` (a type in the global namespace) likewise strips nothing. For an unexpected
reference shape (for example a type with an empty name) the simple-name fallthrough
arm returns whatever name the reference carries; a null `typeRef` raises a
`NullReferenceException`, which is a caller contract violation. Callers can rely on
receiving a non-null string for any non-null reference.

### Dependencies

- **Mono.Cecil** — TypeNameSimplifier operates on Mono.Cecil `TypeReference`
  objects; it requires Mono.Cecil to be available at compile time — see Mono.Cecil
  Integration Design.

### Callers

- **DotNetEmitter** — calls TypeNameSimplifier via `BuildTypeSignature` and related
  signature-builder helper methods (including `BuildMemberSignature`, `BuildDelegateSignature`,
  `BuildPropertySignature`, `BuildFieldSignature`, `BuildEventSignature`, and
  `BuildMethodSignature`) to produce idiomatic C# declaration strings for all output pages.
- **TypeLinkResolver** — calls TypeNameSimplifier.Simplify to produce the plain-text
  display name of primitive and System types used as link display text, and calls
  TypeNameSimplifier.StripArity and FlattenArity when computing type page keys and
  external type display names.

### External Interfaces

N/A — this is an internal class with no external interfaces exposed beyond its assembly.
