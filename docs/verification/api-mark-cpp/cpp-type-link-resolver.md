## CppTypeLinkResolver

### Verification Approach

`CppTypeLinkResolver` is unit-tested in `test/ApiMark.Cpp.Tests/CppTypeLinkResolverTests.cs`
using an `InMemoryMarkdownWriterFactory` substitute and pre-configured known-type
dictionaries. Tests cover: null/whitespace input pass-through, primitive-type
pass-through, `std::` type pass-through, exact qualified-name match, unambiguous
short-name fallback, ambiguous short-name non-link, qualified-reference
disambiguation, external-type tracking, qualifier stripping
(`const`/`volatile`/pointer/reference), template-prefix corruption prevention,
null `knownTypes` constructor rejection, and null `externalTypes` `Linkify`
rejection.

### Test Environment

No external services, network access, or file system access are required. Tests run with
the standard xUnit.net test runner. No clang installation is needed.

### Acceptance Criteria

- An exact qualified-name match in `knownTypes` always produces a Markdown link in the
  returned string.
- An unambiguous short-name reference (only one known type with that unqualified name)
  produces a Markdown link via the short-name fallback.
- An ambiguous short-name reference (two or more known types share the same unqualified
  name) produces plain text and does not produce a link.
- A fully-qualified reference to one of two ambiguously-named types resolves correctly
  via the exact-match path and produces the correct Markdown link.
- Primitive and `std::` types return the original string unchanged and are not added to
  the external-types set.
- A non-std, non-library type whose stripped name carries a non-empty namespace is added
  to the caller's external-types set, and the original string is returned unchanged.
- Null input returns null; whitespace-only input returns unchanged — neither causes an
  exception or external-type tracking.
- When a type name appears in a template argument position sharing a prefix with the
  resolved type (e.g., `Foo<FooBar>` where `Foo` is a known type), only the actual type
  token is wrapped in a Markdown link and the template argument is left unchanged.
- Leading `const`/`volatile` and trailing pointer/reference qualifiers are stripped from
  the type name before any lookup is performed.
- Constructor throws `ArgumentNullException` when `knownTypes` is null.
- `Linkify` throws `ArgumentNullException` when `externalTypes` is null.

### Test Scenarios

**Exact qualified match emits a link**: Verifies that when a fully-qualified type name
(e.g. `"ns::Foo"`) exactly matches a key in `knownTypes`, `Linkify` returns a string
containing a Markdown link with the short type name and the resolved relative path.
This scenario is tested by `CppTypeLinkResolver_Linkify_ExactQualifiedMatch_EmitsLink`.

**Unambiguous short name emits a link**: Verifies that when only one known type has the
unqualified name being resolved (e.g. `"Bar"` with only `"ns::Bar"` in `knownTypes`),
`Linkify` returns a Markdown link via the short-name fallback path.
This scenario is tested by `CppTypeLinkResolver_Linkify_UnambiguousShortName_EmitsLink`.

**Ambiguous short name emits plain text**: Verifies that when two known types share the
same unqualified name (e.g. `"size_type"` is both `"ns::Outer::size_type"` and
`"ns::Other::size_type"`), an unqualified reference produces plain text and no link,
preventing non-deterministic navigation.
This scenario is tested by `CppTypeLinkResolver_Linkify_AmbiguousShortName_EmitsPlainText`.

**Qualified reference to ambiguous type emits correct link**: Verifies that a
fully-qualified reference (e.g. `"ns::Outer::size_type"`) still resolves to the correct
page even when two types share the same unqualified name, because the exact-match path
takes precedence over the ambiguous short-name path.
This scenario is tested by
`CppTypeLinkResolver_Linkify_QualifiedReferenceToAmbiguousType_EmitsCorrectLink`.

**Primitive type returns unchanged**: Verifies that primitive C++ types such as `int` are
returned as plain text without being tracked as external types. Tested by
`CppTypeLinkResolver_Linkify_PrimitiveType_ReturnsUnchanged`.

**Null input returns null**: Verifies that passing a null type string returns null
without throwing. Tested by `CppTypeLinkResolver_Linkify_NullInput_ReturnsNull`.

**Whitespace input returns unchanged**: Verifies that a whitespace-only string is returned
unchanged. Tested by `CppTypeLinkResolver_Linkify_WhitespaceInput_ReturnsUnchanged`.

**std:: type returns unchanged**: Verifies that `std::` types are returned as plain text
and are not tracked as external types. Tested by
`CppTypeLinkResolver_Linkify_StdType_ReturnsUnchanged`.

**External namespaced type is tracked**: Verifies that a non-std, non-library external type
with a namespace qualifier is added to the caller's external-types set. Tested by
`CppTypeLinkResolver_Linkify_ExternalType_AddsToExternalTypesSet`.

**Qualifiers stripped before lookup**: Verifies that leading `const`/`volatile` and trailing
reference/pointer qualifiers are removed before the base name is looked up. Tested by
`CppTypeLinkResolver_Linkify_QualifiedType_StripsQualifiersBeforeLookup`.

**Template arg prefix not corrupted**: Verifies that when a type name appears as both an
intra-library type (e.g. `Foo`) and a prefix of a template argument (e.g. `FooBar`), only
the actual type token is linked and the template argument is left unchanged. Tested by
`CppTypeLinkResolver_Linkify_QualifiedTypeWithSameNamePrefixInTemplateArg_EmitsLinkWithoutCorruption`.
