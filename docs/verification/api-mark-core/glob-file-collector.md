## GlobFileCollector

### Verification Approach

`GlobFileCollector` is verified through direct unit tests that create isolated
temporary directories, populate them with files, invoke `Collect` with various
pattern configurations, and assert on the returned file list. Temporary
directories are created in `Path.GetTempPath()` with random names and are
deleted in `finally` blocks to prevent test pollution.

### Test Environment

N/A - standard .NET test runner is sufficient. No external tools or environment
variables are required. Tests use isolated temporary directories so they are
safe to run in parallel.

### Acceptance Criteria

- All `GlobFileCollector` test cases pass with zero failures.
- Empty pattern list returns an empty result.
- Relative patterns are resolved against the supplied working directory.
- Bare-star patterns (`**/*`) apply language-extension filtering.
- Explicit-extension patterns (`**/*.vhd`) select only that extension.
- Absolute patterns are supported and ignore the working directory.
- Absolute patterns with no glob metacharacters are treated as literal file paths
  and select exactly the named file when it exists and has a matching extension.
- `!`-prefixed exclusion patterns remove matching files from the result.
- Non-existent pattern roots return an empty result without throwing.
- Overlapping patterns produce a deduplicated, sorted result.
- `Collect` throws `ArgumentNullException` for null `patterns`, `languageExtensions`,
  or `workingDirectory`.

### Test Scenarios

**Empty patterns return empty list**: Verifies that invoking `Collect` with an
empty pattern list returns an empty result without accessing the filesystem.
Tested by `GlobFileCollector_Collect_EmptyPatterns_ReturnsEmptyList`.

**Relative `**/*.vhd` pattern finds `.vhd` files**: Verifies that a relative
explicit-extension pattern locates `.vhd` files under the working directory
and excludes files with other extensions. Tested by
`GlobFileCollector_Collect_RelativeVhdPattern_FindsVhdFiles`.

**Bare-star pattern with VHDL extensions selects `.vhd` and `.vhdl` only**:
Verifies that a `**/*` pattern triggers language-extension inference and
returns both `.vhd` and `.vhdl` files while excluding files with other
extensions (e.g. `.txt`). Tested by
`GlobFileCollector_Collect_BareStarWithVhdlExtensions_FiltersToVhdlOnly`.

**Absolute pattern finds files outside the working directory**: Verifies that
an absolute path pattern (e.g. `{tempDir}/**/*.vhd`) locates files correctly
regardless of the supplied working directory. Tested by
`GlobFileCollector_Collect_AbsolutePattern_FindsFiles`.

**Exclusion pattern removes matched files**: Verifies that a `!`-prefixed
pattern removes matching files from the accumulated inclusion result, so that
`["**/*.vhd", "!test/**/*.vhd"]` returns only files outside the `test/`
subtree. Tested by
`GlobFileCollector_Collect_ExclusionPattern_RemovesMatchedFiles`.

**Non-existent root returns empty without throwing**: Verifies that a pattern
whose root directory does not exist is silently skipped and contributes no
files, with no exception raised. Tested by
`GlobFileCollector_Collect_NonExistentRoot_ReturnsEmptyWithoutThrowing`.

**Overlapping patterns produce sorted, deduplicated result**: Verifies that
supplying two identical patterns that both match the same files returns each
file exactly once, and that the returned list is in ascending ordinal order.
Tested by
`GlobFileCollector_Collect_OverlappingPatterns_ReturnsSortedDeduplicated`.

**Literal absolute file path selects exactly the named file**: Verifies that an
absolute pattern with no glob metacharacters (e.g., `/absolute/path/to/file.vhd`)
is treated as a literal file path and returned in the result when the file exists
and has a matching extension, without any directory traversal. Tested by
`GlobFileCollector_Collect_LiteralAbsoluteFilePath_SelectsExactFile`.

**Literal absolute exclusion path removes exactly the named file**: Verifies that
an absolute literal exclusion pattern (no glob metacharacters, prefixed with `!`)
removes exactly the named file from the accumulated inclusion result. Tested by
`GlobFileCollector_Collect_LiteralAbsoluteExclusionPath_RemovesExactFile`.

**Null patterns throws ArgumentNullException**: Verifies that passing `null` for the
`patterns` argument throws `ArgumentNullException` immediately. Tested by
`GlobFileCollector_Collect_NullPatterns_ThrowsArgumentNullException`.

**Null languageExtensions throws ArgumentNullException**: Verifies that passing `null`
for the `languageExtensions` argument throws `ArgumentNullException` immediately. Tested
by `GlobFileCollector_Collect_NullLanguageExtensions_ThrowsArgumentNullException`.

**Null workingDirectory throws ArgumentNullException**: Verifies that passing `null`
for the `workingDirectory` argument throws `ArgumentNullException` immediately. Tested
by `GlobFileCollector_Collect_NullWorkingDirectory_ThrowsArgumentNullException`.
