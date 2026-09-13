namespace ApiMark.DotNet;

/// <summary>
///     Provides the <see cref="StringComparer"/> and path-casing normalization used whenever two
///     file-system paths need to be compared or deduplicated within <c>ApiMark.DotNet</c>.
/// </summary>
/// <remarks>
///     Both <see cref="DotNetGenerator"/> (deduplicating reference-path search directories before
///     seeding a Mono.Cecil <c>DefaultAssemblyResolver</c>) and <see cref="ExternalXmlDocResolver"/>
///     (keying its per-reference-path documentation cache) need the same rule for deciding whether
///     two path strings refer to the same underlying file or directory.
///     <para>
///     There is no reliable way to infer case sensitivity from the operating system alone: macOS
///     and Windows can both host case-sensitive volumes (e.g. an explicitly formatted
///     case-sensitive APFS volume, or an NTFS directory with per-directory case sensitivity
///     enabled for WSL interop), and Linux can host case-insensitive file systems (e.g. exFAT
///     mounts, or ext4 directories with the <c>casefold</c> feature enabled). An
///     operating-system-based guess — for example "Ordinal on Linux, OrdinalIgnoreCase
///     everywhere else" — is therefore wrong on any of these combinations: it can either
///     incorrectly conflate two genuinely distinct paths that differ only in case, or incorrectly
///     treat two spellings of the same path as distinct.
///     </para>
///     <para>
///     The only combination that is correct regardless of platform or file system is to first
///     resolve each path to its actual on-disk casing (see <see cref="NormalizeCase"/>) and then
///     compare the normalized results with a case-sensitive (<see cref="StringComparer.Ordinal"/>)
///     comparer: two inputs that name the same file always resolve to an identical normalized
///     string (because the file system itself is the single source of truth for its real casing),
///     while two inputs that name genuinely different files/directories never do, even when the
///     file system happens to be case-insensitive and would otherwise open either spelling
///     successfully.
///     </para>
///     This mirrors the equivalent helper in <c>ApiMark.Cpp.CppEmitter</c>; the logic is
///     duplicated there rather than shared because <c>ApiMark.DotNet</c> does not reference
///     <c>ApiMark.Cpp</c> and introducing a cross-project/shared-assembly dependency purely for
///     this one small helper would be a larger change than the bug fix it supports.
/// </remarks>
internal static class FileSystemPathComparer
{
    /// <summary>
    ///     The <see cref="StringComparer"/> to use once paths have been normalized via
    ///     <see cref="NormalizeCase"/>. Always case-sensitive: after normalization, case is the
    ///     only remaining signal that can legitimately distinguish two paths on a case-sensitive
    ///     file system, so it must never be discarded.
    /// </summary>
    public static StringComparer Comparer => StringComparer.Ordinal;

    /// <summary>
    ///     Resolves <paramref name="path"/> to its actual on-disk casing by querying the file
    ///     system directly, one path segment at a time, rather than guessing based on the
    ///     operating system.
    /// </summary>
    /// <remarks>
    ///     <paramref name="path"/> is expected to already be an absolute, fully-qualified path
    ///     (e.g. via <see cref="Path.GetFullPath(string)"/>); this method does not perform its own
    ///     relative-path resolution. Each segment from the root downwards is looked up
    ///     case-insensitively among its parent directory's actual entries; when a matching entry
    ///     is found, its real, on-disk-cased name replaces the input segment. Segments that do not
    ///     exist (for example, because the leaf file itself has not been created, or an
    ///     intermediate directory is missing) are left exactly as supplied — normalization is
    ///     best-effort and only corrects segments the file system can actually confirm.
    ///     <para>
    ///     When <paramref name="directoryEntryCache"/> is supplied, each parent directory's
    ///     enumerated entries are memoized in it, keyed by the parent directory's own already-
    ///     normalized path. This matters because a caller normalizing many paths that share common
    ///     ancestor directories — for example dozens or hundreds of NuGet package assembly paths
    ///     that all live under the same package-cache root — would otherwise re-enumerate the same
    ///     shared directories once per path. Callers that only normalize a single, independent path
    ///     can omit this parameter (or pass <c>null</c>); the cache is intentionally the caller's
    ///     responsibility to create and scope (e.g. to one generation run), rather than a
    ///     process-wide cache here, so results can never become stale across independent calls
    ///     separated by file-system changes.
    ///     </para>
    /// </remarks>
    /// <param name="path">An absolute path to normalize.</param>
    /// <param name="directoryEntryCache">
    ///     An optional, caller-owned cache of parent-directory entry listings, reused across
    ///     multiple <see cref="NormalizeCase"/> calls to avoid redundant directory enumeration
    ///     when many paths share common ancestor directories. Pass <c>null</c> (the default) to
    ///     normalize a single path with no caching.
    /// </param>
    /// <returns>
    ///     <paramref name="path"/> with every segment that exists on disk replaced by its actual
    ///     on-disk casing.
    /// </returns>
    public static string NormalizeCase(string path, Dictionary<string, string[]>? directoryEntryCache = null)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        var remainder = path[root.Length..];
        var segments = remainder.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        var current = CanonicalizeRoot(root);
        foreach (var segment in segments)
        {
            current = Path.Combine(current, FindActualEntryName(current, segment, directoryEntryCache) ?? segment);
        }

        return current;
    }

    /// <summary>
    ///     Canonicalizes the drive-letter or UNC root prefix of a path so that two differently-cased
    ///     spellings of the same root always normalize to an identical result.
    /// </summary>
    /// <remarks>
    ///     Unlike every subsequent path segment, a root prefix is never a directory entry that can
    ///     be looked up and resolved to a "real" on-disk casing via <see cref="FindActualEntryName"/>
    ///     — <see cref="NormalizeCase"/> preserved it verbatim, which meant two spellings of the
    ///     same root (e.g. <c>C:\</c> vs <c>c:\</c>, or two differently-cased UNC server/share
    ///     names) normalized to two distinct strings under the <see cref="Comparer"/>'s Ordinal
    ///     comparison, breaking the documented at-most-once/deduplication guarantee. Windows drive
    ///     letters carry no meaningful case identity at all, so they are canonicalized to uppercase
    ///     (the conventional spelling). A UNC server/share root has no locally-queryable "real"
    ///     casing either without a network round trip, so it is instead canonicalized to lowercase:
    ///     a stable, deterministic form under which two spellings of the same root always match,
    ///     which is all the deduplication guarantee actually requires. Roots that are neither
    ///     (e.g. Unix's single-character <c>/</c>) are returned unchanged.
    /// </remarks>
    /// <param name="root">The root prefix returned by <see cref="Path.GetPathRoot(string)"/>.</param>
    /// <returns>The canonicalized root prefix.</returns>
    private static string CanonicalizeRoot(string root)
    {
        // Drive-letter root: "C:", "C:\", or "C:/".
        if (root.Length is 2 or 3 && char.IsLetter(root[0]) && root[1] == ':')
        {
            return char.ToUpperInvariant(root[0]) + root[1..];
        }

        // UNC root: "\\server\share\" (or the forward-slash-separator equivalent).
        if (root.Length >= 2
            && (root[0] == Path.DirectorySeparatorChar || root[0] == Path.AltDirectorySeparatorChar)
            && (root[1] == Path.DirectorySeparatorChar || root[1] == Path.AltDirectorySeparatorChar))
        {
            return root.ToLowerInvariant();
        }

        return root;
    }

    /// <summary>
    ///     Looks up <paramref name="segment"/> among the actual file-system entries of
    ///     <paramref name="parentDirectory"/>, preferring an exact (ordinal) match and falling
    ///     back to an unambiguous case-insensitive match, and returns the matching entry's real
    ///     on-disk name.
    /// </summary>
    /// <param name="parentDirectory">The directory to search.</param>
    /// <param name="segment">The path segment to resolve.</param>
    /// <param name="directoryEntryCache">
    ///     An optional cache of previously enumerated directory entries, keyed by parent
    ///     directory. See <see cref="NormalizeCase"/> for scoping guidance.
    /// </param>
    /// <returns>
    ///     The matching entry's actual name, or <c>null</c> when <paramref name="parentDirectory"/>
    ///     does not exist, cannot be enumerated, contains no entry matching <paramref name="segment"/>
    ///     case-insensitively, or contains two or more entries that match <paramref name="segment"/>
    ///     case-insensitively but none of them exactly — an ambiguous case-sensitive-file-system
    ///     scenario this method deliberately refuses to guess at rather than resolving arbitrarily.
    /// </returns>
    private static string? FindActualEntryName(string parentDirectory, string segment, Dictionary<string, string[]>? directoryEntryCache)
    {
        string[]? cachedEntries = null;
        var haveCachedEntries = directoryEntryCache is not null && directoryEntryCache.TryGetValue(parentDirectory, out cachedEntries);

        if (!haveCachedEntries)
        {
            if (!Directory.Exists(parentDirectory))
            {
                return null;
            }

            try
            {
                cachedEntries = Directory.EnumerateFileSystemEntries(parentDirectory)
                    .Select(Path.GetFileName)
                    .Where(name => name is not null)
                    .Select(name => name!)
                    .ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Directory exists but cannot be enumerated (e.g. permissions); leave this
                // segment unresolved for the caller to fall back to its supplied casing, without
                // caching a failure (a transient permissions issue should not be remembered).
                return null;
            }

            directoryEntryCache?.Add(parentDirectory, cachedEntries);
        }

        // Prefer an exact (ordinal) match first: on a case-sensitive file system, two entries
        // can coexist whose names differ only by case (e.g. "foo.dll" and "Foo.dll"). Falling
        // through to a case-insensitive match unconditionally would let enumeration order — which
        // is unspecified — decide which of the two is treated as the "real" casing, silently
        // resolving to the wrong file. An exact match is always unambiguous and correct regardless
        // of enumeration order or file-system case sensitivity, so it must win whenever one exists.
        //
        // When there is no exact match, only fall back to a case-insensitive match when exactly
        // one distinct entry matches case-insensitively. If two or more case variants coexist
        // (e.g. both "Foo.dll" and "foo.dll" exist) and the supplied segment matches neither one
        // exactly (e.g. "FOO.dll"), there is no way to know which of the coexisting files the
        // caller actually meant — arbitrarily picking whichever enumeration happens to return
        // first would risk silently resolving to the wrong file (e.g. attributing external XML
        // documentation to the wrong referenced assembly). In that ambiguous case, return null so
        // the caller falls back to the as-supplied casing instead of guessing.
        string? caseInsensitiveMatch = null;
        var caseInsensitiveMatchCount = 0;
        foreach (var name in cachedEntries!)
        {
            if (string.Equals(name, segment, StringComparison.Ordinal))
            {
                return name;
            }

            if (string.Equals(name, segment, StringComparison.OrdinalIgnoreCase))
            {
                caseInsensitiveMatch = name;
                caseInsensitiveMatchCount++;
            }
        }

        return caseInsensitiveMatchCount == 1 ? caseInsensitiveMatch : null;
    }
}
