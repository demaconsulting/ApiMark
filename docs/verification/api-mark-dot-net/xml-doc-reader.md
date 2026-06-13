## XmlDocReader

### Verification Approach

`XmlDocReader` is unit-tested with temporary XML documentation files written by
each test's arrange step. Each test writes a minimal XML doc file containing only
the member element required for the assertion, calls the relevant getter, and then
deletes the file in a `finally` block. No mocking is required; the class has no
injectable dependencies. Tests exercise each getter independently, plus the
constructor's file-not-found guard.

### Test Environment

Tests require write access to the temporary file system path returned by
`Path.GetTempFileName()`. No external service, network dependency, or fixture
assembly is needed.

### Acceptance Criteria

- All `XmlDocReader` tests pass with zero failures.
- `XmlDocReader` constructor throws `FileNotFoundException` for a missing file.
- `GetSummary` returns trimmed single-line text and preserves inline references.
- `GetSummary` returns `null` when the member is absent.
- `GetRemarks` returns trimmed multi-line text for a present member.
- `GetRemarks` returns `null` when the member or element is absent.
- `GetExceptions` returns all `cref` values from `<exception>` elements.
- `GetExceptionDetails` returns formatted type names and descriptions; empty-cref entries are filtered.
- `GetParams` returns parameter names and descriptions in declaration order.
- `GetReturns` returns trimmed returns text.
- `GetReturns` returns `null` when the member or element is absent.
- `GetExample` returns trimmed text; `null` for whitespace-only content.
- `GetExample` returns `null` when the member or element is absent.
- `GetExampleParts` separates prose text nodes from `<code>` elements.
- `GetExampleParts` returns an empty list when the member is absent.

### Test Scenarios

**Constructor throws FileNotFoundException for a missing file**: Verifies that
constructing an `XmlDocReader` with a non-existent path throws
`FileNotFoundException`. This scenario is tested by
`XmlDocReader_Constructor_FileDoesNotExist_ThrowsFileNotFoundException`.

**GetSummary returns trimmed text for a present member**: Verifies that leading
and trailing whitespace is removed from the summary element's text. This scenario
is tested by `XmlDocReader_GetSummary_MemberPresent_ReturnsTrimmedText`.

**GetSummary preserves inline symbol references and language keywords**: Verifies
that `<see langword="..."/>`, `<paramref name="..."/>`, and `<see cref="..."/>`
elements are rendered as readable text in the summary. This scenario is tested by
`XmlDocReader_GetSummary_WithInlineReferences_PreservesReferencedNames`.

**GetSummary returns null for an absent member**: Verifies that a member not
present in the XML doc file returns `null` rather than throwing. This scenario is
tested by `XmlDocReader_GetSummary_MemberAbsent_ReturnsNull`.

**GetRemarks returns trimmed text for a present member**: Verifies that remarks
text is returned with whitespace normalized. This scenario is tested by
`XmlDocReader_GetRemarks_MemberPresent_ReturnsTrimmedText`.

**GetRemarks returns null when the member or element is absent**: Verifies that
`GetRemarks` returns `null` when the member does not exist in the XML doc file.
This scenario is tested by `XmlDocReader_GetRemarks_MemberAbsent_ReturnsNull`.

**GetExceptions returns all cref values for a present member**: Verifies that all
`<exception cref="...">` values are returned as a list. This scenario is tested by
`XmlDocReader_GetExceptions_MemberWithExceptions_ReturnsCrefValues`.

**GetExceptionDetails returns formatted types and descriptions**: Verifies that
exception type names are formatted (type-kind prefix stripped, primitive aliases
applied) and descriptions are included. This scenario is tested by
`XmlDocReader_GetExceptionDetails_MemberWithExceptions_ReturnsFormattedTypesAndDescriptions`.

**GetParams returns names and descriptions in order**: Verifies that parameter
name and description pairs are returned in declaration order. This scenario is
tested by `XmlDocReader_GetParams_MemberWithParams_ReturnsNamesAndDescriptions`.

**GetReturns returns trimmed text for a present member**: Verifies that returns
text is returned with whitespace trimmed. This scenario is tested by
`XmlDocReader_GetReturns_MemberWithReturns_ReturnsTrimmedText`.

**GetReturns returns null when the member or element is absent**: Verifies that
`GetReturns` returns `null` when the member does not exist in the XML doc file.
This scenario is tested by `XmlDocReader_GetReturns_MemberAbsent_ReturnsNull`.

**GetExample returns trimmed text for a present member**: Verifies that example
text is returned with leading and trailing whitespace removed. This scenario is
tested by `XmlDocReader_GetExample_MemberWithExample_ReturnsTrimmedText`.

**GetExample returns null for whitespace-only content**: Verifies that an
`<example>` element containing only whitespace collapses to `null`. This scenario
is tested by `XmlDocReader_GetExample_WhitespaceOnly_ReturnsNull`.

**GetExample returns null when the member or element is absent**: Verifies that
`GetExample` returns `null` when the member does not exist in the XML doc file.
This scenario is tested by `XmlDocReader_GetExample_MemberAbsent_ReturnsNull`.

**GetExampleParts returns whole text as a code part when no code element exists**:
Verifies that when the `<example>` element has no `<code>` children, the entire
text is returned as a single code part. This scenario is tested by
`XmlDocReader_GetExampleParts_NoCodeElement_ReturnsWholeTextAsCodePart`.

**GetExampleParts separates prose from code elements**: Verifies that when both
text nodes and `<code>` children are present, prose and code are returned as
separate parts in order. This scenario is tested by
`XmlDocReader_GetExampleParts_WithCodeElement_SeparatesProseFromCode`.

**GetExampleParts returns empty list for an absent member**: Verifies that an
absent member identifier returns an empty list rather than throwing. This scenario
is tested by `XmlDocReader_GetExampleParts_MemberAbsent_ReturnsEmpty`.
