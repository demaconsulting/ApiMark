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
    ///     Safely combines a base path with one or more caller-supplied relative path segments,
    ///     rejecting any input that could escape the base directory.
    /// </summary>
    /// <remarks>
    ///     Each segment in <paramref name="relativePaths"/> is validated and appended to the
    ///     running combined path in order. Two validation layers are applied per segment:
    ///     1. An upfront string check rejects segments that contain ".." components or are already
    ///        rooted — these are the most common forms of directory-traversal attack.
    ///     2. A defense-in-depth check resolves both paths with <see cref="Path.GetFullPath(string)"/>
    ///        and uses <see cref="Path.GetRelativePath"/> to confirm the combined result stays under
    ///        the base. This guards against edge cases (e.g. platform-specific path normalization)
    ///        that could bypass the string check.
    ///     Forward slashes in path segments are accepted on all platforms; <see cref="Path.Join"/>
    ///     and <see cref="Path.GetFullPath(string)"/> normalize them correctly.
    ///     This method is stateless and thread-safe.
    /// </remarks>
    /// <param name="basePath">
    ///     The base directory path. Must not be null. Any valid directory path is accepted; it need
    ///     not exist on disk because only string and normalized-path operations are performed.
    /// </param>
    /// <param name="relativePaths">
    ///     One or more caller-supplied relative path segments to append in order. Must not be null.
    ///     Each individual segment must not be null, must not contain ".." components, and must not
    ///     be an absolute (rooted) path.
    /// </param>
    /// <returns>
    ///     The result of combining <paramref name="basePath"/> with each segment in
    ///     <paramref name="relativePaths"/> in order. The returned path is always within
    ///     <paramref name="basePath"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="basePath"/>, <paramref name="relativePaths"/>, or any
    ///     individual segment within <paramref name="relativePaths"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when any segment contains ".." components, is an absolute path, or resolves
    ///     outside the current combined path after normalization.
    /// </exception>
    internal static string SafePathCombine(string basePath, params string[] relativePaths)
    {
        // Validate that basePath and the segments array are not null
        ArgumentNullException.ThrowIfNull(basePath);
        ArgumentNullException.ThrowIfNull(relativePaths);

        // Apply validation and combination for each segment in order
        var current = basePath;
        foreach (var relativePath in relativePaths)
        {
            // Validate the individual segment is not null
            ArgumentNullException.ThrowIfNull(relativePath);

            // Ensure the segment does not contain path traversal sequences
            if (relativePath.Contains("..") || Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException($"Invalid path component: {relativePath}", nameof(relativePaths));
            }

            // Path.Join is used (not Path.Combine) because we have validated that relativePath
            // is not an absolute path; Path.Join performs simple concatenation and will not
            // silently discard current even if relativePath were somehow absolute.
            var combinedPath = Path.Join(current, relativePath);

            // Defense-in-depth: ensure the combined path is still under the base path
            var fullBasePath = Path.GetFullPath(basePath);
            var fullCombinedPath = Path.GetFullPath(combinedPath);
            var relativeCheck = Path.GetRelativePath(fullBasePath, fullCombinedPath);
            if (relativeCheck.StartsWith("..") || Path.IsPathRooted(relativeCheck))
            {
                throw new ArgumentException($"Invalid path component: {relativePath}", nameof(relativePaths));
            }

            current = combinedPath;
        }

        return current;
    }
}
