## TypeNameSimplifier

### Verification Approach

`TypeNameSimplifier` applies seven independent simplification rules to convert CLR type names into
compact, C#-friendly display text. Every rule is fully unit-testable because each input — a raw CLR
type name string — produces a deterministic simplified output string. Tests exercise each of the
seven rules independently and verify combined rule application to confirm that interactions between
rules do not produce unexpected output. No dependencies are injected; no mocking is required.

### Test Environment

TypeNameSimplifier tests apply pure string-transformation rules and have no dependency
on the compiled fixture assembly. The fixture assembly requirement is a shared test
project dependency that does not affect TypeNameSimplifier unit tests specifically.
Tests are invoked via `dotnet test` in the standard .NET test environment. No network
or process dependencies are required.

### Acceptance Criteria

- All `TypeNameSimplifier` unit tests pass with zero failures.
- Each of the seven simplification rules produces the expected simplified name when exercised
  individually.
- All 16 C# primitive aliases (`bool`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`,
  `long`, `ulong`, `float`, `double`, `decimal`, `char`, `string`, `object`, `void`) are
  verified by dedicated `[InlineData]` cases in the primitive alias theory test.
- Combined rule application on a type name that triggers multiple rules produces the expected
  final form.
- Edge-case inputs — null context namespace, type with no applicable rules, deeply nested generic
  arguments — do not cause exceptions and return a safe fallback. The null-context-namespace edge
  case is verified by `TypeNameSimplifier_Simplify_NullContextNamespace_DoesNotThrow`. Nested-
  namespace prefix stripping is verified by
  `TypeNameSimplifier_Simplify_ContextNamespaceTypes_NestedNamespace_StripsSharedPrefix`.

### Test Scenarios

**Primitive type aliases are simplified to C# keywords**: Verifies that all 16 CLR primitive
types (`System.Boolean`, `System.Byte`, `System.SByte`, `System.Int16`, `System.UInt16`,
`System.Int32`, `System.UInt32`, `System.Int64`, `System.UInt64`, `System.Single`,
`System.Double`, `System.Decimal`, `System.Char`, `System.String`, `System.Object`, and
`System.Void`) are simplified to their respective C# keyword equivalents (`bool`, `byte`,
`sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`,
`char`, `string`, `object`, and `void`) so generated documentation uses familiar language
syntax. This scenario is tested by
`TypeNameSimplifier_Simplify_Primitives_RenderLanguageAliases`.

**Nullable value types are rendered with the ? suffix**: Verifies that `System.Nullable{T}` is
simplified to `T?` so optional value types appear in the idiomatic C# nullable form. This scenario
is tested by `TypeNameSimplifier_Simplify_NullableValueTypes_UseQuestionMarkForm`.

**Generic type arguments are recursively simplified**: Verifies that generic type names such as
`System.Collections.Generic.List{System.Int32}` are simplified to `List<int>` including recursive
simplification of each type argument. This scenario is tested by
`TypeNameSimplifier_Simplify_GenericArguments_AreSimplifiedRecursively`.

**Array types use the C# bracket notation**: Verifies that CLR array types are simplified to
the `T[]` form rather than the verbose CLR array representation, with the element type itself
simplified. This scenario is tested by `TypeNameSimplifier_Simplify_ArrayType_ReturnsBracketNotation`.

**Common well-known namespace types use their short names**: Verifies that types from
`System.Collections.Generic` (such as `List<T>`, `Dictionary<K,V>`, `IEnumerable<T>`) and
`System.Threading.Tasks` (such as `Task`, `Task<T>`) are simplified to their short names without
the namespace prefix so generated signatures remain compact. This scenario is tested by
`TypeNameSimplifier_Simplify_WellKnownNamespaceTypes_RenderWithoutNamespace`.

**Context namespace types drop the shared prefix**: Verifies that a type in the same namespace as
the context drops its namespace prefix entirely, and a type in a nested namespace drops only the
shared prefix. This scenario is tested by `TypeNameSimplifier_Simplify_ContextNamespaceTypes_RenderWithoutSharedPrefix`.

**Nullable reference annotation appends ? combined with primitive alias**: Verifies that when a
reference type carries a nullable annotation and `isNullableAnnotated` is true, the simplified name
correctly combines the primitive alias with the `?` suffix — for example `System.String` with a
nullable annotation produces `string?`; and that a non-annotated reference type does not receive a
spurious `?` suffix. This scenario is tested by
`TypeNameSimplifier_Simplify_NullableAnnotatedReferenceType_AppendsQuestionMark` and
`TypeNameSimplifier_Simplify_NonAnnotatedReferenceType_DoesNotAppendQuestionMark`.
