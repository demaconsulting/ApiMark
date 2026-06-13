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
- An intra-assembly type produces a Markdown link when `generateLinks` is `true`.
- An intra-assembly type produces plain text when `generateLinks` is `false`.
- A nullable generic parameter appends `?` when `isNullableAnnotated` is `true`.

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
