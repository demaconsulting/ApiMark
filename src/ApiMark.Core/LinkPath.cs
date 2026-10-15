namespace ApiMark.Core;

/// <summary>
///     Computes relative Markdown link destinations between pages with pure string arithmetic, so links are
///     identical on every operating system.
/// </summary>
public static class LinkPath
{
    /// <summary>
    ///     Computes the relative destination from a page in <paramref name="fromDirectory"/> to a target.
    /// </summary>
    /// <param name="fromDirectory">The directory (forward slashes, empty for the root) of the page containing the link.</param>
    /// <param name="targetPath">The target path relative to the same root, including its extension.</param>
    /// <returns>A forward-slash relative path such as <c>../Other/Page.md</c> or <c>Page.md</c>.</returns>
    public static string Relative(string fromDirectory, string targetPath)
    {
        ArgumentNullException.ThrowIfNull(fromDirectory);
        ArgumentNullException.ThrowIfNull(targetPath);

        var from = fromDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var to = targetPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Only directory segments of the target can be shared with the source directory.
        var common = 0;
        while (common < from.Length && common < to.Length - 1 &&
               string.Equals(from[common], to[common], StringComparison.Ordinal))
        {
            common++;
        }

        var parts = Enumerable.Repeat("..", from.Length - common).Concat(to.Skip(common));
        return string.Join('/', parts);
    }

    /// <summary>
    ///     Percent-encodes the characters that would terminate, corrupt, or re-interpret a Markdown inline
    ///     link destination built from page paths.
    /// </summary>
    /// <remarks>
    ///     The input must be a raw (not yet encoded) relative path made of registry-approved names, such as
    ///     the result of <see cref="Relative"/>. A raw <c>%</c> is encoded as <c>%25</c> so the destination
    ///     resolves to the file whose name contains it; text that is already percent-encoded is therefore
    ///     encoded again, so an externally supplied base URL (which may already be encoded) must not be
    ///     passed through this method: encode only the registry-produced path and append it to the base.
    ///     <c>/</c>, <c>.</c>, and braces are kept.
    /// </remarks>
    /// <param name="destination">The raw destination path.</param>
    /// <returns>The destination with <c>%</c>, <c>#</c>, <c>?</c>, space, parentheses, and angle brackets encoded.</returns>
    public static string Escape(string destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        // The percent sign must be encoded first so the escapes added below are not encoded again.
        return destination
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace(" ", "%20", StringComparison.Ordinal)
            .Replace("#", "%23", StringComparison.Ordinal)
            .Replace("?", "%3F", StringComparison.Ordinal)
            .Replace("(", "%28", StringComparison.Ordinal)
            .Replace(")", "%29", StringComparison.Ordinal)
            .Replace("<", "%3C", StringComparison.Ordinal)
            .Replace(">", "%3E", StringComparison.Ordinal);
    }
}
