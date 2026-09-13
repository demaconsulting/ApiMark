namespace ApiMark.Core;

/// <summary>
///     Helper utilities for safe and reliable path operations within ApiMark.
/// </summary>
/// <remarks>
///     This class exists to provide a single, auditable point for path-related concerns that
///     would otherwise be re-implemented (or forgotten) independently by each caller: combining
///     user-supplied or generator-supplied path segments with a base directory without permitting
///     directory-traversal escapes (<see cref="SafePathCombine"/>), and resolving a path to its
///     actual on-disk casing so that two differently-cased spellings of the same file or
///     directory can be recognized as identical regardless of platform or file-system case
///     sensitivity (<see cref="NormalizeCase"/>, <see cref="Comparer"/>).
///     All members are stateless and thread-safe.
/// </remarks>
public static class PathHelpers
{
    /// <summary>
    ///     Safely combines a base path with zero or more path segments, ensuring the result
    ///     remains within the base directory.
    /// </summary>
    /// <remarks>
    ///     All segments are joined using <see cref="Path.Join(ReadOnlySpan{string})"/>, then the
    ///     combined result is normalized with <see cref="Path.GetFullPath(string)"/> and checked
    ///     against the normalized base using <see cref="Path.GetRelativePath"/>. If the result
    ///     resolves outside the base directory the method throws; otherwise the joined
    ///     (un-normalized) path is returned. The escape check matches only an exact <c>..</c>
    ///     segment or one followed by a directory separator, so names beginning with two dots
    ///     (e.g. <c>..config</c>) are not misidentified as traversal.
    ///     Individual segments may contain <c>..</c> or be rooted provided the combined result
    ///     does not escape the base — for example segments <c>["baa", ".."]</c> on base
    ///     <c>C:\foo</c> resolve back to <c>C:\foo</c> and are accepted.
    ///     When no segments are supplied, returns <paramref name="basePath"/> unchanged.
    ///     This method is stateless and thread-safe.
    /// </remarks>
    /// <param name="basePath">
    ///     The base directory path. Must not be null. Any valid directory path is accepted; it need
    ///     not exist on disk because only string and normalized-path operations are performed.
    /// </param>
    /// <param name="relativePaths">
    ///     Zero or more path segments to append in order. Must not be null, and each individual
    ///     segment must not be null.
    /// </param>
    /// <returns>
    ///     The result of joining <paramref name="basePath"/> with all segments in
    ///     <paramref name="relativePaths"/>. The returned path always resolves within
    ///     <paramref name="basePath"/>. Returns <paramref name="basePath"/> unchanged when
    ///     no segments are supplied.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="basePath"/>, <paramref name="relativePaths"/>, or any
    ///     individual segment within <paramref name="relativePaths"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the combined path resolves outside <paramref name="basePath"/>.
    /// </exception>
    internal static string SafePathCombine(string basePath, params string[] relativePaths)
    {
        // Reject null arguments before any path work
        ArgumentNullException.ThrowIfNull(basePath);
        ArgumentNullException.ThrowIfNull(relativePaths);

        if (relativePaths.Any(segment => segment is null))
        {
            throw new ArgumentNullException(nameof(relativePaths), "Individual path segments must not be null.");
        }

        // Join all segments onto the base path
        var combined = relativePaths.Aggregate(basePath, Path.Join);

        // Normalize the combined result
        var fullBase = Path.GetFullPath(basePath);
        var fullCombined = Path.GetFullPath(combined);
        var relative = Path.GetRelativePath(fullBase, fullCombined);

        // Reject any combination whose normalized result escapes the base directory
        if (relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            relative.StartsWith("../", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            throw new ArgumentException("Path escapes base directory.", nameof(relativePaths));
        }

        return combined;
    }

    /// <summary>
    ///     The <see cref="StringComparer"/> to use once paths have been normalized via
    ///     <see cref="NormalizeCase"/>. Always case-sensitive: after normalization, case is the
    ///     only remaining signal that can legitimately distinguish two paths on a case-sensitive
    ///     file system, so it must never be discarded.
    /// </summary>
    /// <remarks>
    ///     There is no reliable way to infer case sensitivity from the operating system alone:
    ///     macOS and Windows can both host case-sensitive volumes (e.g. an explicitly formatted
    ///     case-sensitive APFS volume, or an NTFS directory with per-directory case sensitivity
    ///     enabled for WSL interop), and Linux can host case-insensitive file systems (e.g. exFAT
    ///     mounts, or ext4 directories with the <c>casefold</c> feature enabled). An
    ///     operating-system-based guess — for example "Ordinal on Linux, OrdinalIgnoreCase
    ///     everywhere else" — is therefore wrong on any of these combinations: it can either
    ///     incorrectly conflate two genuinely distinct paths that differ only in case, or
    ///     incorrectly treat two spellings of the same path as distinct.
    ///     The only combination that is correct regardless of platform or file system is to first
    ///     resolve each path to its actual on-disk casing (see <see cref="NormalizeCase"/>) and
    ///     then compare the normalized results with this case-sensitive comparer: two inputs that
    ///     name the same file always resolve to an identical normalized string (because the file
    ///     system itself is the single source of truth for its real casing), while two inputs that
    ///     name genuinely different files/directories never do, even when the file system happens
    ///     to be case-insensitive and would otherwise open either spelling successfully.
    /// </remarks>
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
    ///     — a naive implementation would preserve it verbatim, which would mean two spellings of
    ///     the same root (e.g. <c>C:\</c> vs <c>c:\</c>, or two differently-cased UNC server/share
    ///     names) normalize to two distinct strings under <see cref="Comparer"/>'s Ordinal
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
