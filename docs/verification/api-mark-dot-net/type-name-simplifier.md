## TypeNameSimplifier

### Verification Approach

`TypeNameSimplifier` applies eight simplification rules (numbered 0 through 7: by-reference
unwrapping, primitive aliases, arrays, `Nullable<T>`, well-known namespaces, context namespace,
generic arguments, and per-position nullable reference suffix) to convert Mono.Cecil type
references into compact, C#-friendly display text. Each rule is unit-testable because a given
`TypeReference` (plus namespace context and nullable flags) produces a deterministic string. Tests
obtain type references from two sources: members of the compiled fixture assembly read with
Mono.Cecil, and type shapes synthesized directly with Mono.Cecil (by-reference, multi-dimensional
arrays, generic parameters, synthesized `NullableAttribute`/`NullableContextAttribute`). Nullable
rendering of the fixture members is cross-checked against the .NET reflection oracle
`System.Reflection.NullabilityInfoContext`. No dependencies are injected; no mocking is required.

### Test Environment

Tests depend on the compiled fixture assembly (located through `FixturePaths.GetFixtureDll()`)
and on the `ApiMark.DotNet` assembly under test, including the internal `NullabilityCursor` and
`DotNetEmitter.CreateNullability`. The oracle test also loads the fixture assembly through
reflection. Tests run via `dotnet test` in the standard .NET test environment. No network or
process dependencies are required.

### Acceptance Criteria

- All `TypeNameSimplifier` unit tests pass with zero failures.
- Each of the eight simplification rules (0 through 7) produces the expected simplified name
  when exercised individually.
- All 16 C# primitive aliases (`bool`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`,
  `long`, `ulong`, `float`, `double`, `decimal`, `char`, `string`, `object`, `void`) are
  verified by dedicated `[InlineData]` cases in the primitive alias theory test.
- Combined rule application on a type name that triggers multiple rules produces the expected
  final form.
- Edge-case inputs — null context namespace, type with no applicable rules, deeply nested generic
  arguments — do not cause exceptions. The null-context-namespace case is verified for exact
  output by `TypeNameSimplifier_Simplify_NullContextNamespace_ReturnsExactNames` (and for
  not throwing by `TypeNameSimplifier_Simplify_NullContextNamespace_DoesNotThrow`). Nested-
  namespace prefix stripping is verified by
  `TypeNameSimplifier_Simplify_ContextNamespaceTypes_NestedNamespace_StripsSharedPrefix`.
- By-reference unwrapping, multi-dimensional arrays with per-position nullability,
  struct-constrained generic parameters with an inherited flag of 2, `NullableContext`
  inheritance (type, method, explicit override), and exhausted flag arrays are each verified by a
  dedicated test.

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

**Empty context namespace strips nothing**: Verifies that an empty (global) context namespace
strips no prefix and does not throw for a global type, a namespaced type, and a generic instance. This
scenario is tested by `TypeNameSimplifier_Simplify_GlobalContextNamespace_DoesNotStripOrThrow`.

**Nullable<T> consumes two flags**: Verifies that `Nullable<T>` renders `T?` while consuming the
flags for both the wrapper and its argument, as covered by the nullable value type and per-position
scenarios above.

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

**Per-type nullability**: *Arrange*: fixture types with nullable members (diagram, mostly-nullable,
oblivious, array/nullable classes) and generic-parameter members. *Act*: render each member type
with a cursor from `DotNetEmitter.CreateNullability`. *Expected*: output matches the
`System.Reflection.NullabilityInfoContext` oracle for every compared position
(`TypeNameSimplifier_Simplify_NullabilityFixtures_MatchReflectionOracle`); a function-typed
return renders `Func<NullWire, NullWireStyle?>?`
(`TypeNameSimplifier_Simplify_FuncWithNullableResult_AnnotatesInnerAndOuter`); generic parameters
render `T?`, `List<T>` and `List<T?>?` as annotated
(`TypeNameSimplifier_Simplify_GenericParameterAnnotations_RenderQuestionMarks`).

**By-reference types are unwrapped (Rule 0)**: *Arrange*: a `ByReferenceType` wrapping a fixture
class, and synthesized by-reference wrappers around `string` and `List<string>` with nullable flags.
*Act*: simplify. *Expected*: no `&` marker appears (`SampleClass`); the wrapper consumes no flag, so
`ref string?` renders `string?` and `List<string?>` renders with flags `[1, 2]`. Tested by
`TypeNameSimplifier_Simplify_ByReferenceType_UnwrapsToElementTypeName` and
`TypeNameSimplifier_Simplify_ByReferenceWithNullability_AnnotatesElementType`.

**Multi-dimensional arrays**: *Arrange*: a rank-2 `int` array, and a rank-2 `string` array with
flags `[2, 1]` and `[1, 2]`. *Act*: simplify. *Expected*: `int[,]`; `string[,]?` (array annotated)
and `string?[,]` (element annotated). Tested by
`TypeNameSimplifier_Simplify_MultiDimensionalArray_AppendsRankSuffix` and
`TypeNameSimplifier_Simplify_MultiDimensionalArrayWithNullability_AnnotatesPerPosition`.

**Struct-constrained generic parameter with inherited flag 2**: *Arrange*: a generic parameter with
the not-nullable-value-type constraint and an unconstrained one, each with a cursor whose context
default is 2. *Act*: simplify. *Expected*: `TStruct` (no `?`) and `TPlain?`. Tested by
`TypeNameSimplifier_Simplify_StructConstrainedGenericParameter_InheritedFlagTwo_NoQuestionMark`.

**NullableContext inheritance**: *Arrange*: synthesized type and method with
`NullableContextAttribute` values and no `NullableAttribute`. *Act*: simplify return and parameter
types with `DotNetEmitter.CreateNullability`. *Expected*: a type-level context of 2 yields `string?`;
a method-level context of 1 overrides it to `string`; an explicit `NullableAttribute(1)` on the
parameter overrides the context to `string`. Tested by
`TypeNameSimplifier_Simplify_NullableContextOnType_IsInherited`,
`TypeNameSimplifier_Simplify_NullableContextOnMethod_OverridesTypeContext` and
`TypeNameSimplifier_Simplify_ExplicitNullableAttribute_OverridesContext`.

**Exhausted flag array**: *Arrange*: `List<string>` with a one-element flag array `[1]` and context
default 2. *Act*: simplify. *Expected*: `List<string?>` — the outer position uses the array flag
and the remaining position returns the context default. Tested by
`TypeNameSimplifier_Simplify_ExhaustedFlagArray_UsesContextDefault`.

**Null context namespace**: *Arrange*: a primitive, a custom generic type and `List<string>`.
*Act*: simplify with a `null` context namespace. *Expected*: exactly `string`, `Foo` and
`List<string>`. Tested by `TypeNameSimplifier_Simplify_NullContextNamespace_ReturnsExactNames`.

**Void is never nullable**: *Arrange*: `System.Void` with an annotated flag of 2. *Act*: simplify.
*Expected*: `void`. Tested by `TypeNameSimplifier_Simplify_VoidWithAnnotatedFlag_RendersVoidWithoutQuestionMark`.

**Tuples**: *Arrange*: unnamed, nullable, and eight-element `ValueTuple` types. *Act*: simplify.
*Expected*: `(int, string)`, `(int, string)?`, and a flattened eight-element tuple. Tested by
`TypeNameSimplifier_Simplify_UnnamedTuple_RendersTupleSyntax`,
`TypeNameSimplifier_Simplify_NullableTuple_RendersTupleWithQuestionMark`, and
`TypeNameSimplifier_Simplify_EightElementTuple_FlattensRest`. Named tuples and the compiler's
flag order (no flags for non-generic value types or `Nullable<T>`) are verified against compiled
fixtures by `DotNetEmitter_BuildMemberSignature_NullableMatrixAndParams_RendersExactSignature`.

**External types use short names**: Arrange `ExternalSignatureShapes<T>` (external base class, interface,
constraints, event, property, field, and method types) with the external DLL as a reference. Expected: every
signature shows `ExternalBaseClass`, `ExternalGenericFormat<string>`, and so on, never `External.X`. Tested by
`DotNetEmitterGradualDisclosure_Emit_TypeSignature_UsesShortExternalNames`,
`DotNetEmitterGradualDisclosure_Emit_MemberSignatures_UseShortExternalNames`, and
`DotNetEmitterGradualDisclosure_Emit_NoSignatureContainsExternalNamespace`.

**Generic arity file names**: Arrange `Foo`, `Foo1`, `Foo<T>`, `Foo<T1, T2>`, and `Outer<T>.Inner<U>`.
Expected: `Foo{1}.md` and `Foo1.md` coexist, namespace indexes link to the brace-arity names, nested generic
types use brace-arity folders and files, and all relative links resolve. The registry rules are verified by
`DotNetFileNamingTests` and the `SymbolNameRegistry` tests. Tested by
`DotNetEmitterGradualDisclosure_Emit_SameNamedGenericTypes_ProduceDistinctFiles`,
`DotNetEmitterGradualDisclosure_Emit_NamespaceIndex_LinksHyphenatedArityFiles`,
`DotNetEmitterGradualDisclosure_Emit_NestedGenericType_UsesHyphenatedFolderAndFile`,
`DotNetEmitterGradualDisclosure_Emit_AllPages_NoDigitSuffixFileNamesForGenericTypes`,
`DotNetEmitterGradualDisclosure_Emit_AllRelativeLinks_ResolveToEmittedPages`, and
`DotNetEmitterGradualDisclosure_Emit_CrefAndBaseTypeLinks_UseHyphenatedArityNames`. The hand-built assembly
tests in `DotNetSyntheticNamingTests` (`DotNetSyntheticNaming_HostileTypeNames_GetExactNames`,
`..._NamesLikeApi_NeverOverwriteRootIndex`, `..._MemberNamedOperators_IsDisplacedAndOperatorCheckIsStrict`,
`..._NamespaceAndTypeSamePath_NamespaceWinsTypeIsSuffixedAndLinksResolve`, `..._CaseTwins_GetSeparatePaths`,
`..._TypeLinks_UseApprovedNamesWithoutDoubleEscaping`, and
`..._AllVisibilities_PathsAreLegalAndLinksAndBreadcrumbsResolve`) assert the exact names and links.
