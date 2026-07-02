## TypeLinkResolver

### Verification Approach

`TypeLinkResolver` is unit-tested in `test/ApiMark.DotNet.Tests/` using real
Mono.Cecil type references obtained from the fixture assembly. Tests verify that
each resolution path (primitive alias, intra-assembly link, plain text with
external tracking, nullable annotation, array suffix, generic container) produces
the expected Markdown string. An empty `HashSet<ExternalTypeInfo>` is supplied
as the accumulator for tests that inspect external-type tracking. No mocking is
required.

### Test Environment

Tests require the compiled fixture assembly so that real Mono.Cecil type
references can be obtained. No external service, network dependency, or writable
output location is needed.

### Acceptance Criteria

- All `TypeLinkResolver` tests pass with zero failures.
- A null type reference returns an empty string.
- `System.Int32` resolves to the C# alias `int`.
- `System.String` resolves to the C# alias `string`.
- A System-namespace external type (e.g. `System.IO.Stream`) is returned as plain text and is not tracked as an external dependency.
- A non-System external type (e.g. `Acme.Widgets.Widget`) is returned as plain text and is added to the external types accumulator.
- An intra-assembly type produces a Markdown link when `generateLinks` is `true`.
- An intra-assembly type produces plain text when `generateLinks` is `false`.
- A nullable generic parameter appends `?` when `isNullableAnnotated` is `true`.
- A `Nullable<T>` value type is unwrapped and rendered as the inner type's C# alias followed by `?` (e.g., `Nullable<int>` renders as `int?`).
- A nullable intra-assembly type produces a Markdown link followed by a `?` suffix.
- An array type appends the array rank suffix (e.g., `[]`) to the element type name.
- A multi-dimensional array type appends the multi-dimensional rank suffix (e.g., `[,]` for rank-2) to the element type name.
- A nullable array type appends the array rank suffix followed by the `?` suffix (e.g., `string[]?`).
- A generic container type appends angle-bracket notation listing the resolved type arguments.

### Test Scenarios

**Null type reference returns an empty string**: Verifies that `Linkify` returns
an empty string rather than throwing for a null input. This scenario is tested by
`TypeLinkResolver_Linkify_NullTypeRef_ReturnsEmptyString`.

**System.Int32 resolves to the C# alias "int"**: Verifies that the primitive alias
table is applied for `System.Int32`. This scenario is tested by
`TypeLinkResolver_Linkify_Int32_ReturnsCSharpAlias`.

**System.String resolves to the C# alias "string"**: Verifies that the primitive
alias table is applied for `System.String`. This scenario is tested by
`TypeLinkResolver_Linkify_StringType_ReturnsCSharpAlias`.

**Intra-assembly type produces a Markdown link when generateLinks is true**:
Verifies that link-generation mode produces clickable cross-references. This
scenario is tested by
`TypeLinkResolver_Linkify_GenerateLinksTrue_IntraAssemblyType_ReturnsMarkdownLink`.

**Intra-assembly type produces plain text when generateLinks is false**: Verifies
that the no-link mode suppresses markup for single-file output contexts. This
scenario is tested by
`TypeLinkResolver_Linkify_GenerateLinksFalse_IntraAssemblyType_ReturnsPlainText`.

**Nullable generic parameter appends ? suffix**: Verifies that a generic type
parameter linkified with `isNullableAnnotated: true` produces a `?` suffix. This
scenario is tested by
`TypeLinkResolver_Linkify_NullableGenericParameter_AppendsQuestionMark`.

**Nullable value type unwraps to inner alias with ? suffix**: Verifies that a
`Nullable<T>` generic instance is unwrapped and rendered as the inner type's C#
alias followed by `?` (i.e., `Nullable<int>` renders as `int?`, not
`Nullable<int>`), confirming that value-type nullables are rendered in idiomatic
C# syntax. This scenario is tested by
`TypeLinkResolver_Linkify_NullableValueType_ReturnsInnerAliasWithQuestionMark`.

**Nullable intra-assembly type returns link with ? suffix**: Verifies that an
intra-assembly type reference linkified with `isNullableAnnotated: true` produces
a Markdown link ending with a `?` suffix, confirming that the nullable annotation
is applied after the cross-reference link is generated. This scenario is tested by
`TypeLinkResolver_Linkify_NullableIntraAssemblyType_ReturnsLinkWithQuestionMark`.

**Array type appends [] suffix**: Verifies that an array type reference produces
a result ending with `[]`, confirming that the array rank suffix is appended to the
element type name. This scenario is tested by
`TypeLinkResolver_Linkify_ArrayType_AppendsArraySuffix`.

**Multi-dimensional array type appends rank suffix**: Verifies that a
multi-dimensional array type reference appends the correct rank suffix to the
element type name (i.e., a rank-2 `int` array renders as `int[,]`), confirming that
array rank is reflected in the generated Markdown. This scenario is tested by
`TypeLinkResolver_Linkify_MultiDimensionalArrayType_AppendsRankSuffix`.

**Nullable array type appends array suffix then ? suffix**: Verifies that an array
type reference linkified with `isNullableAnnotated: true` produces a result ending
with `[]?` — the array rank suffix followed by the nullable marker (i.e., a
nullable `string` array renders as `string[]?`). This scenario is tested by
`TypeLinkResolver_Linkify_NullableArrayType_AppendsArraySuffixThenQuestionMark`.

**Generic type renders type arguments in angle-bracket notation**: Verifies that a
generic instance type reference (e.g., `List<string>`) produces a result containing
escaped angle brackets, confirming that the resolved type argument list is appended.
This scenario is tested by
`TypeLinkResolver_Linkify_GenericType_RendersTypeArguments`.

**System-namespace external type returns plain text and is not tracked**: Verifies
that a type reference from a `System.*` namespace (e.g. `System.IO.Stream`) is
returned as plain text and is not added to the external types accumulator, because
System types are universally known and should not appear in the External Types
section. This scenario is tested by
`TypeLinkResolver_Linkify_SystemNamespaceExternalType_ReturnsPlainTextAndDoesNotTrack`.

**Non-System external type returns plain text and is tracked**: Verifies that a
type reference from a non-System namespace (e.g. `Acme.Widgets.Widget`) is returned
as plain text and is added to the external types accumulator so that the consuming
emitter can emit an External Types section. This scenario is tested by
`TypeLinkResolver_Linkify_ExternalNonSystemType_ReturnsPlainNameAndTracksExternalType`.
