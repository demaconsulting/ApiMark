## Microsoft.Extensions.FileSystemGlobbing

### Verification Approach

`Microsoft.Extensions.FileSystemGlobbing` is verified in ApiMark through unit tests in
`test/ApiMark.Core.Tests/GlobFileCollectorTests.cs` that exercise `GlobFileCollector`,
the sole consumer of the library. The tests operate against temporary real directories
on disk, confirming that the `Matcher` API behaves as expected for the subset of
features ApiMark uses: include patterns, exclusion patterns, and non-existent roots.

### Test Scenarios

**Relative glob pattern matches VHDL files**: Verifies that a relative `*.vhd` pattern
resolved against a working directory returns only the `.vhd` files present in that
directory. This scenario is tested by
`GlobFileCollector_Collect_RelativeVhdPattern_FindsVhdFiles`.

**Absolute glob pattern matches files**: Verifies that an absolute glob pattern whose
root is derived from the longest non-glob prefix finds the expected files without
requiring a separate working directory. This scenario is tested by
`GlobFileCollector_Collect_AbsolutePattern_FindsFiles`.
