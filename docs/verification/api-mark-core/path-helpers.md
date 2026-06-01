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
- `SafePathCombine` returns the expected combined path for valid relative input,
  including nested paths, current-directory references, empty segments, and multi-segment
  combinations.
- `SafePathCombine` throws `ArgumentException` for traversal attempts and rooted paths.
- `SafePathCombine` throws `ArgumentNullException` for null `basePath` and null relative
  segments.

### Test Scenarios

**PathHelpers_SafePathCombine_ValidPaths_CombinesCorrectly**: Verifies that a normal
relative path is appended to the base path without modification and produces the same
result as the platform path helper.

**PathHelpers_SafePathCombine_PathTraversalWithDoubleDots_ThrowsArgumentException**:
Verifies that a leading `..` traversal attempt is rejected immediately with
`ArgumentException`.

**PathHelpers_SafePathCombine_DoubleDotsInMiddle_ThrowsArgumentException**: Verifies that
an embedded traversal sequence later in a relative path is also rejected, not just a
leading traversal segment.

**PathHelpers_SafePathCombine_AbsolutePath_ThrowsArgumentException**: Verifies that a
rooted Unix-style path is rejected instead of replacing the trusted base path.

**PathHelpers_SafePathCombine_WindowsAbsolutePath_ThrowsArgumentException**: Verifies that
Windows drive-letter absolute paths are rejected on Windows systems; the test is skipped
on non-Windows platforms where the runtime does not treat drive-letter strings as rooted.

**PathHelpers_SafePathCombine_CurrentDirectoryReference_CombinesCorrectly**: Verifies that
current-directory references (`.`) are accepted because they remain within the base path
and combine to the expected location.

**PathHelpers_SafePathCombine_NestedPaths_CombinesCorrectly**: Verifies that deeply nested
relative paths are combined correctly when no traversal or rooted segments are present.

**PathHelpers_SafePathCombine_EmptyRelativePath_ReturnsBasePath**: Verifies that an empty
relative path segment does not change the resulting path.

**PathHelpers_SafePathCombine_MultipleSegments_CombinesCorrectly**: Verifies that the
`params` overload validates and appends multiple segments in order, producing the same
result as joining the same segments explicitly.

**PathHelpers_SafePathCombine_TraversalInLaterSegment_ThrowsArgumentException**: Verifies
that traversal introduced in any later segment of a multi-segment call is rejected.

**PathHelpers_SafePathCombine_NullBasePath_ThrowsArgumentNullException**: Verifies that a
null base path is rejected before any path normalization or combination is attempted.

**PathHelpers_SafePathCombine_NullRelativePath_ThrowsArgumentNullException**: Verifies that
null relative path segments are rejected before any path combination occurs.
