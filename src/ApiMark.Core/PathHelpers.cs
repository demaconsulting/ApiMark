namespace ApiMark.Core;

/// <summary>
///     Helper utilities for safe path operations within ApiMark.
/// </summary>
/// <remarks>
///     This class exists to provide a single, auditable point of path-safety enforcement for any
///     operation that combines user-supplied or generator-supplied path segments with a base
///     directory. Centralizing the check prevents directory-traversal vulnerabilities from being
///     re-implemented (or forgotten) in each caller.
///     All members are stateless and thread-safe.
/// </remarks>
internal static class PathHelpers
{
    /// <summary>
    ///     Safely combines a base path with one or more path segments, ensuring the result
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
    ///     This method is stateless and thread-safe.
    /// </remarks>
    /// <param name="basePath">
    ///     The base directory path. Must not be null. Any valid directory path is accepted; it need
    ///     not exist on disk because only string and normalized-path operations are performed.
    /// </param>
    /// <param name="relativePaths">
    ///     One or more path segments to append in order. Must not be null, and each individual
    ///     segment must not be null.
    /// </param>
    /// <returns>
    ///     The result of joining <paramref name="basePath"/> with all segments in
    ///     <paramref name="relativePaths"/>. The returned path always resolves within
    ///     <paramref name="basePath"/>.
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
        ArgumentNullException.ThrowIfNull(basePath);
        ArgumentNullException.ThrowIfNull(relativePaths);

        if (relativePaths.Any(segment => segment is null))
        {
            throw new ArgumentNullException(nameof(relativePaths), "Individual path segments must not be null.");
        }

        var combined = relativePaths.Aggregate(basePath, Path.Join);

        var fullBase = Path.GetFullPath(basePath);
        var fullCombined = Path.GetFullPath(combined);
        var relative = Path.GetRelativePath(fullBase, fullCombined);

        if (relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            relative.StartsWith("../", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            throw new ArgumentException("Path escapes base directory.", nameof(relativePaths));
        }

        return combined;
    }
}
