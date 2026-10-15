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
- A nested type referenced as a field type resolves to the correct nested-type page path.
- A nested type referenced as a method return type resolves to the correct nested-type page path.
- A nested type referenced as a method parameter type resolves to the correct nested-type page path.
- A nested type referenced as a generic type argument resolves to the correct nested-type page path.
- A type nested two levels deep (Outer.Middle.Inner) resolves to the full ancestor-chain page path.
- Linking to a nested type from within its own parent type's page produces a shorter relative path than linking to the same nested type from an unrelated namespace/type's page.
- `LinkifyResolvedType` returns a Markdown link to a resolved intra-assembly type's page when link generation is enabled.
- `LinkifyResolvedType` returns the display text unchanged when link generation is disabled.
- `LinkifyResolvedMember` returns a Markdown link when the member is found in the supplied member-page index.
- `LinkifyResolvedMember` returns the display text unchanged when the member has no entry in the member-page index.
- `LinkifyResolvedMember` returns the display text unchanged when link generation is disabled.
- A type from a mapped external assembly links to that assembly's documentation using an http(s) base or a relative path (normalized and climbed from the current folder), for plain, generic, and nested types, and is not tracked as external.
- Assembly-name lookup is case-insensitive regardless of the caller dictionary's comparer.
- Rooted and `file:` API paths produce absolute `file:` links, and spaces/parentheses in link targets are percent-encoded.
- A type from an unmapped assembly, or any external type when `generateLinks` is `false`, stays plain text and is tracked as external.
- Nullable flags are applied per position in pre-order (generic arguments, array elements); value types never receive `?`; exhausted flags fall back to the default.
- A by-reference type resolves to its element type without a trailing `&` and consumes no nullable flag.
- A type scoped to a module other than the documented module (a base type of a referenced assembly) is treated as external: plain text and tracked when unmapped, linked into the mapped documentation when mapped, and never rendered as a link into the documented tree.

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

**Nested type as field type resolves to the correct nested-type page path**:
Verifies that a `TypeReference` for a nested type (which Mono.Cecil reports with
an empty `Namespace`), referenced as a field's type, resolves to a page path
derived from the outermost declaring ancestor's namespace plus each
declaring-type name from outermost to innermost (e.g.
`OuterClass/Inner.md`), rather than treating the empty `Namespace` as the
folder. This scenario is tested by
`TypeLinkResolver_Linkify_NestedTypeAsFieldType_ResolvesToNestedTypePage`.

**Nested type as method return type resolves to the correct nested-type page
path**: Verifies the same nested-type page-key algorithm for a method's return
type. This scenario is tested by
`TypeLinkResolver_Linkify_NestedTypeAsReturnType_ResolvesToNestedTypePage`.

**Nested type as method parameter type resolves to the correct nested-type page
path**: Verifies the same nested-type page-key algorithm for a method
parameter's type. This scenario is tested by
`TypeLinkResolver_Linkify_NestedTypeAsParameterType_ResolvesToNestedTypePage`.

**Nested type as generic type argument resolves to the correct nested-type page
path**: Verifies the same nested-type page-key algorithm when the nested type
appears as a type argument of a generic container (e.g. `List<OuterClass.Inner>`).
This scenario is tested by
`TypeLinkResolver_Linkify_NestedTypeAsGenericArgument_ResolvesToNestedTypePage`.

**Two-levels-deep nested type resolves the full ancestor path**: Verifies that a
type nested two levels deep (`TwoLevelNestedClass.Middle.Inner`) walks the
`DeclaringType` chain all the way to the outermost non-nested ancestor and
joins every intermediate level's flattened name, producing a path such as
`TwoLevelNestedClass/Middle/Inner.md`. This scenario is tested by
`TypeLinkResolver_Linkify_TwoLevelsDeepNestedType_ResolvesFullAncestorPath`.

**Linking to a nested type from its own parent page vs. from an unrelated page
produces different relative paths**: Verifies that `ComputeRelativePath`
produces a shorter relative path (e.g. `Inner.md`) when the link originates
from within the nested type's own parent type's page, compared to the longer
relative path (e.g. `OuterClass/Inner.md`) produced when the link originates
from a completely different namespace/type's page — confirming the page-key
construction is independent of the link's source location and only the
relative-path computation varies. This scenario is tested by
`TypeLinkResolver_Linkify_NestedType_FromParentPageVsOtherPage_ProducesDifferentRelativePaths`.

**LinkifyResolvedType returns a Markdown link when link generation is enabled**:
Verifies that a resolved intra-assembly `TypeDefinition` cref target produces a
Markdown link to its page, wrapping the caller-supplied display text, reusing
the same `GetTypePageKey`/`ComputeRelativePath` helpers `Linkify` uses for
intra-assembly `TypeReference` values. This scenario is tested by
`TypeLinkResolver_LinkifyResolvedType_GenerateLinksTrue_ReturnsMarkdownLink`.

**LinkifyResolvedType returns the display text unchanged when link generation is
disabled**: Verifies that single-file mode (`generateLinks: false`) short-circuits
`LinkifyResolvedType` to return the display text verbatim, with no link markup.
This scenario is tested by
`TypeLinkResolver_LinkifyResolvedType_GenerateLinksFalse_ReturnsDisplayTextUnchanged`.

**LinkifyResolvedMember returns a Markdown link when found in the member-page
index**: Verifies that a resolved intra-assembly member cref target, present in
the supplied `memberPageIndex`, produces a Markdown link to its page. This
scenario is tested by
`TypeLinkResolver_LinkifyResolvedMember_FoundInIndex_ReturnsMarkdownLink`.

**LinkifyResolvedMember returns the display text unchanged when not in the
member-page index**: Verifies that a member with no entry in `memberPageIndex`
(e.g. filtered out of the generated documentation by visibility) falls back to
the display text unchanged, with no link markup. This scenario is tested by
`TypeLinkResolver_LinkifyResolvedMember_NotInIndex_ReturnsDisplayTextUnchanged`.

**LinkifyResolvedMember returns the display text unchanged when link generation
is disabled**: Verifies that single-file mode (`generateLinks: false`)
short-circuits `LinkifyResolvedMember` before consulting `memberPageIndex` at
all. This scenario is tested by
`TypeLinkResolver_LinkifyResolvedMember_GenerateLinksFalse_ReturnsDisplayTextUnchanged`.

**Mapped external type links using an absolute http(s) base**: Arrange a resolver
whose mapping for `Acme.Widgets` is `https://docs.example.com/widgets/api/` and a
synthetic `Acme.Widgets.Widget` reference. Act: `Linkify` from folder `A/B`.
Expected: `[Widget](https://docs.example.com/widgets/api/Acme.Widgets/Widget.md)`,
unchanged by the current folder, and the external-types accumulator stays empty. This
scenario is tested by
`TypeLinkResolver_Linkify_MappedHttpsApiPath_ReturnsAbsoluteLinkAndSkipsTracking`.

**Mapped external type links using a relative path**: Arrange a mapping of
`.\Other\api` and call `Linkify` from folder `A/B`. Expected:
`[Widget](../../Other/api/Acme.Widgets/Widget.md)` — backslashes and the leading `./`
are normalized and the link climbs one `../` per current-folder segment. This scenario
is tested by `TypeLinkResolver_Linkify_MappedRelativeApiPath_ClimbsOutOfCurrentFolder`.

**Assembly-name lookup is case-insensitive**: Arrange an `Ordinal` caller dictionary
keyed `Acme.Widgets` and a type scoped to assembly `ACME.WIDGETS`. Expected: the link
is produced. This scenario is tested by
`TypeLinkResolver_Linkify_CaseSensitiveCallerDictionary_StillMatchesCaseInsensitively`.

**Mapped generic external type**: Arrange `Box<int>` from the mapped assembly.
Expected: `[Box](../Other/api/Acme.Widgets/Box1.md)\<int\>` and nothing tracked. This
scenario is tested by `TypeLinkResolver_Linkify_MappedGenericType_LinksContainerWithArguments`.

**Mapped nested external type**: Arrange `Outer.Inner` from the mapped assembly.
Expected: `[Inner](../Other/api/Acme.Widgets/Outer/Inner.md)`. This scenario is tested
by `TypeLinkResolver_Linkify_MappedNestedType_LinksToNestedPagePath`.

**Rooted and file: API paths**: Arrange mappings `/docs/api` and `file:///docs/api`.
Expected for both: `[Widget](file:///docs/api/Acme.Widgets/Widget.md)` — never a
relative climb. This scenario is tested by
`TypeLinkResolver_Linkify_RootedOrFileApiPath_ReturnsAbsoluteFileLink`.

**Link target escaping**: Arrange a mapping `../My Lib (v2)/api`. Expected: spaces and
parentheses are percent-encoded
(`../My%20Lib%20%28v2%29/api/Acme.Widgets/Widget.md`). This scenario is tested by
`TypeLinkResolver_Linkify_ApiPathWithSpacesAndParentheses_EscapesLinkTarget`.

**Unmapped assembly keeps table-only behavior**: Arrange a mapping for a different
assembly and a type from `Example.Gadgets`. Expected: plain `Gadget`, tracked in the
accumulator. This scenario is tested by
`TypeLinkResolver_Linkify_UnmappedAssembly_ReturnsPlainTextAndTracksType`.

**Mapping ignored when generateLinks is false**: Arrange a mapped resolver created
with `generateLinks: false`. Expected: plain `Widget`, tracked. This scenario is
tested by
`TypeLinkResolver_Linkify_MappedAssemblyWithGenerateLinksFalse_ReturnsPlainTextAndTracksType`.

**End-to-end mapped link**: The generator-level scenario is
`DotNetGenerator_Generate_ExternalLinksMapped_LinksTypeAndSkipsExternalTypesTable`.

**Per-position nullable flags on generic arguments**: Arrange `List<string>` and a
cursor with flags `[2, 2]`. Expected: `List\<string?\>?`. This scenario is tested by
`TypeLinkResolver_Linkify_CursorGenericArgumentFlags_AnnotatesEachPosition`.

**Per-position nullable flags on arrays**: Arrange `string[]` and flags `[1, 2]`.
Expected: `string?[]` (element annotated, array not). This scenario is tested by
`TypeLinkResolver_Linkify_CursorArrayFlags_AnnotatesElementNotArray`.

**Value types never receive ?**: Arrange `int` with cursor flag `2`, and separately
with the legacy `isNullableAnnotated: true`. Expected: `int` in both cases. This
scenario is tested by `TypeLinkResolver_Linkify_AnnotatedValueType_DoesNotAppendQuestionMark`.

**Exhausted flags fall back to the cursor default**: Arrange `List<string>` with the
single-flag cursor from the boolean overload (`true`). Expected: `List\<string\>?` —
the argument position is oblivious. This scenario is tested by
`TypeLinkResolver_Linkify_CursorExhausted_LaterPositionsUnannotated`.

**By-reference types**: Arrange a `ByReferenceType` wrapping an intra-assembly type.
Expected: the link uses the plain type name and path (`[SampleClass](SampleClass.md)`),
never a name with a trailing `&`. Arrange a by-reference `string` with flags `[2]`.
Expected: `string?` — the wrapper consumes no flag. These scenarios are tested by
`TypeLinkResolver_Linkify_ByReferenceType_ReturnsLinkWithPlainTypeName` and
`TypeLinkResolver_Linkify_CursorWithByReference_ByRefConsumesNoFlag`.

**Global namespace type links to the global-namespace folder**: Arrange an intra-assembly type
reference with an empty namespace. Act: call `Linkify`. Expected:
`[GlobalNamespaceClass](global-namespace/GlobalNamespaceClass.md)`. Tested by
`TypeLinkResolver_Linkify_GlobalNamespaceType_LinksToGlobalNamespacePage`.

**Tuples and flag positions in row cells**: Arrange the `NullableSignatureMatrix` fixture. Act by
emitting gradual disclosure. Expected: tuple return types such as `(int Id, string? Name)` and
nullable delegate type arguments render with the same annotations as the signature block. Tested by
`DotNetEmitterGradualDisclosure_Emit_NullableMatrixAndParams_OverviewRowsMatchSignature`.

**External generic types**: Arrange `ExternalGenericConsumer` (properties typed
`ExternalGenericFormat<Dictionary<string, List<int>>>`, `<int[]>` and a nested format). Act by
emitting gradual disclosure. Expected: External Types rows use aliases, nested and array arguments,
with escaped angle brackets. Tested by `DotNetEmitterGradualDisclosure_Emit_ExternalGenericTypes_AreSimplifiedAndEscaped`.

**External crefs**: Arrange `ExternalCrefClass` (summary crefs to `ExternalBaseClass`, its `DescribeBase`
member, and `System.String`) with the external fixture mapped to a relative path, mapped to an `https` URL,
and unmapped. Expected: the external type and member crefs become code-span links to the mapped type page
(relative or absolute), `System.String` stays a plain code span, and an unmapped assembly leaves plain code
spans. Tested by `DotNetEmitterGradualDisclosure_Emit_ExternalCref_MappedAssemblyLinksType`,
`DotNetEmitterGradualDisclosure_Emit_ExternalCref_AbsoluteMappingLinksType`, and
`DotNetEmitterGradualDisclosure_Emit_ExternalCref_UnmappedStaysPlainCodeSpan`.

**Overload anchors in member links**: Arrange the `UndoHistory` fixture whose overloads share one page. Act by
emitting gradual disclosure and resolving member crefs. Expected: the member link ends with the overload's
heading anchor (`#transactstring-action`, `#transacttstring-funct`), and each anchor matches a real heading
on the target page. Tested by `DotNetEmitterGradualDisclosure_Emit_OverloadCref_LinksToOverloadHeadingAnchor`
and `DotNetEmitterGradualDisclosure_Emit_AllLinkAnchors_MatchRealHeadings`.

**Nested and generic-nested external crefs**: Arrange `NestedExternalConsumer` with `ExternalLinks` mapping the
external fixture assembly. Expected: crefs and property types naming `NestedHost.Inner`, `NestedHost.Deep.Leaf`,
and `NestedGenericHost<T>.Inner<U>` link to `.../NestedHost/Inner.md`, `.../NestedHost/Deep/Leaf.md`, and
`.../NestedGenericHost{1}/Inner{1}.md`. Tested by
`DotNetEmitterGradualDisclosure_Emit_ExternalNestedTypeCrefs_LinkToNestedPages` and
`DotNetEmitterGradualDisclosure_Emit_ExternalNestedPropertyTypes_LinkToNestedPages`. External link names equal
the names the target assembly's own registry produces; tested by
`DotNetEmitterGradualDisclosure_Emit_ExternalLinks_MatchTargetAssemblyRegistryNames`.

**Foreign-module types are external**: Arrange `InheritedFromExternalChain`, whose base `ExternalChainMiddle` derives from `ExternalChainTop` in the same external assembly, and emit with reference paths, first unmapped and then with `ExternalLinks` mapping the external fixture assembly. Expected: unmapped, both inherited headings are plain text; mapped, both link into the external documentation; and every relative link on every page resolves to an emitted page. Tested by `DotNetEmitterGradualDisclosure_Emit_ExternalBaseChain_Unmapped_RendersPlainTextHeadings`, `DotNetEmitterGradualDisclosure_Emit_ExternalBaseChain_Mapped_LinksEveryHeadingIntoExternalDocs`, and `DotNetEmitterGradualDisclosure_Emit_WithReferences_AllRelativeLinksResolveToEmittedPages`.
