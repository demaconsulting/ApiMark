## ExternalXmlDocResolver

### Verification Approach

`ExternalXmlDocResolver` is unit-tested with temporary directories and files
created by each test's arrange step (mirroring `XmlDocReaderTests`'s
temporary-file style). Each test creates a small directory tree simulating a
reference assembly DLL and its XML documentation file (in the conventional
sibling location, or under `ref/`/`lib/` folders to exercise the swap
fallback), calls `TryGetMember`, and deletes the temporary directory in a
`finally` block. No mocking is required; the class has no injectable
dependencies beyond plain file system paths.

### Test Environment

Tests require write access to a temporary directory under
`Path.GetTempPath()`. No external service, network dependency, or fixture
assembly is needed — the "reference assembly DLL" files used in tests are
empty placeholder files, because `ExternalXmlDocResolver` never parses the DLL
itself, only path strings and the sibling/`ref`/`lib`-swapped XML file.

### Acceptance Criteria

- All `ExternalXmlDocResolver` tests pass with zero failures.
- `TryGetMember` resolves a member from the conventional sibling `.xml` file.
- `TryGetMember` returns `null` when no XML documentation file exists anywhere for the configured reference path.
- `TryGetMember` falls back to a `lib/` folder XML doc file when the reference assembly DLL sits under `ref/` with no sibling XML doc of its own.
- `TryGetMember` falls back to a `ref/` folder XML doc file when the reference assembly DLL sits under `lib/` with no sibling XML doc of its own.
- `TryGetMember` searches all configured reference paths in order, using the second path's documentation when the member is absent from the first path's documentation.
- `ExternalXmlDocResolver` parses each reference assembly's XML documentation file at most once and caches the result, verified by deleting the file after a successful lookup and confirming a second, different member from the same file still resolves.
- `TryGetMember` returns `null` when constructed with an empty reference-path list.
- `TryGetMember` returns `null` (rather than throwing) when a reference assembly's XML documentation file exists but contains malformed/corrupt XML.
- A repeated `TryGetMember` miss for the same member ID is served from the negative per-member cache without re-reading the underlying XML documentation file from disk.
- The constructor throws `ArgumentNullException` when `referenceAssemblyPaths` is `null`.

### Test Scenarios

**TryGetMember resolves a member from the sibling XML file**: Verifies that
when a reference assembly DLL has a conventional sibling `.xml` file
containing the requested member, `TryGetMember` returns that member's
`<member>` element. This scenario is tested by
`ExternalXmlDocResolver_TryGetMember_SiblingXmlPresent_ReturnsMember`.

**TryGetMember returns null when no documentation file exists**: Verifies
that when neither a sibling `.xml` file nor a `ref/`/`lib/`-swapped `.xml`
file exists for the configured reference path, `TryGetMember` returns `null`
rather than throwing. This scenario is tested by
`ExternalXmlDocResolver_TryGetMember_NoDocAnywhere_ReturnsNull`.

**TryGetMember resolves via the ref-to-lib folder swap**: Verifies that when
the reference assembly DLL sits under a `ref/` folder with no sibling XML doc
of its own, but a `lib/` folder (with the `ref` segment swapped to `lib`)
contains the matching `.xml` file, `TryGetMember` finds and returns the
member from that swapped location. This scenario is tested by
`ExternalXmlDocResolver_TryGetMember_RefLibFolderSwap_LibXmlPresentUnderRefDll_ReturnsMember`.

**TryGetMember resolves via the lib-to-ref folder swap**: Verifies the swap
in the other direction — the reference assembly DLL sits under a `lib/`
folder with no sibling XML doc, but the corresponding `ref/` folder contains
the matching `.xml` file. This scenario is tested by
`ExternalXmlDocResolver_TryGetMember_RefLibFolderSwap_RefXmlPresentUnderLibDll_ReturnsMember`.

**TryGetMember searches multiple reference paths in order**: Verifies that
when the first configured reference path's documentation does not contain the
requested member but the second configured path's documentation does,
`TryGetMember` returns the member from the second path — confirming the
"search all configured reference paths" requirement. This scenario is tested
by `ExternalXmlDocResolver_TryGetMember_SecondReferencePathMatches_ReturnsMemberFromSecondPath`.

**ExternalXmlDocResolver caches parsed documentation across calls**: Verifies
the "lazily parse and cache — do not eagerly parse" requirement using a
black-box technique: `TryGetMember` is called once successfully, the
underlying XML file is then deleted from disk, and `TryGetMember` is called
again for a different member defined in the same (now-deleted) file. The
second call still succeeding proves the file was parsed once and its member
index cached, rather than re-read from disk on every call. This scenario is
tested by
`ExternalXmlDocResolver_TryGetMember_CachesParsedDocAcrossCalls_DeletedFileStillResolvesSecondCall`.

**TryGetMember returns null for an empty reference-path list**: Verifies that
constructing `ExternalXmlDocResolver` with an empty list degrades gracefully —
`TryGetMember` returns `null` for any member ID rather than throwing. This
scenario is tested by
`ExternalXmlDocResolver_TryGetMember_EmptyReferencePathList_ReturnsNull`.

**TryGetMember degrades gracefully on a corrupt XML documentation file**: Verifies
that when a reference assembly's sibling `.xml` file exists but contains malformed
XML, `TryGetMember` returns `null` instead of propagating the parse exception,
confirming the narrow `catch` in `LoadMembers` for `System.Xml.XmlException` (along
with `IOException`/`UnauthorizedAccessException`) degrades a single problematic
reference assembly to "no documentation available" without failing the whole
generation run. This scenario is tested by
`ExternalXmlDocResolver_TryGetMember_CorruptXmlDocFile_ReturnsNullWithoutThrowing`.

**Repeated misses are served from the negative cache without re-reading disk**:
Verifies that once `TryGetMember` has returned `null` for a given member ID, a
second call for the same member ID does not re-touch the file system — proven by
rewriting the underlying XML file between the first and second call to add a
matching `<member>` entry for the missed ID, then confirming the second call
still returns `null`. A non-caching implementation would observe the newly
added entry and return non-null, so the second call still returning `null`
proves the negative result came from the cache rather than a fresh disk read.
This scenario is tested by
`ExternalXmlDocResolver_TryGetMember_RepeatedMissForSameMember_DoesNotReReadDiskAfterFirstMiss`.

**Constructor rejects a null reference-path list**: Verifies that constructing
`ExternalXmlDocResolver` with a `null` `referenceAssemblyPaths` argument throws
`ArgumentNullException` immediately rather than deferring the failure to the first
`TryGetMember` call. This scenario is tested by
`ExternalXmlDocResolver_Constructor_NullReferencePaths_ThrowsArgumentNullException`.
