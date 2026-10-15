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
    ///     Percent-encodes the characters that would terminate or corrupt a Markdown inline link destination.
    ///     An existing <c>%</c> is left alone so an already encoded base URL is not encoded twice.
    /// </summary>
    /// <param name="destination">The raw destination.</param>
    /// <returns>The destination with spaces, parentheses, and angle brackets encoded.</returns>
    public static string Escape(string destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return destination
            .Replace(" ", "%20", StringComparison.Ordinal)
            .Replace("(", "%28", StringComparison.Ordinal)
            .Replace(")", "%29", StringComparison.Ordinal)
            .Replace("<", "%3C", StringComparison.Ordinal)
            .Replace(">", "%3E", StringComparison.Ordinal);
    }
}
