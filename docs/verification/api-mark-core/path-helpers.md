## PathHelpers

### Verification Approach

PathHelpers is verified with unit tests in `test/ApiMark.Core.Tests/PathHelpersTests.cs`.
The tests call the real implementation directly and cover normal operation, traversal
rejection, rooted-path rejection, null-argument handling, and multi-segment behavior.
Because PathHelpers is `internal`, `ApiMark.Core` exposes it to `ApiMark.Core.Tests`
through `InternalsVisibleTo`.

### Test Environment

N/A - standard test environment using the .NET test runner is sufficient.

### Acceptance Criteria

- All `PathHelpersTests` test cases pass with zero failures.
- `SafePathCombine` returns the expected combined path for valid input, including
  nested paths, current-directory references, empty segments, multi-segment combinations,
  segments that backtrack within the base, and filenames containing `..` as a substring.
- `SafePathCombine` returns `basePath` unchanged when zero segments are supplied.
- `SafePathCombine` throws `ArgumentException` for paths that resolve outside the base
  directory after joining.
- `SafePathCombine` throws `ArgumentNullException` for null `basePath` and null segments.

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
