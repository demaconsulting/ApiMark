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
simplification method and holds no mutable state across calls. A private static
readonly set (`WellKnownNamespaces`) contains the namespace prefixes that are
stripped; this field is the intended place to add new prefixes as the tool evolves.

### Key Methods

**TypeNameSimplifier.Simplify**: Produces a simplified C# type name from a
Mono.Cecil TypeReference.

- *Parameters*:
  - `TypeReference typeRef` — Mono.Cecil type reference to simplify.
  - `string contextNamespace` — namespace of the type currently being documented;
    used for context-relative name shortening.
- *Returns*: `string` — simplified C# type name string.
- *Preconditions*: `typeRef` must not be null.
- *Postconditions*: The returned string is a valid C# type name that a developer
  would recognize; all applicable simplification rules have been applied.

Simplification rules applied in order:

1. **C# primitive aliases** — CLR primitive types are replaced by their C# keyword
   equivalents: `System.Int32` → `int`, `System.String` → `string`,
   `System.Boolean` → `bool`, `System.Void` → `void`, and so on for all standard
   C# keyword aliases. These aliases apply to the primitive types only; non-aliased
   types such as `System.Text.Json.JsonSerializer` retain their full name.
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

- **DotNetGenerator** — calls TypeNameSimplifier.Simplify for every type reference
  encountered while building method signatures, property types, field types, and
  parameter lists during Markdown generation, passing the current type's namespace
  as `contextNamespace`.
