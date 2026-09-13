## PathHelpers

### Verification Approach

PathHelpers is verified with unit tests in `test/ApiMark.Core.Tests/PathHelpersTests.cs`.
The tests call the real implementation directly and cover normal operation, traversal
rejection, rooted-path rejection, null-argument handling, multi-segment behavior, and
on-disk case normalization. `SafePathCombine` remains `internal`, exposed to
`ApiMark.Core.Tests` through `InternalsVisibleTo`; `NormalizeCase` and `Comparer` are
`public` so they can be called directly from `ApiMark.DotNet` (`DotNetGenerator`,
`ExternalXmlDocResolver`).

### Test Environment

N/A - standard test environment using the .NET test runner is sufficient. Some
`NormalizeCase` scenarios that require two directory entries differing only by case to
coexist (exercising the ambiguous-fallback and exact-match-preference behavior) can only
run to full effect on a case-sensitive file system; on a case-insensitive file system the
second write overwrites the same physical entry as the first, so those tests detect this
at run time and skip the assertion rather than exercising a scenario the file system
cannot represent.

### Acceptance Criteria

- All `PathHelpersTests` test cases pass with zero failures.
- `SafePathCombine` returns the expected combined path for valid input, including
  nested paths, current-directory references, empty segments, multi-segment combinations,
  segments that backtrack within the base, and filenames containing `..` as a substring.
- `SafePathCombine` returns `basePath` unchanged when zero segments are supplied.
- `SafePathCombine` throws `ArgumentException` for paths that resolve outside the base
  directory after joining.
- `SafePathCombine` throws `ArgumentNullException` for null `basePath` and null segments.
- `NormalizeCase` resolves a differently-cased input path to the real on-disk casing of
  an existing file/directory, for both leaf and intermediate segments.
- `NormalizeCase` canonicalizes a Windows drive-letter root prefix so two differently
  cased spellings of the same drive normalize identically.
- `NormalizeCase` falls back to the caller-supplied casing for segments that do not
  exist on disk, rather than throwing.
- `NormalizeCase` prefers an exact (ordinal) match over an incidental case-insensitive
  one when two entries differing only by case coexist in the same directory.
- `NormalizeCase` preserves the caller-supplied casing (refuses to guess) when a
  supplied segment matches neither of two coexisting, differently-cased entries exactly.
- `NormalizeCase` correctly resolves multiple sibling paths when a caller-supplied
  `directoryEntryCache` is shared across calls.

### Test Scenarios

**PathHelpers_SafePathCombine_ValidPaths_CombinesCorrectly**: Verifies that a normal
relative path is appended to the base path without modification.

**PathHelpers_SafePathCombine_PathTraversalWithDoubleDots_ThrowsArgumentException**:
Verifies that a leading `..` segment that escapes the base is rejected with
`ArgumentException`.

**PathHelpers_SafePathCombine_DoubleDotsInMiddle_ThrowsArgumentException**: Verifies that
an embedded `..` sequence that causes the combined path to escape the base is also
rejected.

**PathHelpers_SafePathCombine_AbsoluteSegment_WithinBase_CombinesCorrectly**: Verifies
that a segment starting with a directory separator is accepted when the combined result
still resolves within the base (Path.Join folds it in rather than replacing the base).

**PathHelpers_SafePathCombine_BacktrackWithinBase_CombinesCorrectly**: Verifies that
segments such as `["baa", ".."]` are accepted because they resolve back to the base
directory — only the final resolved position matters.

**PathHelpers_SafePathCombine_FilenameWithDoubleDots_CombinesCorrectly**: Verifies that a
filename containing `..` as a substring (e.g. `v1..2.md`) is accepted because it does not
escape the base.

**PathHelpers_SafePathCombine_CurrentDirectoryReference_CombinesCorrectly**: Verifies that
current-directory references (`.`) remain within the base path.

**PathHelpers_SafePathCombine_NestedPaths_CombinesCorrectly**: Verifies that deeply nested
relative paths are combined correctly.

**PathHelpers_SafePathCombine_EmptyRelativePath_ReturnsBasePath**: Verifies that an empty
segment does not change the resulting path.

**PathHelpers_SafePathCombine_MultipleSegments_CombinesCorrectly**: Verifies that the
`params` overload appends multiple segments in order.

**PathHelpers_SafePathCombine_TraversalInLaterSegment_ThrowsArgumentException**: Verifies
that traversal introduced across multiple segments that collectively escape the base is
rejected.

**PathHelpers_SafePathCombine_NullBasePath_ThrowsArgumentNullException**: Verifies that a
null base path is rejected with `ArgumentNullException`.

**PathHelpers_SafePathCombine_NullRelativePath_ThrowsArgumentNullException**: Verifies that
a null segment is rejected with `ArgumentNullException`.

**PathHelpers_SafePathCombine_NoSegments_ReturnsBasePath**: Verifies that calling
`SafePathCombine` with zero segments returns `basePath` unchanged, confirming
that the zero-segment case is handled as a no-op.

**PathHelpers_NormalizeCase_ExistingPathWithDifferentCaseInput_ResolvesToActualOnDiskCasing**:
Verifies that a differently-cased input path resolves to the real on-disk casing of both
an intermediate directory segment and the leaf file segment.

**PathHelpers_NormalizeCase_DifferentlyCasedDriveLetterRoot_ProducesIdenticalResult**:
Verifies that two differently-cased spellings of the same Windows drive-letter root
normalize to an identical result (skipped on platforms without a drive-letter root).

**PathHelpers_NormalizeCase_TwoCaseVariantsOfSamePath_ProduceIdenticalResult**: Verifies
that two differently-cased input paths naming the same real file normalize to an
identical result, proving they can be safely compared with a case-sensitive comparer
afterward.

**PathHelpers_NormalizeCase_NonExistentPath_PreservesSuppliedCasing**: Verifies that a
path that does not resolve to a real file preserves the caller-supplied casing rather
than throwing.

**PathHelpers_NormalizeCase_CaseSensitiveFileSystemWithBothCasings_PrefersExactMatch**:
Verifies that when two entries differing only by case coexist (case-sensitive file
systems only), an exact match is always preferred over an incidental case-insensitive
one, regardless of directory-enumeration order.

**PathHelpers_NormalizeCase_CaseSensitiveFileSystemWithBothCasings_AmbiguousThirdCasing_PreservesSuppliedCasing**:
Verifies that when two entries differing only by case coexist and the supplied segment
matches neither exactly, the caller-supplied casing is preserved rather than guessing.

**PathHelpers_NormalizeCase_WithSharedDirectoryEntryCache_ResolvesMultipleSiblingPaths**:
Verifies that a caller-supplied `directoryEntryCache`, shared across multiple
`NormalizeCase` calls for paths under the same ancestor directory, still resolves each
path correctly.
