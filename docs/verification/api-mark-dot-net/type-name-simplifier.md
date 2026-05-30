## TypeNameSimplifier

### Verification Approach

`TypeNameSimplifier` applies seven independent simplification rules to convert CLR type names into
compact, C#-friendly display text. Every rule is fully unit-testable because each input — a raw CLR
type name string — produces a deterministic simplified output string. Tests exercise each of the
seven rules independently and verify combined rule application to confirm that interactions between
rules do not produce unexpected output. No dependencies are injected; no mocking is required.

### Test Environment

N/A — standard test environment using the .NET test runner is sufficient for TypeNameSimplifier
unit tests. Tests exercise pure string-transformation logic with no file system, network, or process
dependencies.

### Acceptance Criteria

- All `TypeNameSimplifier` unit tests pass with zero failures.
- Each of the seven simplification rules produces the expected simplified name when exercised
  individually.
- Combined rule application on a type name that triggers multiple rules produces the expected
  final form.
- Edge-case inputs — null context namespace, type with no applicable rules, deeply nested generic
  arguments — do not cause exceptions and return a safe fallback.

### Test Scenarios

**Primitive type aliases are simplified to C# keywords**: Verifies that CLR primitive types such as
`System.Int32` and `System.Boolean` are simplified to their C# keyword equivalents `int` and `bool`
so generated documentation uses familiar language syntax. This scenario is tested by
`Simplify_PrimitiveTypes_ReturnsCSharpKeywords`.

**Nullable value types are rendered with the ? suffix**: Verifies that `System.Nullable{T}` is
simplified to `T?` so optional value types appear in the idiomatic C# nullable form. This scenario
is tested by `Simplify_NullableValueType_ReturnsSuffixForm`.

**Generic type arguments are recursively simplified**: Verifies that generic type names such as
`System.Collections.Generic.List{System.Int32}` are simplified to `List<int>` including recursive
simplification of each type argument. This scenario is tested by
`Simplify_GenericTypeWithPrimitiveArgument_ReturnsSimplifiedForm`.

**Array types use the C# bracket notation**: Verifies that CLR array types are simplified to
the `T[]` form rather than the verbose CLR array representation, with the element type itself
simplified. This scenario is tested by `Simplify_ArrayType_ReturnsBracketNotation`.

**Common well-known namespace types use their short names**: Verifies that types from
`System.Collections.Generic` (such as `List<T>`, `Dictionary<K,V>`, `IEnumerable<T>`) and
`System.Threading.Tasks` (such as `Task`, `Task<T>`) are simplified to their short names without
the namespace prefix so generated signatures remain compact. This scenario is tested by
`Simplify_WellKnownNamespaceType_ReturnsShortName`.

**Context namespace types drop the shared prefix**: Verifies that a type in the same namespace as
the context drops its namespace prefix entirely, and a type in a nested namespace drops only the
shared prefix. This scenario is tested by `Simplify_ContextNamespaceType_DropsSharedPrefix`.

**All seven rules apply in combination**: Verifies that a complex type name that triggers multiple
simplification rules simultaneously produces the expected combined form without rule interactions
corrupting the output. This scenario is tested by
`Simplify_ComplexTypeWithMultipleRules_ReturnsExpectedCombinedForm`.

**Unknown types are returned unchanged**: Verifies that a type name that does not match any
simplification rule is returned as-is without modification or error, ensuring that unrecognized
types are safely passed through to the output. This scenario is tested by
`Simplify_UnknownType_ReturnsInputUnchanged`.
