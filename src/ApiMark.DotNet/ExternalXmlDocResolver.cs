using System.Xml.Linq;

namespace ApiMark.DotNet;

/// <summary>
///     Locates and lazily parses the XML documentation files of externally referenced assemblies
///     (e.g. NuGet package dependencies) so that <c>&lt;inheritdoc /&gt;</c> elements can resolve
///     against base types/members defined outside the assembly currently being documented.
/// </summary>
/// <remarks>
///     For each configured reference assembly path, the corresponding XML documentation file is
///     located using the standard convention of a sibling file with the same name and a
///     <c>.xml</c> extension. When that sibling file does not exist, a <c>ref/</c>&#8596;<c>lib/</c>
///     folder-segment swap is attempted, because some NuGet packages ship compile-time reference
///     assemblies under a <c>ref/</c> folder while their XML documentation is published only
///     alongside the runtime assembly under the corresponding <c>lib/</c> folder (or vice versa).
///     This mirrors the well-known fallback behavior of the community <c>SauceControl.InheritDoc</c>
///     tool, reimplemented natively here without taking a dependency on that package.
///     <para>
///     Each reference assembly's XML documentation file is parsed at most once, the first time any
///     of its members is requested — reference assembly lists can be large (e.g. hundreds of
///     transitive NuGet dependencies) and most of their documentation is never needed, so eagerly
///     parsing every configured path up front would waste time and memory. Both per-file parse
///     results (including "no doc file found" / "failed to parse") and per-member-ID lookup results
///     (including misses) are cached for the lifetime of this instance, so repeated lookups —
///     successful or not — never re-touch disk.
///     </para>
///     <para>
///     Known limitation: the <c>ref/</c>&#8596;<c>lib/</c> swap only handles a single matching path
///     segment named exactly <c>ref</c> or <c>lib</c> — specifically, the one nearest the assembly
///     file (searched from the end of the path backwards, so an unrelated earlier segment that
///     happens to share the name is never matched instead); it does not attempt to reconcile
///     differing target-framework sub-folders (e.g. <c>ref/net8.0/</c> vs <c>lib/netstandard2.0/</c>)
///     beyond that single segment swap, and it does not search arbitrary additional locations. A
///     NuGet package that ships its XML documentation in neither the sibling location nor the
///     swapped <c>ref/</c>/<c>lib/</c> location will simply not have its external members resolved.
///     </para>
///     Instances are not safe for concurrent use from multiple threads because both caches are
///     backed by plain, non-thread-safe dictionaries; ApiMark's generation pipeline only ever
///     accesses a single instance from a single thread.
/// </remarks>
public sealed class ExternalXmlDocResolver
{
    /// <summary>The configured reference assembly paths, searched in order for each lookup.</summary>
    private readonly IReadOnlyList<string> _referenceAssemblyPaths;

    /// <summary>
    ///     Cache of parsed member indexes keyed by reference assembly path. A <c>null</c> value
    ///     means "no XML documentation file could be found or parsed for this reference assembly
    ///     path", cached so repeated misses do not re-probe the file system.
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, XElement>?> _docsByReferencePath = new(FileSystemPathComparer.Comparer);

    /// <summary>
    ///     Cache of resolved member elements keyed by member ID, spanning all configured reference
    ///     assembly paths. A <c>null</c> value means "not found in any configured reference
    ///     assembly's XML documentation", cached so repeated misses do not re-scan every path.
    /// </summary>
    private readonly Dictionary<string, XElement?> _memberCache = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of <see cref="ExternalXmlDocResolver"/>.</summary>
    /// <param name="referenceAssemblyPaths">
    ///     Paths to referenced assembly DLLs (e.g. NuGet package assemblies) whose sibling XML
    ///     documentation files should be searched, in order, for <c>&lt;inheritdoc /&gt;</c>
    ///     resolution targets not found in the assembly currently being documented.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="referenceAssemblyPaths"/> is <c>null</c>.</exception>
    public ExternalXmlDocResolver(IReadOnlyList<string> referenceAssemblyPaths)
    {
        ArgumentNullException.ThrowIfNull(referenceAssemblyPaths);

        // Snapshot the input rather than holding a reference to the caller's list: if the caller
        // later mutates the same mutable list instance (e.g. adds a path), this resolver must not
        // silently observe the change, because doing so would leave already-cached "not found"
        // member lookups (see _memberCache) stale — they would never be re-searched against the
        // newly added path.
        _referenceAssemblyPaths = referenceAssemblyPaths.ToArray();
    }

    /// <summary>
    ///     Attempts to resolve <paramref name="memberId"/> against the XML documentation files of
    ///     the configured reference assembly paths.
    /// </summary>
    /// <remarks>
    ///     Reference assembly paths are searched in the order supplied to the constructor; the
    ///     first path whose XML documentation file contains <paramref name="memberId"/> wins.
    ///     Results (including misses) are cached, so repeated calls with the same
    ///     <paramref name="memberId"/> never re-parse or re-search.
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier (e.g. <c>T:MyNamespace.MyClass</c>) to resolve.</param>
    /// <returns>The matching <c>&lt;member&gt;</c> element, or <c>null</c> when not found in any configured reference assembly.</returns>
    public XElement? TryGetMember(string memberId)
    {
        if (_memberCache.TryGetValue(memberId, out var cached))
        {
            return cached;
        }

        XElement? result = null;
        foreach (var referenceAssemblyPath in _referenceAssemblyPaths)
        {
            var members = GetOrLoadMembers(referenceAssemblyPath);
            if (members != null && members.TryGetValue(memberId, out var member))
            {
                result = member;
                break;
            }
        }

        _memberCache[memberId] = result;
        return result;
    }

    /// <summary>
    ///     Returns the cached member index for <paramref name="referenceAssemblyPath"/>, parsing
    ///     and caching it on first access.
    /// </summary>
    /// <param name="referenceAssemblyPath">Path to the reference assembly DLL.</param>
    /// <returns>
    ///     A dictionary of member elements keyed by member ID, or <c>null</c> when no XML
    ///     documentation file could be located or parsed for this reference assembly.
    /// </returns>
    private Dictionary<string, XElement>? GetOrLoadMembers(string referenceAssemblyPath)
    {
        if (_docsByReferencePath.TryGetValue(referenceAssemblyPath, out var cached))
        {
            return cached;
        }

        var members = LoadMembers(referenceAssemblyPath);
        _docsByReferencePath[referenceAssemblyPath] = members;
        return members;
    }

    /// <summary>
    ///     Locates and parses the XML documentation file for <paramref name="referenceAssemblyPath"/>.
    /// </summary>
    /// <param name="referenceAssemblyPath">Path to the reference assembly DLL.</param>
    /// <returns>
    ///     A dictionary of member elements keyed by member ID, or <c>null</c> when no XML
    ///     documentation file could be located, or it could not be parsed.
    /// </returns>
    private static Dictionary<string, XElement>? LoadMembers(string referenceAssemblyPath)
    {
        var xmlDocPath = ResolveXmlDocPath(referenceAssemblyPath);
        if (xmlDocPath == null)
        {
            return null;
        }

        try
        {
            // Index member elements keyed by their 'name' attribute, using the same
            // first-wins duplicate handling as XmlDocReader's own constructor so that a
            // malformed external XML doc file with repeated name attributes does not throw.
            var doc = XDocument.Load(xmlDocPath);
            return doc.Descendants("member")
                .Where(m => m.Attribute("name") != null)
                .GroupBy(m => m.Attribute("name")!.Value)
                .ToDictionary(g => g.Key, g => g.First());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            // An unreadable or corrupt external XML doc file must not fail the whole
            // generation run — degrade to "no external documentation available" instead.
            return null;
        }
    }

    /// <summary>
    ///     Determines the path to the XML documentation file for a reference assembly, trying the
    ///     conventional sibling <c>.xml</c> file first, then a <c>ref/</c>&#8596;<c>lib/</c>
    ///     folder-segment swap fallback.
    /// </summary>
    /// <param name="referenceAssemblyPath">Path to the reference assembly DLL.</param>
    /// <returns>The path to an existing XML documentation file, or <c>null</c> if none was found.</returns>
    private static string? ResolveXmlDocPath(string referenceAssemblyPath)
    {
        var siblingPath = Path.ChangeExtension(referenceAssemblyPath, ".xml");
        if (File.Exists(siblingPath))
        {
            return siblingPath;
        }

        var swappedPath = SwapRefLibSegment(referenceAssemblyPath);
        if (swappedPath != null)
        {
            var swappedXmlPath = Path.ChangeExtension(swappedPath, ".xml");
            if (File.Exists(swappedXmlPath))
            {
                return swappedXmlPath;
            }
        }

        return null;
    }

    /// <summary>
    ///     Swaps the LAST path segment named exactly <c>ref</c> or <c>lib</c> for the other,
    ///     mimicking the folder layout convention used by many NuGet packages where compile-time
    ///     reference assemblies live under <c>ref/</c> and runtime assemblies (often bundled with
    ///     the actual XML documentation) live under <c>lib/</c>, or vice versa.
    /// </summary>
    /// <remarks>
    ///     Scans from the end of the path (nearest the assembly file) backwards so that an
    ///     unrelated, earlier path segment that happens to be named <c>ref</c> or <c>lib</c> (for
    ///     example a user or drive folder such as <c>/home/lib/.nuget/packages/Pkg/ref/net8.0</c>)
    ///     is never matched in preference to the actual NuGet package-layout segment, which is
    ///     always the one closest to the assembly file itself.
    /// </remarks>
    /// <param name="path">The original assembly path.</param>
    /// <returns>The path with the swapped segment, or <c>null</c> when no <c>ref</c>/<c>lib</c> segment is present.</returns>
    private static string? SwapRefLibSegment(string path)
    {
        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        var segments = path.Split(separators);

        for (var i = segments.Length - 1; i >= 0; i--)
        {
            if (string.Equals(segments[i], "ref", StringComparison.OrdinalIgnoreCase))
            {
                segments[i] = "lib";
                return string.Join(Path.DirectorySeparatorChar, segments);
            }

            if (string.Equals(segments[i], "lib", StringComparison.OrdinalIgnoreCase))
            {
                segments[i] = "ref";
                return string.Join(Path.DirectorySeparatorChar, segments);
            }
        }

        return null;
    }
}

