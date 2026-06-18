## XmlDocReader

### Verification Approach

`XmlDocReader` is unit-tested with temporary XML documentation files written by
each test's arrange step. Each test writes a minimal XML doc file containing only
the member element required for the assertion, calls the relevant getter, and then
deletes the file in a `finally` block. No mocking is required; the class has no
injectable dependencies. Tests exercise each getter independently, plus the
constructor's file-not-found guard, and all four `<inheritdoc />` resolution
styles (bare, `cref`, `path`, and `cref + path`).

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
- `GetExampleParts` strips common leading indentation from multi-line `<code>` blocks.
- `GetExampleParts` preserves relative indentation when lines have varying indent depths.
- `GetExampleParts` preserves blank lines inside `<code>` blocks without counting them in the indent calculation.
- `GetExampleParts` applies dedent to the whole-value fallback path when no `<code>` children are present.
- `GetExampleParts` returns an empty list when the member is absent.
- `<inheritdoc cref="..." />` resolves documentation from the explicitly named member.
- `<inheritdoc cref="..." />` returns `null` when the cref target is absent.
- `<inheritdoc cref="..." />` returns `null` on a cyclic chain without throwing.
- `<inheritdoc path="..." />` applies an XPath filter to the resolved source member.
- `<inheritdoc path="..." />` returns `null` when the path expression matches nothing.
- `<inheritdoc cref="..." path="..." />` selects filtered sections from an explicit target.
- Bare `<inheritdoc />` with an injected chain resolves to the base member.
- Bare `<inheritdoc />` without a chain returns `null`.
- Bare `<inheritdoc />` with a chain entry pointing to an absent member returns `null`.
- Multi-hop `cref` chains resolve transitively (A → B → C yields C's docs).
- A failed first-candidate traversal does not poison the visited set for subsequent candidates in a bare `<inheritdoc />` chain.

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

**GetExampleParts strips common indent from uniformly indented multi-line code**:
Verifies that when all content lines in a `<code>` block carry the same number of
leading spaces (from XML formatting), all of those spaces are stripped uniformly
so the output is flush-left. This scenario is tested by
`XmlDocReader_GetExampleParts_MultiLineCodeUniformIndent_StripsCommonIndent`.

**GetExampleParts strips common indent and preserves relative indentation in mixed-indent code**:
Verifies that when a `<code>` block contains lines with varying indentation (e.g. a
base of 8 spaces plus 4 more for inner blocks), the common 8-space prefix is stripped
and the 4-space relative indentation is preserved. This scenario is tested by
`XmlDocReader_GetExampleParts_MultiLineCodeMixedIndent_StripsCommonIndentPreservesRelative`.

**GetExampleParts single-line code has no regression**: Verifies that a single-line
`<code>` element with no leading whitespace is returned unchanged. This scenario is
tested by `XmlDocReader_GetExampleParts_SingleLineCode_NoRegression`.

**GetExampleParts preserves blank lines within code blocks**: Verifies that blank
lines inside a `<code>` block are preserved in the output and that they do not
contribute to the minimum-indentation calculation. This scenario is tested by
`XmlDocReader_GetExampleParts_CodeWithBlankLinesInMiddle_PreservesBlankLines`.

**GetExampleParts strips common indent from no-code-children fallback path**:
Verifies that when the `<example>` element has no `<code>` children and the entire
value is treated as a single code block, the same `DedentCode` logic is applied so
indented content is returned flush-left. This scenario is tested by
`XmlDocReader_GetExampleParts_NoCodeElement_IndentedContent_StripsCommonIndent`.

**GetSummary follows a cref inheritdoc reference**: Verifies that
`<inheritdoc cref="M:Target" />` causes the lookup to read the summary from the
named target member. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocWithCref_ReturnsSummaryFromTarget`.

**GetRemarks follows a cref inheritdoc reference**: Verifies that remarks
propagate from the cref target. This scenario is tested by
`XmlDocReader_GetRemarks_InheritDocWithCref_ReturnsRemarksFromTarget`.

**GetParams follows a cref inheritdoc reference**: Verifies that parameter
descriptions propagate from the cref target. This scenario is tested by
`XmlDocReader_GetParams_InheritDocWithCref_ReturnsParamsFromTarget`.

**GetReturns follows a cref inheritdoc reference**: Verifies that the returns
text propagates from the cref target. This scenario is tested by
`XmlDocReader_GetReturns_InheritDocWithCref_ReturnsReturnsFromTarget`.

**GetSummary returns null for a missing cref target**: Verifies that a cref
pointing to an absent member degrades to `null`. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocWithCref_MissingTarget_ReturnsNull`.

**GetSummary returns null on a cyclic cref chain**: Verifies that a cycle in
the `<inheritdoc cref="..." />` graph is detected and resolved to `null`
without throwing a stack overflow or infinite loop. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocWithCref_CyclicReference_ReturnsNull`.

**GetSummary applies a path XPath filter**: Verifies that `path="//summary"`
selects only the `<summary>` element from the resolved source and that
`<remarks>` or other sections are not included in the result. This scenario is
tested by
`XmlDocReader_GetSummary_InheritDocWithPath_ReturnsFilteredSummary`.

**GetSummary returns null for a non-matching path**: Verifies that an XPath
expression that matches nothing produces `null`. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocWithPath_NonMatchingPath_ReturnsNull`.

**GetSummary applies cref + path**: Verifies that a combination of an explicit
cref target and an XPath path filter returns only the filtered content from the
named target. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocWithCrefAndPath_ReturnsFilteredSummaryFromTarget`.

**GetSummary resolves bare inheritdoc using the injected chain**: Verifies that
a bare `<inheritdoc />` (no cref) follows the injected inheritance chain map to
return the base member's summary. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocBare_WithChain_ReturnsSummaryFromBase`.

**GetSummary returns null for bare inheritdoc without a chain**: Verifies that
bare `<inheritdoc />` degrades to `null` when no chain is supplied. This
scenario is tested by
`XmlDocReader_GetSummary_InheritDocBare_NoChain_ReturnsNull`.

**GetSummary returns null when the chain entry target is absent**: Verifies that
a chain entry pointing to a member not present in the XML doc file degrades to
`null`. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocBare_ChainMemberAbsent_ReturnsNull`.

**GetSummary resolves a multi-hop cref chain transitively**: Verifies that
A → B → C chains resolve C's summary for a query on A. This scenario is tested
by `XmlDocReader_GetSummary_InheritDocChained_ResolvesTransitively`.

**GetExceptions follows a cref inheritdoc reference**: Verifies that exception
cref values are inherited from the explicitly named cref target member. This
scenario is tested by
`XmlDocReader_GetExceptions_InheritDocWithCref_ReturnsExceptionsFromTarget`.

**GetExceptionDetails follows a cref inheritdoc reference**: Verifies that
exception type names and descriptions are inherited from the explicitly named
cref target member. This scenario is tested by
`XmlDocReader_GetExceptionDetails_InheritDocWithCref_ReturnsExceptionDetailsFromTarget`.

**GetExample follows a cref inheritdoc reference**: Verifies that example text
is inherited from the explicitly named cref target member. This scenario is
tested by `XmlDocReader_GetExample_InheritDocWithCref_ReturnsExampleFromTarget`.

**GetExampleParts follows a cref inheritdoc reference**: Verifies that structured
example parts (prose and code blocks) are inherited from the explicitly named
cref target member. This scenario is tested by
`XmlDocReader_GetExampleParts_InheritDocWithCref_ReturnsExamplePartsFromTarget`.

**Branch-local visited set prevents first candidate from blocking second candidate**:
Regression test for the branch-local visited-set fix. Verifies that when a bare
`<inheritdoc />` has multiple chain candidates and the first candidate traverses a
shared ancestor but fails (via a non-matching `path` filter), the second candidate
can independently resolve that same ancestor and return its documentation. Without
the branch-local fix, the shared ancestor would be marked as visited during the
first candidate's traversal, causing the second candidate to be blocked by the cycle
guard and returning `null` instead of the correct summary. This scenario is tested by
`XmlDocReader_GetSummary_InheritDocBare_MultipleChainCandidates_SecondCandidateNotBlockedByFirstsVisited`.
