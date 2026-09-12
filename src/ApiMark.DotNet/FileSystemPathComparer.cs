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
    /// </remarks>
    /// <param name="path">An absolute path to normalize.</param>
    /// <returns>
    ///     <paramref name="path"/> with every segment that exists on disk replaced by its actual
    ///     on-disk casing.
    /// </returns>
    public static string NormalizeCase(string path)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        var remainder = path[root.Length..];
        var segments = remainder.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        var current = root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, FindActualEntryName(current, segment) ?? segment);
        }

        return current;
    }

    /// <summary>
    ///     Looks up <paramref name="segment"/> among the actual file-system entries of
    ///     <paramref name="parentDirectory"/>, case-insensitively, and returns the matching
    ///     entry's real on-disk name.
    /// </summary>
    /// <param name="parentDirectory">The directory to search.</param>
    /// <param name="segment">The path segment to resolve.</param>
    /// <returns>
    ///     The matching entry's actual name, or <c>null</c> when <paramref name="parentDirectory"/>
    ///     does not exist, cannot be enumerated, or contains no entry matching
    ///     <paramref name="segment"/> case-insensitively.
    /// </returns>
    private static string? FindActualEntryName(string parentDirectory, string segment)
    {
        if (!Directory.Exists(parentDirectory))
        {
            return null;
        }

        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(parentDirectory))
            {
                var name = Path.GetFileName(entry);
                if (string.Equals(name, segment, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Directory exists but cannot be enumerated (e.g. permissions); fall back to the
            // caller-supplied casing for this segment rather than failing normalization entirely.
        }

        return null;
    }
}
