using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;

namespace ApiMark.DotNet;

/// <summary>Reads and indexes a .NET XML documentation file for fast member-level lookups.</summary>
/// <remarks>
///     Safe for concurrent reads after construction when no external member lookup is
///     configured; all of this class's own fields are set once during the constructor and
///     are never subsequently mutated. When <c>externalMemberLookup</c> is supplied (typically
///     backed by <see cref="ExternalXmlDocResolver.TryGetMember(string)"/>), concurrent calls into this
///     reader are only as safe as the supplied delegate: <see cref="ExternalXmlDocResolver"/>
///     itself is documented as not safe for concurrent use because its internal caches are
///     plain, non-thread-safe dictionaries. ApiMark's current generation pipeline only ever
///     accesses a single <see cref="XmlDocReader"/> instance from a single thread (each MSBuild
///     task invocation spawns an isolated <c>ApiMark.Tool</c> child process), so this is not a
///     practical limitation today.
///     <para>
///     A bare (<c>cref</c>-less) <c>&lt;inheritdoc /&gt;</c> is resolved purely by looking up its
///     member ID in <c>_inheritanceChain</c>; this class does not itself walk assembly metadata or
///     distinguish primary-assembly members from externally-resolved ones. Whether resolution can
///     continue across multiple external hops therefore depends entirely on how completely the
///     supplied <c>_inheritanceChain</c> was populated by its caller: <c>DotNetGenerator</c>
///     recursively walks resolvable external base types/interfaces (see
///     <c>DotNetGenerator.RecurseBaseTypeInheritanceEntries</c>) when building the chain it passes in,
///     so a chain entry commonly does exist for an externally-resolved member whose own base type
///     is itself resolvable via Mono.Cecil — allowing a bare <c>&lt;inheritdoc /&gt;</c> to resolve
///     across more than one external hop in that case. If the chain has no entry for a given
///     member ID (for example because an external base type could not be resolved, such as when it
///     lives in an assembly outside the configured reference paths), resolution stops there and
///     returns no content for that hop. An explicit <c>cref</c> at any hop is unaffected either way
///     and resolves correctly across any number of hops, because <c>cref</c> targets recurse
///     directly through <see cref="ResolveMemberElement"/> rather than through
///     <c>_inheritanceChain</c>.
///     </para>
/// </remarks>
public sealed class XmlDocReader
{
    /// <summary>Index of documentation members keyed by their XML doc identifier.</summary>
    private readonly Dictionary<string, XElement> _members;

    /// <summary>
    ///     Optional inheritance chain mapping member IDs to ordered candidate base member IDs.
    ///     Used to resolve bare <c>&lt;inheritdoc /&gt;</c> elements that carry no <c>cref</c>
    ///     attribute. When <c>null</c>, bare inheritdoc elements that reference no explicit target
    ///     produce <c>null</c> or empty results rather than throwing.
    /// </summary>
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>>? _inheritanceChain;

    /// <summary>
    ///     Optional fallback delegate used to resolve a member ID against externally referenced
    ///     assemblies' XML documentation (e.g. NuGet package dependencies) when the member ID is
    ///     not present in this reader's own index. Typically backed by
    ///     <see cref="ExternalXmlDocResolver.TryGetMember(string)"/>. When <c>null</c>, unresolved member
    ///     IDs simply produce <c>null</c>, matching prior behavior exactly.
    /// </summary>
    private readonly Func<string, XElement?>? _externalMemberLookup;

    /// <summary>Initializes a new instance of <see cref="XmlDocReader"/> from the given path.</summary>
    /// <remarks>
    ///     When duplicate member names appear in the XML doc file, the first occurrence is used
    ///     and subsequent duplicates are silently discarded. This is a defensive policy for
    ///     malformed but real-world XML doc files where the compiler emits the same member ID
    ///     more than once (e.g., due to partial-class splits or tooling bugs).
    ///     <para>
    ///     This overload's signature is preserved exactly as originally published (before external
    ///     member lookup support was added) to remain binary-compatible with callers compiled
    ///     against earlier releases. It delegates to the external-lookup-capable overload with a
    ///     <c>null</c> lookup, which is equivalent to prior behavior.
    ///     </para>
    /// </remarks>
    /// <param name="xmlDocPath">Path to the XML documentation file.</param>
    /// <param name="inheritanceChain">
    ///     Optional map of member ID to ordered list of base member IDs. Used to resolve
    ///     bare <c>&lt;inheritdoc /&gt;</c> elements that carry no <c>cref</c> attribute.
    ///     When <c>null</c>, bare inheritdoc resolution returns <c>null</c> or empty.
    /// </param>
    /// <exception cref="FileNotFoundException">Thrown when <paramref name="xmlDocPath"/> does not exist.</exception>
    public XmlDocReader(
        string xmlDocPath,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? inheritanceChain = null)
        : this(xmlDocPath, inheritanceChain, externalMemberLookup: null)
    {
    }

    /// <summary>
    ///     Initializes a new instance of <see cref="XmlDocReader"/> from the given path, with an
    ///     optional fallback delegate for resolving member IDs against externally referenced
    ///     assemblies' XML documentation.
    /// </summary>
    /// <remarks>
    ///     When duplicate member names appear in the XML doc file, the first occurrence is used
    ///     and subsequent duplicates are silently discarded. This is a defensive policy for
    ///     malformed but real-world XML doc files where the compiler emits the same member ID
    ///     more than once (e.g., due to partial-class splits or tooling bugs).
    ///     <para>
    ///     Both parameters are required (no defaults) on this overload so that it never overlaps
    ///     with, or creates ambiguity against, the original 2-parameter overload above — a caller
    ///     wanting the external lookup fallback must always supply both an inheritance chain
    ///     (or explicit <c>null</c>) and a lookup delegate.
    ///     </para>
    /// </remarks>
    /// <param name="xmlDocPath">Path to the XML documentation file.</param>
    /// <param name="inheritanceChain">
    ///     Optional map of member ID to ordered list of base member IDs. Used to resolve
    ///     bare <c>&lt;inheritdoc /&gt;</c> elements that carry no <c>cref</c> attribute.
    ///     When <c>null</c>, bare inheritdoc resolution returns <c>null</c> or empty.
    /// </param>
    /// <param name="externalMemberLookup">
    ///     Optional fallback delegate consulted only while resolving an <c>&lt;inheritdoc /&gt;</c>
    ///     resolution target that is not present in this reader's own index — never for a
    ///     top-level lookup (e.g. <c>GetSummary</c>/<c>GetRemarks</c>) of a member that is simply
    ///     undocumented locally. Used to resolve <c>&lt;inheritdoc /&gt;</c> references that target
    ///     base types or members defined in externally referenced assemblies. When <c>null</c>, no
    ///     external fallback is attempted and behavior is identical to prior releases.
    /// </param>
    /// <exception cref="FileNotFoundException">Thrown when <paramref name="xmlDocPath"/> does not exist.</exception>
    public XmlDocReader(
        string xmlDocPath,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? inheritanceChain,
        Func<string, XElement?>? externalMemberLookup)
    {
        // Verify the file exists before attempting to parse — a missing doc file
        // is a configuration error that callers should handle explicitly
        if (!File.Exists(xmlDocPath))
        {
            throw new FileNotFoundException("XML documentation file not found.", xmlDocPath);
        }

        // Build an index of member elements keyed by their 'name' attribute so
        // individual lookups are O(1) rather than scanning all descendants each call.
        // GroupBy before ToDictionary provides first-wins duplicate handling so a
        // malformed XML doc file with repeated name attributes does not throw.
        var doc = XDocument.Load(xmlDocPath);
        _members = doc.Descendants("member")
            .Where(m => m.Attribute("name") != null)
            .GroupBy(m => m.Attribute("name")!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        _inheritanceChain = inheritanceChain;
        _externalMemberLookup = externalMemberLookup;
    }

    /// <summary>Returns the trimmed summary text for <paramref name="memberId"/>, or <c>null</c> if absent.</summary>
    /// <remarks>
    ///     Summary text is always normalized to a single line because this accessor feeds compact
    ///     contexts (table cells and quick-index bullet lines) where real Markdown block structure
    ///     cannot render. A <c>&lt;list&gt;</c> inside the summary therefore renders as inline
    ///     numbered markers (e.g. <c>(1) item one (2) item two</c>) rather than a real list — see
    ///     <see cref="GetSummaryMarkdown(string)"/> for the full multi-line rendering used in member-detail
    ///     page bodies. When the member carries an <c>&lt;inheritdoc /&gt;</c> element, the summary
    ///     is resolved from the referenced or inherited base member recursively.
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier (e.g. <c>T:MyNamespace.MyClass</c>).</param>
    /// <returns>Single-line trimmed summary text, or <c>null</c>.</returns>
    public string? GetSummary(string memberId) => GetSummary(memberId, null);

    /// <summary>
    ///     Overload of <see cref="GetSummary(string)"/> accepting an optional
    ///     <see cref="CrefLinkContext"/>, used internally by the gradual-disclosure emitter to
    ///     enable <c>&lt;see cref&gt;</c>/<c>&lt;seealso cref&gt;</c> cross-reference linking.
    ///     Kept as a separate internal overload (rather than adding the parameter to the public
    ///     method) so the public API surface of <see cref="XmlDocReader"/> is completely
    ///     unaffected by this feature.
    /// </summary>
    /// <param name="memberId">The XML doc member identifier (e.g. <c>T:MyNamespace.MyClass</c>).</param>
    /// <param name="linkContext">
    ///     Optional context enabling <c>&lt;see cref&gt;</c>/<c>&lt;seealso cref&gt;</c> references
    ///     within the summary to render as real relative Markdown links instead of their default
    ///     code-span-only fallback. See <see cref="CrefLinkContext"/>. <see langword="null"/>
    ///     preserves the exact current fallback rendering unconditionally — this is how
    ///     single-file emitter output remains completely unaffected by this feature.
    /// </param>
    /// <returns>Single-line trimmed summary text, or <c>null</c>.</returns>
    internal string? GetSummary(string memberId, CrefLinkContext? linkContext)
    {
        var member = ResolveMemberElement(memberId, new HashSet<string>(StringComparer.Ordinal));
        if (member == null)
        {
            return null;
        }

        // Use single-line normalization — summaries must fit on one line by convention
        return GetSingleLineDocumentationText(member.Element("summary"), linkContext);
    }

    /// <summary>
    ///     Returns the full multi-line Markdown rendering of the summary for
    ///     <paramref name="memberId"/>, or <c>null</c> if absent.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="GetSummary(string)"/> (always collapsed to a single line for compact
    ///     contexts), this preserves real multi-line Markdown structure — a <c>&lt;list&gt;</c>
    ///     renders as a blank-line-separated block with each item on its own line, and any prose
    ///     following the list renders as a separate paragraph — matching how <see cref="GetRemarks(string)"/>
    ///     already renders lists. Intended for member-detail-page bodies, where the summary is the
    ///     primary prose rather than a compact table cell. When the member carries an
    ///     <c>&lt;inheritdoc /&gt;</c> element, the summary is resolved from the referenced or
    ///     inherited base member recursively, matching <see cref="GetSummary(string)"/>.
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier (e.g. <c>T:MyNamespace.MyClass</c>).</param>
    /// <returns>Multi-line Markdown summary text, or <c>null</c>.</returns>
    public string? GetSummaryMarkdown(string memberId) => GetSummaryMarkdown(memberId, null);

    /// <summary>
    ///     Overload of <see cref="GetSummaryMarkdown(string)"/> accepting an optional
    ///     <see cref="CrefLinkContext"/>. See <see cref="GetSummary(string, CrefLinkContext?)"/>.
    /// </summary>
    /// <param name="memberId">The XML doc member identifier (e.g. <c>T:MyNamespace.MyClass</c>).</param>
    /// <param name="linkContext">See <see cref="GetSummary(string, CrefLinkContext?)"/>.</param>
    /// <returns>Multi-line Markdown summary text, or <c>null</c>.</returns>
    internal string? GetSummaryMarkdown(string memberId, CrefLinkContext? linkContext)
    {
        var member = ResolveMemberElement(memberId, new HashSet<string>(StringComparer.Ordinal));
        if (member == null)
        {
            return null;
        }

        return GetDocumentationText(member.Element("summary"), linkContext);
    }

    /// <summary>Returns the trimmed remarks text for <paramref name="memberId"/>, or <c>null</c> if absent.</summary>
    /// <remarks>
    ///     When the member carries an <c>&lt;inheritdoc /&gt;</c> element, the remarks are
    ///     resolved from the referenced or inherited base member recursively.
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>Trimmed remarks text, or <c>null</c>.</returns>
    public string? GetRemarks(string memberId) => GetRemarks(memberId, null);

    /// <summary>
    ///     Overload of <see cref="GetRemarks(string)"/> accepting an optional
    ///     <see cref="CrefLinkContext"/>. See <see cref="GetSummary(string, CrefLinkContext?)"/>.
    /// </summary>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <param name="linkContext">See <see cref="GetSummary(string, CrefLinkContext?)"/>.</param>
    /// <returns>Trimmed remarks text, or <c>null</c>.</returns>
    internal string? GetRemarks(string memberId, CrefLinkContext? linkContext)
    {
        var member = ResolveMemberElement(memberId, new HashSet<string>(StringComparer.Ordinal));
        if (member == null)
        {
            return null;
        }

        return GetDocumentationText(member.Element("remarks"), linkContext);
    }

    /// <summary>Returns all <c>cref</c> attribute values from <c>&lt;exception&gt;</c> elements for <paramref name="memberId"/>.</summary>
    /// <remarks>
    ///     When the member carries an <c>&lt;inheritdoc /&gt;</c> element, exceptions are
    ///     resolved from the referenced or inherited base member recursively.
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>A read-only list of exception cref strings.</returns>
    public IReadOnlyList<string> GetExceptions(string memberId)
    {
        var member = ResolveMemberElement(memberId, new HashSet<string>(StringComparer.Ordinal));
        if (member == null)
        {
            return Array.Empty<string>();
        }

        return member.Elements("exception")
            .Select(e => e.Attribute("cref")?.Value ?? string.Empty)
            .Where(v => v.Length > 0)
            .ToList();
    }

    /// <summary>Returns exception types and descriptions from <c>&lt;exception&gt;</c> elements for <paramref name="memberId"/>.</summary>
    /// <remarks>
    ///     When the member carries an <c>&lt;inheritdoc /&gt;</c> element, exception details are
    ///     resolved from the referenced or inherited base member recursively. The description uses
    ///     the single-line rendering (<see cref="GetSingleLineDocumentationText"/>), not the
    ///     multi-line <see cref="GetDocumentationText"/> used by <see cref="GetRemarks(string)"/> and
    ///     <see cref="GetSummaryMarkdown(string)"/>: every caller of this method writes the description into a
    ///     raw pipe-delimited Markdown table row (see <c>FileMarkdownWriter.WriteTable</c>), which
    ///     does not escape embedded newlines — a fenced code block or <c>&lt;br/&gt;</c>-driven
    ///     paragraph break in the description would otherwise inject extra rows and corrupt the
    ///     table.
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>A read-only list of (Type, Description) tuples.</returns>
    public IReadOnlyList<(string Type, string? Description)> GetExceptionDetails(string memberId)
    {
        var member = ResolveMemberElement(memberId, new HashSet<string>(StringComparer.Ordinal));
        if (member == null)
        {
            return Array.Empty<(string, string?)>();
        }

        return member.Elements("exception")
            .Select<XElement, (string Type, string? Description)>(e =>
            {
                var cref = e.Attribute("cref")?.Value;
                var type = string.IsNullOrWhiteSpace(cref) ? string.Empty : FormatCref(cref).Text;
                var description = GetSingleLineDocumentationText(e);
                return (type, description);
            })
            .Where(e => e.Type.Length > 0)
            .ToList();
    }

    /// <summary>Returns parameter names and descriptions for <paramref name="memberId"/>.</summary>
    /// <remarks>
    ///     When the member carries an <c>&lt;inheritdoc /&gt;</c> element, parameters are resolved
    ///     from the referenced or inherited base member recursively. The description uses the
    ///     single-line rendering (<see cref="GetSingleLineDocumentationText"/>) for the same reason
    ///     as <see cref="GetExceptionDetails"/>: every caller writes the description into a raw
    ///     pipe-delimited Markdown table row, which cannot safely contain an embedded literal
    ///     newline.
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>A read-only list of (Name, Description) tuples.</returns>
    public IReadOnlyList<(string Name, string? Description)> GetParams(string memberId) => GetParams(memberId, null);

    /// <summary>
    ///     Overload of <see cref="GetParams(string)"/> accepting an optional
    ///     <see cref="CrefLinkContext"/>. See <see cref="GetSummary(string, CrefLinkContext?)"/>.
    /// </summary>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <param name="linkContext">See <see cref="GetSummary(string, CrefLinkContext?)"/>.</param>
    /// <returns>A read-only list of (Name, Description) tuples.</returns>
    internal IReadOnlyList<(string Name, string? Description)> GetParams(string memberId, CrefLinkContext? linkContext)
    {
        var member = ResolveMemberElement(memberId, new HashSet<string>(StringComparer.Ordinal));
        if (member == null)
        {
            return Array.Empty<(string, string?)>();
        }

        return member.Elements("param")
            .Select<XElement, (string Name, string? Description)>(p => (
                p.Attribute("name")?.Value ?? string.Empty,
                GetSingleLineDocumentationText(p, linkContext)))
            .Where(p => p.Name.Length > 0)
            .ToList();
    }

    /// <summary>Returns the trimmed returns text for <paramref name="memberId"/>, or <c>null</c> if absent.</summary>
    /// <remarks>
    ///     When the member carries an <c>&lt;inheritdoc /&gt;</c> element, the returns text is
    ///     resolved from the referenced or inherited base member recursively.
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>Trimmed returns text, or <c>null</c>.</returns>
    public string? GetReturns(string memberId) => GetReturns(memberId, null);

    /// <summary>
    ///     Overload of <see cref="GetReturns(string)"/> accepting an optional
    ///     <see cref="CrefLinkContext"/>. See <see cref="GetSummary(string, CrefLinkContext?)"/>.
    /// </summary>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <param name="linkContext">See <see cref="GetSummary(string, CrefLinkContext?)"/>.</param>
    /// <returns>Trimmed returns text, or <c>null</c>.</returns>
    internal string? GetReturns(string memberId, CrefLinkContext? linkContext)
    {
        var member = ResolveMemberElement(memberId, new HashSet<string>(StringComparer.Ordinal));
        if (member == null)
        {
            return null;
        }

        return GetDocumentationText(member.Element("returns"), linkContext);
    }

    /// <summary>Returns the trimmed example text for <paramref name="memberId"/>, or <c>null</c> if absent.</summary>
    /// <remarks>
    ///     Returns <c>null</c> when the <c>&lt;example&gt;</c> element is absent or contains only
    ///     whitespace, matching the null-for-missing contract of <see cref="GetSummary(string)"/>,
    ///     <see cref="GetRemarks(string)"/>, and <see cref="GetReturns(string)"/>.
    ///     When the member carries an <c>&lt;inheritdoc /&gt;</c> element, the example is
    ///     resolved from the referenced or inherited base member recursively.
    ///     <para>
    ///     This method has no <see cref="CrefLinkContext"/> overload: it returns the raw,
    ///     untransformed element value rather than routing through
    ///     <see cref="AppendElementText"/>/<see cref="GetInlineReferenceText"/>, so a
    ///     <c>&lt;see cref&gt;</c> inside an <c>&lt;example&gt;</c> element is never linkified
    ///     here. Use <see cref="GetExampleParts(string)"/> for structured example rendering that
    ///     does honor a <see cref="CrefLinkContext"/>.
    ///     </para>
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>Trimmed example text, or <c>null</c> when the element is absent or whitespace-only.</returns>
    public string? GetExample(string memberId)
    {
        var member = ResolveMemberElement(memberId, new HashSet<string>(StringComparer.Ordinal));
        if (member == null)
        {
            return null;
        }

        var el = member.Element("example");

        // Treat whitespace-only content the same as a missing element — callers rely on
        // null to indicate no example is present, so an empty-after-trim value must not
        // be returned as an empty string
        var text = el?.Value.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    ///     Returns the structured example content for <paramref name="memberId"/> as a list of
    ///     (IsCode, Content) parts. <c>IsCode = true</c> indicates a fenced code block; <c>false</c>
    ///     indicates a prose paragraph.
    /// </summary>
    /// <remarks>
    ///     When the <c>&lt;example&gt;</c> element contains no <c>&lt;code&gt;</c> children, the
    ///     entire text is returned as a single code part. When <c>&lt;code&gt;</c> children are
    ///     present, consecutive non-<c>&lt;code&gt;</c> nodes (text and inline elements such as
    ///     <c>&lt;see cref="..." /&gt;</c>, <c>&lt;c&gt;</c>, <c>&lt;paramref&gt;</c>) are
    ///     accumulated and rendered together into a single prose part via the same
    ///     <c>AppendNodeText</c> / <c>AppendElementText</c> pipeline used by other documentation
    ///     accessors. This preserves inline references and inline code within a prose run rather
    ///     than emitting them as isolated, broken fragments.
    ///     When the member carries an <c>&lt;inheritdoc /&gt;</c> element, the example parts are
    ///     resolved from the referenced or inherited base member recursively.
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>
    ///     A list of (IsCode, Content) pairs, or an empty list when the member is absent or has
    ///     no <c>&lt;example&gt;</c> element.
    /// </returns>
    public IReadOnlyList<(bool IsCode, string Content)> GetExampleParts(string memberId) => GetExampleParts(memberId, null);

    /// <summary>
    ///     Overload of <see cref="GetExampleParts(string)"/> accepting an optional
    ///     <see cref="CrefLinkContext"/>. See <see cref="GetSummary(string, CrefLinkContext?)"/>.
    /// </summary>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <param name="linkContext">See <see cref="GetSummary(string, CrefLinkContext?)"/>.</param>
    /// <returns>
    ///     A list of (IsCode, Content) pairs, or an empty list when the member is absent or has
    ///     no <c>&lt;example&gt;</c> element.
    /// </returns>
    internal IReadOnlyList<(bool IsCode, string Content)> GetExampleParts(string memberId, CrefLinkContext? linkContext)
    {
        var member = ResolveMemberElement(memberId, new HashSet<string>(StringComparer.Ordinal));
        var el = member?.Element("example");
        if (el == null)
        {
            return Array.Empty<(bool, string)>();
        }

        // When no <code> children exist, treat the entire value as a single code block
        return el.Elements("code").Any()
            ? BuildMixedExampleParts(el, linkContext)
            : BuildSingleCodeExamplePart(el);
    }

    /// <summary>
    ///     Builds a single-part example result by treating the entire <paramref name="el"/> value
    ///     as one code block. Used when the <c>&lt;example&gt;</c> element has no <c>&lt;code&gt;</c> children.
    /// </summary>
    /// <param name="el">The <c>&lt;example&gt;</c> element.</param>
    /// <returns>A single-element code part list, or an empty list when the dedented text is empty.</returns>
    private static IReadOnlyList<(bool IsCode, string Content)> BuildSingleCodeExamplePart(XElement el)
    {
        var text = DedentCode(el.Value);
        return string.IsNullOrEmpty(text)
            ? Array.Empty<(bool, string)>()
            : [(true, text)];
    }

    /// <summary>
    ///     Builds ordered example parts from an <c>&lt;example&gt;</c> element containing a mix of
    ///     <c>&lt;code&gt;</c> blocks and prose (text nodes, <c>&lt;para&gt;</c>, and inline elements).
    /// </summary>
    /// <remarks>
    ///     Consecutive non-<c>&lt;code&gt;</c> nodes are accumulated and rendered together into a
    ///     single prose part via the same <c>AppendNodeText</c> / <c>AppendElementText</c> pipeline
    ///     used by other documentation accessors. This preserves inline references and inline code
    ///     within a prose run rather than emitting them as isolated, broken fragments.
    /// </remarks>
    /// <param name="el">The <c>&lt;example&gt;</c> element.</param>
    /// <param name="linkContext">See <see cref="GetSummary(string)"/>.</param>
    /// <returns>Ordered list of (IsCode, Content) parts.</returns>
    private static List<(bool IsCode, string Content)> BuildMixedExampleParts(XElement el, CrefLinkContext? linkContext)
    {
        var parts = new List<(bool IsCode, string Content)>();
        var proseBuilder = new StringBuilder();

        // Flushes any accumulated prose content to the parts list and resets the builder
        void FlushProse()
        {
            var text = NormalizeSingleLine(proseBuilder.ToString());
            proseBuilder.Clear();
            if (text.Length > 0)
            {
                parts.Add((false, text));
            }
        }

        foreach (var node in el.Nodes())
        {
            if (node is XElement codeElement && codeElement.Name.LocalName == "code")
            {
                // Emit any accumulated prose before this code block
                FlushProse();

                var code = DedentCode(codeElement.Value);
                if (code.Length > 0)
                {
                    parts.Add((true, code));
                }
            }
            else if (node is XElement paraElement && paraElement.Name.LocalName == "para")
            {
                // Render the paragraph into the accumulator, then flush so that each <para>
                // becomes a distinct prose part rather than merging with adjacent content
                AppendNodeText(proseBuilder, paraElement.Nodes(), linkContext: linkContext);
                FlushProse();
            }
            else
            {
                // Text nodes and inline elements — accumulate for combined prose rendering
                AppendNodeText(proseBuilder, node, linkContext: linkContext);
            }
        }

        // Flush any remaining prose after the last node
        FlushProse();

        return parts;
    }

    /// <summary>
    ///     Resolves the effective member element for <paramref name="memberId"/>, following
    ///     <c>&lt;inheritdoc /&gt;</c> references recursively with cycle detection.
    /// </summary>
    /// <remarks>
    ///     Resolution proceeds as follows:
    ///     <list type="number">
    ///         <item>If <paramref name="memberId"/> is already in <paramref name="visited"/>, return
    ///               <c>null</c> to break cycles.</item>
    ///         <item>If the member is absent from the local index and <paramref name="allowExternalLookup"/>
    ///               is <c>true</c>, fall back to the injected external member lookup delegate (if any);
    ///               if that also misses (or <paramref name="allowExternalLookup"/> is <c>false</c>),
    ///               return <c>null</c>.</item>
    ///         <item>If the member has no <c>&lt;inheritdoc /&gt;</c> child, return the member element directly.</item>
    ///         <item>If a <c>cref</c> attribute is present, resolve the cref target recursively.</item>
    ///         <item>Otherwise, try each candidate in the injected inheritance chain in priority order.</item>
    ///         <item>When a <c>path</c> attribute is present, apply it as an XPath expression to the resolved
    ///               source element and return the matches wrapped in a synthetic <c>&lt;member&gt;</c> element.</item>
    ///     </list>
    ///     <para>
    ///     The external member lookup delegate is only ever consulted for <c>&lt;inheritdoc /&gt;</c>
    ///     target resolution — i.e. when <paramref name="allowExternalLookup"/> is passed as <c>true</c>
    ///     by <see cref="ResolveInheritdocSource"/>'s two recursive call sites. Every top-level entry
    ///     point (<c>GetSummary</c>, <c>GetRemarks</c>, etc.) calls this method with the default
    ///     <c>false</c>, so a member that is simply undocumented locally is never satisfied by an
    ///     incidentally-colliding member ID in an externally referenced assembly's XML doc file.
    ///     </para>
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier to resolve.</param>
    /// <param name="visited">Set of member IDs visited on the current resolution path; updated in-place.</param>
    /// <param name="allowExternalLookup">
    ///     Whether a local miss may fall back to the injected external member lookup delegate.
    ///     Only <see cref="ResolveInheritdocSource"/>'s recursive calls (resolving an
    ///     <c>&lt;inheritdoc /&gt;</c> target) pass <c>true</c>; all top-level entry points use the
    ///     default <c>false</c> so external documentation is never attributed to an unrelated,
    ///     merely-undocumented local member.
    /// </param>
    /// <returns>
    ///     The resolved member element (possibly synthetic when a path filter is applied), or
    ///     <c>null</c> when the member is absent, a cycle is detected, or no valid target is found.
    /// </returns>
    private XElement? ResolveMemberElement(string memberId, HashSet<string> visited, bool allowExternalLookup = false)
    {
        // Cycle detection: stop if this ID has already been visited on the current resolution path
        if (!visited.Add(memberId))
        {
            return null;
        }

        if (!_members.TryGetValue(memberId, out var member))
        {
            // Not found locally — fall back to the external resolver (e.g. a referenced
            // NuGet assembly's own XML doc file) only when explicitly permitted (i.e. this
            // call is resolving an <inheritdoc/> target). Top-level lookups for a member
            // that is simply undocumented locally must not be satisfied by an incidentally
            // colliding external member ID.
            member = allowExternalLookup ? _externalMemberLookup?.Invoke(memberId) : null;
            if (member == null)
            {
                return null;
            }
        }

        var inheritdoc = member.Element("inheritdoc");
        if (inheritdoc == null)
        {
            // No inheritdoc — return the member element directly
            return member;
        }

        var source = ResolveInheritdocSource(memberId, inheritdoc, visited);
        if (source == null)
        {
            return null;
        }

        var path = inheritdoc.Attribute("path")?.Value;
        return string.IsNullOrWhiteSpace(path)
            // Absent or whitespace path — treat as no filter and return the full resolved source
            ? source
            : ApplyInheritdocPathFilter(source, path);
    }

    /// <summary>
    ///     Determines the source member for an <c>&lt;inheritdoc /&gt;</c> element: an explicit
    ///     <c>cref</c> target takes priority, otherwise each candidate in the injected inheritance
    ///     chain is tried in priority order.
    /// </summary>
    /// <remarks>
    ///     The bare (<c>cref</c>-less) branch below looks up <paramref name="memberId"/> directly
    ///     in <c>_inheritanceChain</c> and does not itself know or care whether that ID names a
    ///     primary-assembly or an externally-resolved member (see the class-level remarks): if the
    ///     chain has an entry, resolution continues; if not, this method returns <c>null</c> for
    ///     that hop. Because <c>DotNetGenerator</c> populates the chain recursively for resolvable
    ///     external base types/interfaces, a chain entry is commonly present even for an
    ///     externally-resolved member, so a bare <c>&lt;inheritdoc /&gt;</c> can continue across
    ///     more than one external hop when those further base types are themselves resolvable. An
    ///     explicit <c>cref</c> at any hop is unaffected either way, since the branch above
    ///     recurses directly. Both recursive calls below pass <c>allowExternalLookup: true</c>
    ///     because they are, by definition, resolving an <c>&lt;inheritdoc /&gt;</c> target rather
    ///     than performing a top-level lookup.
    /// </remarks>
    /// <param name="memberId">The member ID that carries the <c>&lt;inheritdoc /&gt;</c> element.</param>
    /// <param name="inheritdoc">The <c>&lt;inheritdoc /&gt;</c> element.</param>
    /// <param name="visited">Set of member IDs visited on the current resolution path.</param>
    /// <returns>The resolved source element, or <c>null</c> when no valid target is found.</returns>
    private XElement? ResolveInheritdocSource(string memberId, XElement inheritdoc, HashSet<string> visited)
    {
        var cref = inheritdoc.Attribute("cref")?.Value;
        if (string.IsNullOrWhiteSpace(cref))
        {
            cref = null;
        }

        if (cref != null)
        {
            // Explicit cref target — recurse in case the target itself also inherits. This is
            // resolving an inheritdoc target, so external lookup is permitted.
            return ResolveMemberElement(cref, visited, allowExternalLookup: true);
        }

        if (_inheritanceChain == null || !_inheritanceChain.TryGetValue(memberId, out var targets))
        {
            return null;
        }

        // Bare inheritdoc — try each candidate in priority order, stop at first hit.
        // Use a branch-local copy of visited for each candidate so that failed
        // traversals in one branch do not poison the visited set seen by sibling
        // candidates in the same priority list.
        foreach (var target in targets)
        {
            var branchVisited = new HashSet<string>(visited, StringComparer.Ordinal);
            var source = ResolveMemberElement(target, branchVisited, allowExternalLookup: true);
            if (source != null)
            {
                return source;
            }
        }

        return null;
    }

    /// <summary>
    ///     Applies an <c>&lt;inheritdoc path="..."/&gt;</c> XPath filter to <paramref name="source"/>
    ///     and wraps matching nodes in a synthetic <c>&lt;member&gt;</c> element.
    /// </summary>
    /// <param name="source">The resolved inheritdoc source element.</param>
    /// <param name="path">The XPath expression from the <c>path</c> attribute.</param>
    /// <returns>
    ///     A synthetic <c>&lt;member&gt;</c> element wrapping the matched nodes, or <c>null</c>
    ///     when the XPath expression is invalid or matches no nodes.
    /// </returns>
    private static XElement? ApplyInheritdocPathFilter(XElement source, string path)
    {
        // Guard against invalid XPath expressions in the XML doc file — degrade gracefully to null
        // rather than crashing documentation generation.
        List<XElement> matched;
        try
        {
            matched = source.XPathSelectElements(path).ToList();
        }
        catch (Exception ex) when (ex is System.Xml.XPath.XPathException or ArgumentException)
        {
            return null;
        }

        if (matched.Count == 0)
        {
            return null;
        }

        var synthetic = new XElement("member");
        foreach (var el in matched)
        {
            synthetic.Add(new XElement(el));
        }

        return synthetic;
    }

    /// <summary>
    ///     Extracts the full (potentially multi-line) text content from <paramref name="element"/>,
    ///     normalizes whitespace, and returns <c>null</c> when the result is empty.
    /// </summary>
    /// <remarks>
    ///     Creates a local <c>codeBlocks</c> placeholder map (see <see cref="RegisterCodeBlockPlaceholder"/>)
    ///     so that any fenced Markdown code block produced for a nested <c>&lt;code&gt;</c> element
    ///     survives <see cref="NormalizeDocumentationText"/>'s per-line whitespace collapsing and
    ///     blank-line-run collapsing intact, then restores the real content via
    ///     <see cref="RestoreCodeBlockPlaceholders"/> immediately before returning. This method and
    ///     <see cref="GetSingleLineDocumentationText"/> are the only two call sites that supply a
    ///     non-<see langword="null"/> map; every other caller of
    ///     <see cref="AppendNodeText(StringBuilder, IEnumerable{XNode}, bool, Dictionary{string, string}?, CrefLinkContext?)"/>
    ///     is eventually squashed through <see cref="NormalizeSingleLine"/> directly (not via
    ///     <see cref="GetSingleLineDocumentationText"/>'s protected path), where a fenced block would
    ///     never be valid, so those callers deliberately keep passing <see langword="null"/>.
    /// </remarks>
    /// <param name="element">The XML element whose text content to extract, or <c>null</c>.</param>
    /// <param name="linkContext">See <see cref="GetSummary(string)"/>.</param>
    /// <returns>Normalized text, or <c>null</c> when the element is absent or empty.</returns>
    private static string? GetDocumentationText(XElement? element, CrefLinkContext? linkContext = null)
    {
        if (element == null)
        {
            return null;
        }

        var codeBlocks = new Dictionary<string, string>(StringComparer.Ordinal);
        var builder = new StringBuilder();
        AppendNodeText(builder, element.Nodes(), singleLine: false, codeBlocks, linkContext);
        var text = RestoreCodeBlockPlaceholders(NormalizeDocumentationText(builder.ToString()), codeBlocks);
        return text.Length == 0 ? null : text;
    }

    /// <summary>
    ///     Builds documentation text from <paramref name="element"/> and normalizes it to a
    ///     single line by collapsing all non-empty trimmed lines into one space-separated string.
    /// </summary>
    /// <remarks>
    ///     An inline <c>&lt;code&gt;</c> span's content is protected from <see cref="NormalizeSingleLine"/>'s
    ///     whitespace collapsing via the same placeholder mechanism <see cref="GetDocumentationText"/>
    ///     uses for fenced blocks (see <see cref="RegisterCodeBlockPlaceholder"/>), so significant
    ///     internal spacing inside a code span survives this single-line rendering too.
    /// </remarks>
    /// <param name="element">The XML element whose text content to extract, or <c>null</c>.</param>
    /// <param name="linkContext">See <see cref="GetSummary(string)"/>.</param>
    /// <returns>Single-line trimmed text, or <c>null</c> when the element is absent or empty.</returns>
    private static string? GetSingleLineDocumentationText(XElement? element, CrefLinkContext? linkContext = null)
    {
        if (element == null)
        {
            return null;
        }

        var codeBlocks = new Dictionary<string, string>(StringComparer.Ordinal);
        var builder = new StringBuilder();
        AppendNodeText(builder, element.Nodes(), singleLine: true, codeBlocks, linkContext);
        var text = RestoreCodeBlockPlaceholders(NormalizeSingleLine(builder.ToString()), codeBlocks);
        return text.Length == 0 ? null : text;
    }

    /// <summary>
    ///     Iterates <paramref name="nodes"/> and appends each node's text representation
    ///     to <paramref name="builder"/>, dispatching XML elements to
    ///     <see cref="AppendElementText"/>.
    /// </summary>
    /// <param name="builder">The string builder that accumulates the output text.</param>
    /// <param name="nodes">The sequence of XML nodes to process.</param>
    /// <param name="singleLine">
    ///     When <see langword="true"/>, block-level content such as <c>&lt;list&gt;</c> renders as
    ///     inline numbered markers suitable for a single-line summary/table-cell context instead
    ///     of real multi-line Markdown. Defaults to <see langword="false"/> so existing callers
    ///     (remarks, examples, nested list items) keep their current multi-line rendering.
    /// </param>
    /// <param name="codeBlocks">
    ///     Optional placeholder map used to protect fenced Markdown code blocks (produced for
    ///     multi-line <c>&lt;code&gt;</c> elements) and inline code spans from later whitespace
    ///     normalization. Only <see cref="GetDocumentationText"/> and
    ///     <see cref="GetSingleLineDocumentationText"/> supply a non-<see langword="null"/> map;
    ///     every other caller passes <see langword="null"/>, which forces <c>&lt;code&gt;</c>
    ///     elements to always render as an unprotected inline span instead of a fenced block (see
    ///     <see cref="AppendCodeElementText"/>).
    /// </param>
    /// <param name="linkContext">See <see cref="GetSummary(string)"/>.</param>
    private static void AppendNodeText(
        StringBuilder builder,
        IEnumerable<XNode> nodes,
        bool singleLine = false,
        Dictionary<string, string>? codeBlocks = null,
        CrefLinkContext? linkContext = null)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case XText text:
                    builder.Append(text.Value);
                    break;
                case XElement element:
                    AppendElementText(builder, element, singleLine, codeBlocks, linkContext);
                    break;
            }
        }
    }

    /// <summary>
    ///     Appends a single node's text representation to <paramref name="builder"/>,
    ///     dispatching XML elements to <see cref="AppendElementText"/>.
    ///     Avoids allocating an array when only one node needs to be processed.
    /// </summary>
    /// <param name="builder">The string builder that accumulates the output text.</param>
    /// <param name="node">The single XML node to process.</param>
    /// <param name="singleLine">See <see cref="AppendNodeText(StringBuilder, IEnumerable{XNode}, bool, Dictionary{string, string}?, CrefLinkContext?)"/>.</param>
    /// <param name="codeBlocks">See <see cref="AppendNodeText(StringBuilder, IEnumerable{XNode}, bool, Dictionary{string, string}?, CrefLinkContext?)"/>.</param>
    /// <param name="linkContext">See <see cref="GetSummary(string)"/>.</param>
    private static void AppendNodeText(
        StringBuilder builder,
        XNode node,
        bool singleLine = false,
        Dictionary<string, string>? codeBlocks = null,
        CrefLinkContext? linkContext = null)
    {
        switch (node)
        {
            case XText text:
                builder.Append(text.Value);
                break;
            case XElement element:
                AppendElementText(builder, element, singleLine, codeBlocks, linkContext);
                break;
        }
    }

    /// <summary>
    ///     Appends the text representation of a single XML element to <paramref name="builder"/>,
    ///     applying element-specific rendering rules for inline references, parameter references,
    ///     inline code, paragraphs, and generic XML elements.
    /// </summary>
    /// <param name="builder">The string builder that accumulates the output text.</param>
    /// <param name="element">The XML element to render.</param>
    /// <param name="singleLine">See <see cref="AppendNodeText(StringBuilder, IEnumerable{XNode}, bool, Dictionary{string, string}?, CrefLinkContext?)"/>.</param>
    /// <param name="codeBlocks">See <see cref="AppendNodeText(StringBuilder, IEnumerable{XNode}, bool, Dictionary{string, string}?, CrefLinkContext?)"/>.</param>
    /// <param name="linkContext">See <see cref="GetSummary(string)"/>.</param>
    private static void AppendElementText(
        StringBuilder builder,
        XElement element,
        bool singleLine = false,
        Dictionary<string, string>? codeBlocks = null,
        CrefLinkContext? linkContext = null)
    {
        switch (element.Name.LocalName)
        {
            case "see":
            case "seealso":
                builder.Append(GetInlineReferenceText(element, linkContext));
                break;
            case "paramref":
            case "typeparamref":
                builder.Append(element.Attribute("name")?.Value ?? string.Empty);
                break;
            case "para":
                AppendNodeText(builder, element.Nodes(), singleLine, codeBlocks, linkContext);
                builder.AppendLine();
                break;
            case "c":
                // Render inline code in backticks so markdown consumers display it as
                // monospace text — matches the intent of <c> in XML documentation.
                // Normalize to a single line to prevent stray whitespace or line breaks
                // inside the element from leaking into the backtick span. Skip empty or
                // whitespace-only elements entirely to avoid emitting stray backtick pairs.
                var inlineCode = NormalizeSingleLine(element.Value);
                if (inlineCode.Length > 0)
                {
                    AppendMarkdownCodeSpan(builder, inlineCode);
                }

                break;
            case "br":
                // A <br/> marks an explicit paragraph break. Emit a blank-line separator so
                // the surrounding text renders as two distinct Markdown paragraphs rather than
                // being silently dropped; NormalizeDocumentationText's blank-line-run collapsing
                // keeps runs of several <br/> tags from producing more than one blank line.
                builder.Append("\n\n");
                break;
            case "code":
                AppendCodeElementText(builder, element, singleLine, codeBlocks);
                break;
            case "list":
                AppendListText(builder, element, singleLine, linkContext);
                break;
            default:
                AppendNodeText(builder, element.Nodes(), singleLine, codeBlocks, linkContext);
                break;
        }
    }

    /// <summary>
    ///     Appends the rendering of a <c>&lt;code&gt;</c> element to <paramref name="builder"/>,
    ///     choosing between a fenced Markdown code block and an inline code span.
    /// </summary>
    /// <remarks>
    ///     The content is dedented via <see cref="DedentCode"/> first; whitespace-only content
    ///     contributes nothing, mirroring the empty-skip guard already used for <c>&lt;c&gt;</c>.
    ///     A fenced block is only ever produced when <paramref name="singleLine"/> is
    ///     <see langword="false"/>, <paramref name="codeBlocks"/> is non-<see langword="null"/>
    ///     (i.e. only from <see cref="GetDocumentationText"/>), <em>and</em> the dedented content
    ///     spans multiple lines; every other caller — including table-cell contexts
    ///     (<see cref="GetSingleLineDocumentationText"/>, whose callers such as
    ///     <see cref="GetParams(string)"/> and <see cref="GetExceptionDetails"/> feed raw Markdown table
    ///     rows where an embedded literal newline would corrupt the table), nested list items
    ///     (<see cref="RenderInlineElement"/>), and <c>&lt;example&gt;</c> mixed prose
    ///     (<see cref="BuildMixedExampleParts"/>) — always takes the inline-span branch instead,
    ///     because a fenced block's literal backtick fences and newlines would otherwise corrupt
    ///     that single-line/table-cell output. Whenever <paramref name="codeBlocks"/> is supplied
    ///     (fenced block or inline span alike), the rendered content is registered under a
    ///     placeholder token (see <see cref="RegisterCodeBlockPlaceholder"/>) so it survives the
    ///     caller's subsequent whitespace normalization (<see cref="NormalizeDocumentationText"/> or
    ///     <see cref="NormalizeSingleLine"/>) with its significant internal whitespace intact; when
    ///     no map is supplied (nested list items, mixed example prose), the span is appended
    ///     directly and remains subject to the same whitespace-collapsing limitation <c>&lt;c&gt;</c>
    ///     already has in those contexts.
    /// </remarks>
    /// <param name="builder">The string builder that accumulates the output text.</param>
    /// <param name="element">The <c>&lt;code&gt;</c> element to render.</param>
    /// <param name="singleLine">See <see cref="AppendNodeText(StringBuilder, IEnumerable{XNode}, bool, Dictionary{string, string}?, CrefLinkContext?)"/>.</param>
    /// <param name="codeBlocks">See <see cref="AppendNodeText(StringBuilder, IEnumerable{XNode}, bool, Dictionary{string, string}?, CrefLinkContext?)"/>.</param>
    private static void AppendCodeElementText(
        StringBuilder builder,
        XElement element,
        bool singleLine,
        Dictionary<string, string>? codeBlocks)
    {
        var dedented = DedentCode(element.Value);
        if (dedented.Length == 0)
        {
            return;
        }

        if (!singleLine && codeBlocks != null && dedented.Contains('\n'))
        {
            var fence = new string('`', Math.Max(3, ComputeFenceLength(dedented)));
            var fencedBlock = $"{fence}\n{dedented}\n{fence}";
            builder.Append("\n\n");
            builder.Append(RegisterCodeBlockPlaceholder(codeBlocks, fencedBlock));
            builder.Append("\n\n");
            return;
        }

        // Single-line context, or no embedded newline — always fall back to an inline code
        // span, flattening any embedded newlines first. Unlike NormalizeSingleLine (used for
        // surrounding prose), this must NOT collapse each line's internal whitespace runs: the
        // content is code, where significant spacing (alignment, multiple spaces, tabs) must
        // survive flattening into one line, with only line-boundary whitespace trimmed and lines
        // joined by a single space.
        var inline = dedented.Contains('\n') ? FlattenCodeToSingleLine(dedented) : dedented;
        if (inline.Length == 0)
        {
            return;
        }

        if (codeBlocks != null)
        {
            // Protect the span's significant internal whitespace from the caller's outer
            // whitespace-collapsing normalization by routing it through the same placeholder
            // mechanism used for fenced blocks.
            var spanBuilder = new StringBuilder();
            AppendMarkdownCodeSpan(spanBuilder, inline);
            builder.Append(RegisterCodeBlockPlaceholder(codeBlocks, spanBuilder.ToString()));
            return;
        }

        AppendMarkdownCodeSpan(builder, inline);
    }

    /// <summary>
    ///     Registers <paramref name="content"/> under a new unique placeholder token in
    ///     <paramref name="codeBlocks"/> and returns the token.
    /// </summary>
    /// <remarks>
    ///     The token is built from a non-whitespace control character (<c>\u0001</c>), a fixed
    ///     literal prefix, and a running counter, so it is immune to <see cref="CollapseWhitespace"/>
    ///     (which only touches whitespace runs) and to <c>Trim()</c> (it neither starts nor ends
    ///     with whitespace), and occupies a single line (contains no <c>\n</c>) so it passes through
    ///     <see cref="NormalizeDocumentationText"/>'s per-line pipeline and blank-line-run collapsing
    ///     completely unchanged. <see cref="RestoreCodeBlockPlaceholders"/> substitutes the real
    ///     content back in once normalization has completed.
    /// </remarks>
    /// <param name="codeBlocks">The placeholder map to register the content into.</param>
    /// <param name="content">The real (fenced code block) content the token stands in for.</param>
    /// <returns>The placeholder token to append in place of <paramref name="content"/>.</returns>
    private static string RegisterCodeBlockPlaceholder(Dictionary<string, string> codeBlocks, string content)
    {
        var token = $"\u0001codeblock{codeBlocks.Count}\u0001";
        codeBlocks[token] = content;
        return token;
    }

    /// <summary>
    ///     Substitutes every placeholder token registered via
    ///     <see cref="RegisterCodeBlockPlaceholder"/> back to its real content in
    ///     <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The normalized text that may contain placeholder tokens.</param>
    /// <param name="codeBlocks">The placeholder map populated during rendering.</param>
    /// <returns><paramref name="text"/> with every placeholder token replaced by its real content.</returns>
    private static string RestoreCodeBlockPlaceholders(string text, Dictionary<string, string> codeBlocks)
    {
        if (codeBlocks.Count == 0)
        {
            return text;
        }

        foreach (var (token, content) in codeBlocks)
        {
            text = text.Replace(token, content, StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>
    ///     Appends a Markdown rendering of a <c>&lt;list&gt;</c> element to
    ///     <paramref name="builder"/>, dispatching on the <c>type</c> attribute
    ///     (<c>bullet</c>, <c>number</c>, or <c>table</c>; defaulting to <c>bullet</c>).
    /// </summary>
    /// <remarks>
    ///     In multi-line mode (<paramref name="singleLine"/> is <see langword="false"/>) the
    ///     rendered block is wrapped in blank lines so that, after
    ///     <see cref="NormalizeDocumentationText"/> trims the string boundaries and
    ///     <c>FileMarkdownWriter.WriteParagraph</c> writes it verbatim, the list is
    ///     separated from surrounding prose and renders as valid CommonMark. Nested
    ///     inline elements inside <c>&lt;term&gt;</c>/<c>&lt;description&gt;</c>/<c>&lt;item&gt;</c>
    ///     (such as <c>&lt;c&gt;</c>, <c>&lt;see&gt;</c>, and <c>&lt;paramref&gt;</c>) are rendered
    ///     via the existing <see cref="AppendNodeText(StringBuilder, IEnumerable{XNode}, bool, Dictionary{string, string}?, CrefLinkContext?)"/> dispatch.
    ///     In single-line mode (used for table cells and <see cref="GetSummary(string)"/>), a real
    ///     multi-line list would be collapsed into unreadable run-together text by
    ///     <see cref="NormalizeSingleLine"/>, so the list instead renders inline as numbered
    ///     markers — e.g. <c>(1) item one (2) item two</c> — for every list <c>type</c>
    ///     (<c>bullet</c>, <c>number</c>, and <c>table</c> alike).
    /// </remarks>
    /// <param name="builder">The string builder that accumulates the output text.</param>
    /// <param name="element">The <c>&lt;list&gt;</c> element to render.</param>
    /// <param name="singleLine">
    ///     When <see langword="true"/>, renders the list as inline numbered markers instead of a
    ///     real multi-line Markdown block.
    /// </param>
    /// <param name="linkContext">See <see cref="GetSummary(string, CrefLinkContext?)"/>.</param>
    private static void AppendListText(StringBuilder builder, XElement element, bool singleLine, CrefLinkContext? linkContext = null)
    {
        if (singleLine)
        {
            AppendInlineMarkerList(builder, element, linkContext);
            return;
        }

        var listType = element.Attribute("type")?.Value;

        // Separate the list block from any preceding prose so Markdown treats it as a
        // distinct block; NormalizeDocumentationText preserves the internal blank line.
        builder.Append("\n\n");

        switch (listType)
        {
            case "table":
                AppendTableList(builder, element, linkContext);
                break;
            case "number":
                AppendMarkerList(builder, element, ordered: true, linkContext);
                break;
            default:
                // "bullet", absent, or any unknown type renders as a bullet list
                AppendMarkerList(builder, element, ordered: false, linkContext);
                break;
        }

        // Separate the list block from any following prose
        builder.Append("\n\n");
    }

    /// <summary>
    ///     Appends an inline, single-line rendering of a <c>&lt;list&gt;</c> element's
    ///     <c>&lt;item&gt;</c> children to <paramref name="builder"/> as numbered markers, e.g.
    ///     <c>(1) item one (2) item two</c>.
    /// </summary>
    /// <remarks>
    ///     Used wherever a real multi-line Markdown list cannot be rendered — table cells and the
    ///     single-line <see cref="GetSummary(string)"/> path — because the surrounding normalizer collapses
    ///     newlines into spaces, which would otherwise run every item together with no separation.
    ///     Applied uniformly regardless of the list's <c>type</c> attribute (<c>bullet</c>,
    ///     <c>number</c>, or <c>table</c>) since none of them can express real block structure
    ///     inline; term/description items reuse <see cref="RenderTermDescription"/> and
    ///     <see cref="FormatTermDescription"/> so inline content (<c>&lt;c&gt;</c>, <c>&lt;see&gt;</c>,
    ///     <c>&lt;paramref&gt;</c>) and term/description pairing render identically to the
    ///     multi-line path. A <c>&lt;listheader&gt;</c>, when present, is rendered first (bolded,
    ///     matching the multi-line <see cref="AppendMarkerList"/> path) so a <c>type="table"</c>
    ///     list's column labels are not silently dropped in this compact representation.
    /// </remarks>
    /// <param name="builder">The string builder that accumulates the output text.</param>
    /// <param name="element">The <c>&lt;list&gt;</c> element whose items to render.</param>
    /// <param name="linkContext">See <see cref="GetSummary(string, CrefLinkContext?)"/>.</param>
    private static void AppendInlineMarkerList(StringBuilder builder, XElement element, CrefLinkContext? linkContext = null)
    {
        var itemTexts = element.Elements("item")
            .Select(item => FormatTermDescription(RenderTermDescription(item, linkContext)))
            .Where(text => text.Length > 0)
            .ToList();

        var header = element.Element("listheader");
        var headerText = header != null ? FormatListHeaderText(RenderTermDescription(header, linkContext)) : string.Empty;

        if (itemTexts.Count == 0 && headerText.Length == 0)
        {
            return;
        }

        // Surround with spaces so the inline list does not collide with adjacent prose once
        // the single-line normalizer collapses newlines
        builder.Append(' ');
        if (headerText.Length > 0)
        {
            // headerText is already fully bolded by FormatListHeaderText; wrapping it again in
            // "**...**" here would produce malformed Markdown.
            builder.Append(headerText);
            if (itemTexts.Count > 0)
            {
                builder.Append(' ');
            }
        }

        for (var i = 0; i < itemTexts.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append('(').Append(i + 1).Append(") ").Append(itemTexts[i]);
        }

        builder.Append(' ');
    }

    /// <summary>
    ///     Appends a bullet (<c>-</c>) or numbered (<c>1.</c>) Markdown list for the
    ///     <c>&lt;item&gt;</c> children of <paramref name="element"/>.
    /// </summary>
    /// <param name="builder">The string builder that accumulates the output text.</param>
    /// <param name="element">The <c>&lt;list&gt;</c> element whose items to render.</param>
    /// <param name="ordered"><see langword="true"/> for a numbered list; <see langword="false"/> for a bullet list.</param>
    /// <param name="linkContext">See <see cref="GetSummary(string, CrefLinkContext?)"/>.</param>
    private static void AppendMarkerList(StringBuilder builder, XElement element, bool ordered, CrefLinkContext? linkContext = null)
    {
        // A leading bold line is emitted for a <listheader> when present so header
        // context is not lost in a bullet/number list (which has no header row).
        var header = element.Element("listheader");
        if (header != null)
        {
            var headerText = FormatListHeaderText(RenderTermDescription(header, linkContext));
            if (headerText.Length > 0)
            {
                // headerText is already fully bolded by FormatListHeaderText; wrapping it again
                // in "**...**" here would produce malformed Markdown.
                builder.Append(headerText).Append("\n\n");
            }
        }

        var marker = ordered ? "1." : "-";
        foreach (var itemText in element.Elements("item").Select(item => FormatTermDescription(RenderTermDescription(item, linkContext))))
        {
            // Skip items that render to no text (for example an empty <description>) so no
            // stray "- "/"1. " marker line is emitted for an item with nothing to show
            if (string.IsNullOrWhiteSpace(itemText))
            {
                continue;
            }

            builder.Append(marker).Append(' ').Append(itemText).Append('\n');
        }
    }

    /// <summary>
    ///     Appends a Markdown table for the <c>&lt;item&gt;</c> children of
    ///     <paramref name="element"/>, using the <c>&lt;listheader&gt;</c> term/description as
    ///     column headers when present and <c>Term</c>/<c>Description</c> otherwise.
    /// </summary>
    /// <param name="builder">The string builder that accumulates the output text.</param>
    /// <param name="element">The <c>&lt;list&gt;</c> element whose items to render as table rows.</param>
    /// <param name="linkContext">See <see cref="GetSummary(string, CrefLinkContext?)"/>.</param>
    private static void AppendTableList(StringBuilder builder, XElement element, CrefLinkContext? linkContext = null)
    {
        var termHeader = "Term";
        var descriptionHeader = "Description";

        var header = element.Element("listheader");
        if (header != null)
        {
            var (headerTerm, headerDescription) = RenderTermDescription(header, linkContext);
            if (headerTerm.Length > 0)
            {
                termHeader = headerTerm;
            }

            if (headerDescription.Length > 0)
            {
                descriptionHeader = headerDescription;
            }
        }

        // Escape literal pipe characters in cell values so they do not break the table
        // structure in Markdown renderers. This mirrors FileMarkdownWriter.WriteTable and is
        // needed because rendered inline content (for example a <c>Flags.A | Flags.B</c> code
        // span or a nested table list) can legitimately contain pipe characters.
        builder.Append("| ").Append(EscapeTableCell(termHeader)).Append(" | ").Append(EscapeTableCell(descriptionHeader)).Append(" |\n");
        builder.Append("| --- | --- |\n");

        foreach (var item in element.Elements("item"))
        {
            var (term, description) = RenderTermDescription(item, linkContext);

            // Skip rows whose term and description both render empty so no blank table row is
            // emitted; the header row is always kept so the table structure remains valid
            if (string.IsNullOrWhiteSpace(term) && string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            builder.Append("| ").Append(EscapeTableCell(term)).Append(" | ").Append(EscapeTableCell(description)).Append(" |\n");
        }
    }

    /// <summary>
    ///     Escapes literal pipe (<c>|</c>) characters in a Markdown table cell value so they do
    ///     not break the table structure. Mirrors the escaping performed by
    ///     <c>FileMarkdownWriter.WriteTable</c>.
    /// </summary>
    /// <param name="cell">The rendered cell value to escape.</param>
    /// <returns>The cell value with every literal pipe replaced by <c>\|</c>.</returns>
    private static string EscapeTableCell(string cell)
    {
        return cell.Replace("|", @"\|", StringComparison.Ordinal);
    }

    /// <summary>
    ///     Renders the <c>&lt;term&gt;</c> and <c>&lt;description&gt;</c> of a list
    ///     <c>&lt;item&gt;</c> or <c>&lt;listheader&gt;</c> element to single-line text via the
    ///     inline-element dispatch. When neither child is present, the element's own inline
    ///     content is returned as the term.
    /// </summary>
    /// <param name="element">The <c>&lt;item&gt;</c> or <c>&lt;listheader&gt;</c> element.</param>
    /// <param name="linkContext">See <see cref="GetSummary(string, CrefLinkContext?)"/>.</param>
    /// <returns>A tuple of the rendered term and description; either may be empty.</returns>
    private static (string Term, string Description) RenderTermDescription(XElement element, CrefLinkContext? linkContext = null)
    {
        var term = element.Element("term");
        var description = element.Element("description");

        // A bare item (no <term>/<description>) contributes its inline content as the term
        if (term == null && description == null)
        {
            return (RenderInlineElement(element, linkContext), string.Empty);
        }

        return (
            term != null ? RenderInlineElement(term, linkContext) : string.Empty,
            description != null ? RenderInlineElement(description, linkContext) : string.Empty);
    }

    /// <summary>
    ///     Combines a rendered term and description into a single-line item string:
    ///     <c>**term** — description</c> when both are present, otherwise whichever is present.
    /// </summary>
    /// <param name="parts">The rendered term and description.</param>
    /// <returns>The combined single-line item text.</returns>
    private static string FormatTermDescription((string Term, string Description) parts)
    {
        var (term, description) = parts;
        if (term.Length > 0 && description.Length > 0)
        {
            return $"**{term}** — {description}";
        }

        return term.Length > 0 ? term : description;
    }

    /// <summary>
    ///     Formats a <c>&lt;listheader&gt;</c>'s rendered term and description as a fully bolded
    ///     label, e.g. <c>**Name** — **Detail**</c>, or just <c>**Name**</c> when only one part is
    ///     present. Unlike <see cref="FormatTermDescription"/> (which bolds only the term, for item
    ///     rows), the entire header label is bolded so it is distinguishable as a heading; callers
    ///     must not wrap the result in an additional <c>**...**</c> pair, since that would produce
    ///     malformed Markdown such as <c>****Name** — Detail**</c>.
    /// </summary>
    /// <param name="parts">The rendered header term and description.</param>
    /// <returns>The fully bolded header label text, or an empty string if both parts are empty.</returns>
    private static string FormatListHeaderText((string Term, string Description) parts)
    {
        var (term, description) = parts;
        if (term.Length > 0 && description.Length > 0)
        {
            return $"**{term}** — **{description}**";
        }

        if (term.Length > 0)
        {
            return $"**{term}**";
        }

        return description.Length > 0 ? $"**{description}**" : string.Empty;
    }

    /// <summary>
    ///     Renders the inline content of <paramref name="element"/> to a single line via the
    ///     shared <see cref="AppendNodeText(StringBuilder, IEnumerable{XNode}, bool, Dictionary{string, string}?, CrefLinkContext?)"/> dispatch, so
    ///     nested inline elements such as <c>&lt;c&gt;</c>, <c>&lt;see&gt;</c>, and
    ///     <c>&lt;paramref&gt;</c> render correctly inside list items.
    /// </summary>
    /// <param name="element">The element whose inline content to render.</param>
    /// <param name="linkContext">See <see cref="GetSummary(string, CrefLinkContext?)"/>.</param>
    /// <returns>The single-line rendered text.</returns>
    private static string RenderInlineElement(XElement element, CrefLinkContext? linkContext = null)
    {
        var builder = new StringBuilder();
        AppendNodeText(builder, element.Nodes(), linkContext: linkContext);
        return NormalizeSingleLine(builder.ToString());
    }

    /// <summary>
    ///     Appends a CommonMark-compliant inline code span for <paramref name="content"/> to
    ///     <paramref name="builder"/>.
    /// </summary>
    /// <remarks>
    ///     The fence length is chosen as one greater than the longest consecutive run of backticks
    ///     in <paramref name="content"/>, so the delimiter can never be confused with content.
    ///     When <paramref name="content"/> starts or ends with a backtick, a single space is
    ///     inserted on each side of the content inside the fence; CommonMark parsers strip exactly
    ///     one leading and one trailing space from a code span whose content both begins and ends
    ///     with a space and is not entirely spaces, preserving the intended text.
    /// </remarks>
    /// <param name="builder">The string builder to append to.</param>
    /// <param name="content">The non-empty, already-normalized code content.</param>
    private static void AppendMarkdownCodeSpan(StringBuilder builder, string content)
    {
        var fence = new string('`', ComputeFenceLength(content));

        // Pad with spaces when content starts or ends with a backtick so the fence
        // character is unambiguous to Markdown parsers (CommonMark §6.1)
        var needsPadding = content[0] == '`' || content[^1] == '`';

        builder.Append(fence);
        if (needsPadding)
        {
            builder.Append(' ');
        }

        builder.Append(content);

        if (needsPadding)
        {
            builder.Append(' ');
        }

        builder.Append(fence);
    }

    /// <summary>
    ///     Computes the minimum Markdown backtick fence length required to unambiguously delimit
    ///     <paramref name="content"/> — one greater than the longest consecutive run of backticks
    ///     found inside it (or <c>1</c> when <paramref name="content"/> contains no backticks).
    /// </summary>
    /// <param name="content">The code content the fence will delimit.</param>
    /// <returns>The number of backtick characters the fence must use.</returns>
    private static int ComputeFenceLength(string content)
    {
        var maxRun = 0;
        var currentRun = 0;
        foreach (var ch in content)
        {
            if (ch == '`')
            {
                currentRun++;
                if (currentRun > maxRun)
                {
                    maxRun = currentRun;
                }
            }
            else
            {
                currentRun = 0;
            }
        }

        return maxRun + 1;
    }

    /// <summary>
    ///     Returns the display text for a <c>&lt;see&gt;</c> or <c>&lt;seealso&gt;</c>
    ///     element, preferring the <c>langword</c> attribute, then explicit element text, then a
    ///     formatted <c>cref</c> attribute, and finally an empty string when none are present.
    /// </summary>
    /// <remarks>
    ///     When the display text is ultimately derived from a <c>cref</c> attribute that refers to
    ///     a type or type member (property, field, event, or method — see
    ///     <see cref="FormatCref"/>/<see cref="FormatMemberReference"/>), it is wrapped in an
    ///     inline Markdown code span via <see cref="FormatAsInlineCodeSpan"/> so it reads visually
    ///     distinct from surrounding prose, matching how <c>&lt;c&gt;</c> content is rendered. A
    ///     <c>langword</c> value, explicit inner text, and a constructor cref are all left
    ///     unwrapped.
    ///     <para>
    ///     When <paramref name="linkContext"/> is supplied and the <c>cref</c> resolves — via
    ///     <see cref="CrefTargetResolver.TryResolveType"/>/<see cref="CrefTargetResolver.TryResolveMember"/>
    ///     — to a symbol declared in the assembly being documented that will actually be emitted
    ///     (per <see cref="CrefLinkContext.IsTypeEmitted"/>/<see cref="CrefLinkContext.IsMemberEmitted"/>),
    ///     the code-span-wrapped text is itself wrapped in a real relative Markdown link (via
    ///     <see cref="TypeLinkResolver.LinkifyResolvedType"/>/<see cref="TypeLinkResolver.LinkifyResolvedMember"/>)
    ///     — the code span becomes the link's label (e.g. <c>[`Type.Member`](path.md)</c>), never
    ///     the other way around, since Markdown code span content is rendered literally and a link
    ///     nested inside one would show as inert <c>[...](...)</c> text rather than a clickable
    ///     link. In every other case — <paramref name="linkContext"/> is
    ///     <see langword="null"/>, the cref is external/malformed/unresolvable, or the resolved
    ///     symbol is filtered out of the generated documentation — rendering is completely
    ///     unchanged from the code-span-only fallback described above. A constructor cref (whose
    ///     <c>ShouldWrapInCodeSpan</c> result is always <see langword="false"/>) is never linked,
    ///     matching this existing precedent.
    ///     </para>
    /// </remarks>
    /// <param name="element">The inline reference element to render.</param>
    /// <param name="linkContext">
    ///     Optional context enabling cross-reference linking for a resolvable intra-assembly
    ///     <c>cref</c>. See <see cref="CrefLinkContext"/>.
    /// </param>
    /// <returns>A non-null display string; may be empty when no renderable content is found.</returns>
    private static string GetInlineReferenceText(XElement element, CrefLinkContext? linkContext = null)
    {
        var langword = element.Attribute("langword")?.Value;
        if (!string.IsNullOrWhiteSpace(langword))
        {
            return langword;
        }

        // If the element provides explicit display text, prefer it over formatting the cref,
        // but still attempt to resolve and link the cref using that explicit text as the link's
        // label — otherwise a valid in-assembly reference with a custom label (e.g.
        // <see cref="ArgumentValidator">the validator</see>) would never become a clickable
        // link. TryLinkifyCref returns the label unchanged when there is no link context or the
        // cref does not resolve, preserving the existing fallback exactly.
        var explicitText = NormalizeDocumentationText(element.Value);
        if (explicitText.Length > 0)
        {
            var explicitCref = element.Attribute("cref")?.Value;
            return string.IsNullOrWhiteSpace(explicitCref)
                ? explicitText
                : TryLinkifyCref(explicitCref, explicitText, linkContext);
        }

        var cref = element.Attribute("cref")?.Value;
        if (!string.IsNullOrWhiteSpace(cref))
        {
            // Request the code-span (unescaped) rendering since the result is about to be
            // wrapped in backticks, where escaped angle brackets would show literal backslashes
            // — see FormatCref's forCodeSpan parameter. Only the constructor-cref result ignores
            // this request and returns the escaped, prose-safe form internally, since it is the
            // only case never wrapped.
            var (text, shouldWrapInCodeSpan) = FormatCref(cref, forCodeSpan: true);
            if (!shouldWrapInCodeSpan || text.Length == 0)
            {
                return text;
            }

            // The code span must be the link's label, not the other way around — Markdown code
            // span content is literal, so a link nested inside a code span would render as inert
            // text showing the raw "[...](...)" syntax rather than a clickable link.
            var codeSpanText = FormatAsInlineCodeSpan(text);
            return TryLinkifyCref(cref, codeSpanText, linkContext);
        }

        return string.Empty;
    }

    /// <summary>
    ///     Attempts to resolve <paramref name="cref"/> against <paramref name="linkContext"/> and,
    ///     when resolution succeeds and the target will actually be emitted, returns
    ///     <paramref name="codeSpanText"/> wrapped in a real relative Markdown link, with the code
    ///     span as the link's label. Returns <paramref name="codeSpanText"/> unchanged in every
    ///     other case (no context, external type, filtered-out member, or malformed cref).
    /// </summary>
    /// <param name="cref">The raw <c>cref</c> attribute value, including its kind prefix.</param>
    /// <param name="codeSpanText">The already backtick-wrapped display text to use as the link label when linking.</param>
    /// <param name="linkContext">Optional cross-reference linking context.</param>
    /// <returns><paramref name="codeSpanText"/>, or that text wrapped in a Markdown link.</returns>
    private static string TryLinkifyCref(string cref, string codeSpanText, CrefLinkContext? linkContext)
    {
        if (linkContext == null)
        {
            return codeSpanText;
        }

        if (linkContext.Targets.TryResolveType(cref, out var type) && linkContext.IsTypeEmitted(type))
        {
            return linkContext.Resolver.LinkifyResolvedType(type, codeSpanText, linkContext.CurrentFolder);
        }

        if (linkContext.Targets.TryResolveMember(cref, out var member) && linkContext.IsMemberEmitted(member))
        {
            return linkContext.Resolver.LinkifyResolvedMember(member, codeSpanText, linkContext.CurrentFolder, linkContext.MemberPageIndex);
        }

        return codeSpanText;
    }

    /// <summary>
    ///     Wraps <paramref name="text"/> in an inline Markdown code span via
    ///     <see cref="AppendMarkdownCodeSpan"/>.
    /// </summary>
    /// <param name="text">The already-formatted, non-empty display text to wrap.</param>
    /// <returns>The text wrapped in backtick fences.</returns>
    private static string FormatAsInlineCodeSpan(string text)
    {
        var builder = new StringBuilder();
        AppendMarkdownCodeSpan(builder, text);
        return builder.ToString();
    }

    /// <summary>
    ///     Converts a raw XML-doc <c>cref</c> attribute value into a short, readable display string
    ///     by stripping the type-kind prefix and simplifying the qualified member name.
    /// </summary>
    /// <param name="cref">Raw cref string, e.g. <c>T:System.ArgumentNullException</c> or <c>M:Foo.Bar.Go(System.Int32)</c>.</param>
    /// <param name="forCodeSpan">
    ///     When <see langword="true"/>, requests the unescaped, code-span-safe rendering of any
    ///     generic angle-bracket notation (e.g. <c>List&lt;T&gt;</c> rather than the prose-escaped
    ///     <c>List\&lt;T\&gt;</c>) for results that will be wrapped in an inline Markdown code span
    ///     by the caller. This request only takes effect for branches whose <c>ShouldWrapInCodeSpan</c>
    ///     result is <see langword="true"/> — the constructor branch is never wrapped, so it always
    ///     returns the escaped, prose-safe form regardless of this flag.
    /// </param>
    /// <returns>
    ///     A concise display name (e.g. <c>ArgumentNullException</c> or <c>Bar.Go()</c>) together with
    ///     <see langword="true"/> unless the cref refers to a constructor — the single source of
    ///     truth <see cref="GetInlineReferenceText"/> uses to decide whether to wrap the text in an
    ///     inline code span. A type-only cref (e.g. <c>ArgumentValidator</c>) is wrapped the same as a
    ///     member reference, so an inline reference always reads as code regardless of whether it
    ///     targets a type or one of its members.
    /// </returns>
    private static (string Text, bool ShouldWrapInCodeSpan) FormatCref(string cref, bool forCodeSpan = false)
    {
        var separatorIndex = cref.IndexOf(':');
        var kind = separatorIndex > 0 ? cref[0] : '\0';
        var target = separatorIndex > 0 ? cref[(separatorIndex + 1)..] : cref;

        var parameterIndex = target.IndexOf('(');
        var parameters = parameterIndex >= 0 ? target[parameterIndex..] : string.Empty;
        var memberTarget = parameterIndex >= 0 ? target[..parameterIndex] : target;

        return kind switch
        {
            'T' => (FormatTypeName(memberTarget, forCodeSpan), true),
            'M' or 'P' or 'F' or 'E' => FormatMemberReference(kind, memberTarget, parameters, forCodeSpan),
            _ => (target, false),
        };
    }

    /// <summary>
    ///     Formats a fully-qualified member reference (method, property, field, or event) into a
    ///     concise display string by splitting on the last dot separator and simplifying the
    ///     type and member names. Constructors (<c>#ctor</c>) are collapsed to the type name only.
    /// </summary>
    /// <param name="kind">The member kind character from the cref prefix: <c>M</c>, <c>P</c>, <c>F</c>, or <c>E</c>.</param>
    /// <param name="target">Fully-qualified member path without the kind prefix or parameter list.</param>
    /// <param name="parameters">The raw parameter list substring (e.g. <c>(System.Int32)</c>), or empty.</param>
    /// <param name="forCodeSpan">
    ///     See <see cref="FormatCref"/>. Applied only to the member-reference result (the result
    ///     returned with <see langword="true"/>) — the constructor branch below always returns
    ///     the escaped, prose-safe form since it is never wrapped in a code span.
    /// </param>
    /// <returns>
    ///     A concise display string suitable for inline documentation text, together with
    ///     <see langword="true"/> unless <paramref name="kind"/> is <c>M</c> and
    ///     <paramref name="target"/> names a constructor (<c>#ctor</c>), in which case the type name
    ///     alone is returned with <see langword="false"/>.
    /// </returns>
    private static (string Text, bool ShouldWrapInCodeSpan) FormatMemberReference(
        char kind,
        string target,
        string parameters,
        bool forCodeSpan = false)
    {
        var lastDot = target.LastIndexOf('.');
        if (lastDot < 0)
        {
            return (target, true);
        }

        var typeName = target[..lastDot];
        var memberName = target[(lastDot + 1)..];

        if (kind == 'M' && memberName == "#ctor")
        {
            // Never wrapped in a code span (ShouldWrapInCodeSpan is false here) — always escaped.
            return (FormatTypeName(typeName, forCodeSpan: false), false);
        }

        // The declaring type name is always included for M/P/F/E member references — a bare
        // member name (e.g. "Go" instead of "Bar.Go") is ambiguous to a reader who does not
        // already know which type declares the referenced member.
        var formattedTypeName = FormatTypeName(typeName, forCodeSpan);
        var formattedMemberName = StripArity(memberName);
        var memberDisplay = $"{formattedTypeName}.{formattedMemberName}";

        return kind == 'M' && parameters.Length > 0
            ? ($"{memberDisplay}()", true)
            : (memberDisplay, true);
    }

    /// <summary>
    ///     Returns a short C# display name for a fully-qualified type name, applying primitive
    ///     aliases for well-known <c>System.*</c> types and stripping the namespace from all others.
    /// </summary>
    /// <param name="typeName">Fully-qualified CLR type name, e.g. <c>System.Int32</c> or <c>My.Namespace.Foo</c>.</param>
    /// <param name="forCodeSpan">
    ///     See <see cref="FormatCref"/>; forwarded to <see cref="FormatTypeArity"/> to control
    ///     whether generic angle-bracket notation is escaped for prose or left raw for a code span.
    /// </param>
    /// <returns>A C# keyword alias (e.g. <c>int</c>) or the unqualified simple name (e.g. <c>Foo</c>).</returns>
    private static string FormatTypeName(string typeName, bool forCodeSpan = false)
    {
        return typeName switch
        {
            "System.Boolean" => "bool",
            "System.Byte" => "byte",
            "System.Char" => "char",
            "System.Decimal" => "decimal",
            "System.Double" => "double",
            "System.Int16" => "short",
            "System.Int32" => "int",
            "System.Int64" => "long",
            "System.Object" => "object",
            "System.SByte" => "sbyte",
            "System.Single" => "float",
            "System.String" => "string",
            "System.UInt16" => "ushort",
            "System.UInt32" => "uint",
            "System.UInt64" => "ulong",
            "System.Void" => "void",
            _ => FormatTypeArity(typeName[(typeName.LastIndexOf('.') + 1)..], forCodeSpan),
        };
    }

    /// <summary>
    ///     Removes the generic arity backtick suffix from a member name, e.g. converting
    ///     <c>GetValue`1</c> to <c>GetValue</c>. Returns the original string unchanged when
    ///     no backtick is present. Use this for member name stripping only — for type display
    ///     names, use <see cref="FormatTypeArity"/> instead to produce angle-bracket notation.
    /// </summary>
    /// <param name="typeName">Raw member name that may contain a backtick arity suffix.</param>
    /// <returns>The name without the backtick and trailing digit(s).</returns>
    private static string StripArity(string typeName)
    {
        var tickIndex = typeName.IndexOf('`');
        return tickIndex >= 0 ? typeName[..tickIndex] : typeName;
    }

    /// <summary>
    ///     Converts a raw CLR type name with a generic arity backtick suffix into a
    ///     C# display form with angle-bracket type-parameter placeholders.
    ///     For example, <c>List`1</c> → <c>List&lt;T&gt;</c> and
    ///     <c>Dictionary`2</c> → <c>Dictionary&lt;T1, T2&gt;</c>.
    ///     Returns the original string unchanged when no backtick is present.
    /// </summary>
    /// <param name="typeName">Raw type name that may contain a backtick arity suffix.</param>
    /// <param name="forCodeSpan">
    ///     When <see langword="true"/>, returns raw (unescaped) angle brackets, suitable for
    ///     content that will be wrapped in an inline Markdown code span — where backslash escapes
    ///     are literal characters, not Markdown escapes, so an escaped result would display stray
    ///     backslashes (e.g. <c>List\&lt;T\&gt;</c> instead of <c>List&lt;T&gt;</c>). When
    ///     <see langword="false"/> (the default), angle brackets are escaped for bare Markdown
    ///     prose, where an unescaped <c>&lt;T&gt;</c> would otherwise be parsed as an HTML tag and
    ///     stripped or hidden by Markdown renderers.
    /// </param>
    /// <returns>The name with angle-bracket placeholder notation, or the original name if no arity marker is present.</returns>
    private static string FormatTypeArity(string typeName, bool forCodeSpan = false)
    {
        var tickIndex = typeName.IndexOf('`');
        if (tickIndex < 0)
        {
            return typeName;
        }

        var baseName = typeName[..tickIndex];
        if (!int.TryParse(typeName[(tickIndex + 1)..], out var arity) || arity <= 0)
        {
            return baseName;
        }

        var typeParams = arity == 1
            ? "T"
            : string.Join(", ", Enumerable.Range(1, arity).Select(i => $"T{i}"));

        return forCodeSpan
            ? $"{baseName}<{typeParams}>"
            : $"{baseName}\\<{typeParams}\\>";
    }

    /// <summary>
    ///     Normalizes raw documentation text by collapsing runs of internal whitespace on each
    ///     line, trimming every line, collapsing runs of two or more consecutive blank lines down
    ///     to a single blank line, and preserving non-empty lines separated by newlines.
    /// </summary>
    /// <remarks>
    ///     The blank-line-run collapsing step guards against markdownlint MD012 violations that
    ///     would otherwise appear where adjacent block-level separators (a <c>&lt;list&gt;</c>, a
    ///     <c>&lt;br/&gt;</c>, or a fenced <c>&lt;code&gt;</c> block) each independently surround
    ///     themselves with a blank line — e.g. a list immediately followed by a fenced code block
    ///     would otherwise leave two blank lines between them. It runs after per-line collapsing
    ///     and trimming, and before the final <see cref="string.Trim()"/>, so a run of blank lines
    ///     at either boundary is first reduced to a single blank line and then removed entirely by
    ///     the trailing trim.
    /// </remarks>
    /// <param name="text">Raw text extracted from an XML documentation element.</param>
    /// <returns>Normalized multi-line text with no leading/trailing whitespace.</returns>
    private static string NormalizeDocumentationText(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(CollapseWhitespace)
            .Select(line => line.Trim());

        var collapsedLines = new List<string>();
        var previousWasBlank = false;
        foreach (var line in lines)
        {
            var isBlank = line.Length == 0;
            if (isBlank && previousWasBlank)
            {
                continue;
            }

            collapsedLines.Add(line);
            previousWasBlank = isBlank;
        }

        return string.Join("\n", collapsedLines).Trim();
    }

    /// <summary>
    ///     Joins all non-empty trimmed lines from <paramref name="text"/> into a single
    ///     space-separated string, collapsing all internal whitespace within each line.
    /// </summary>
    /// <param name="text">Raw text that may contain multiple lines and leading/trailing whitespace.</param>
    /// <returns>A single-line string with redundant whitespace removed.</returns>
    private static string NormalizeSingleLine(string text)
    {
        return string.Join(
            " ",
            text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .Select(CollapseWhitespace)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0));
    }

    /// <summary>
    ///     Joins all non-empty trimmed lines from <paramref name="text"/> into a single
    ///     space-separated string, WITHOUT collapsing each line's internal whitespace runs.
    /// </summary>
    /// <remarks>
    ///     Used to flatten a multi-line <c>&lt;code&gt;</c> element into a single-line inline code
    ///     span (see <see cref="AppendCodeElementText"/>): unlike <see cref="NormalizeSingleLine"/>,
    ///     which is used for surrounding prose and deliberately collapses redundant whitespace, code
    ///     content may have significant internal spacing (alignment, multiple spaces, tabs) that
    ///     must survive being joined onto one line. Only leading/trailing whitespace on each line is
    ///     trimmed, and blank lines are dropped; everything else on a non-blank line is preserved
    ///     verbatim.
    /// </remarks>
    /// <param name="text">Raw multi-line code text (already dedented).</param>
    /// <returns>A single-line string with line boundaries collapsed to single spaces.</returns>
    private static string FlattenCodeToSingleLine(string text)
    {
        return string.Join(
            " ",
            text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0));
    }

    /// <summary>
    ///     Collapses consecutive whitespace characters in <paramref name="text"/> to a single space,
    ///     preserving non-whitespace characters unchanged.
    /// </summary>
    /// <param name="text">The input text in which to collapse whitespace runs.</param>
    /// <returns>A string where every run of whitespace is replaced by a single space character.</returns>
    private static string CollapseWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var previousWasWhitespace = false;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString();
    }

    /// <summary>
    ///     Removes common leading indentation from raw code text extracted from an XML doc
    ///     <c>&lt;code&gt;</c> element so the result renders flush-left in a fenced Markdown code block.
    /// </summary>
    /// <remarks>
    ///     The minimum indentation is computed from the leading whitespace of every non-blank line
    ///     (blank lines — lines that are entirely whitespace — are excluded from the calculation so
    ///     they do not artificially reduce the common prefix). That prefix is then stripped from every
    ///     line. Leading and trailing blank lines are removed from the final result.
    ///     Returns <see cref="string.Empty"/> for whitespace-only input so existing
    ///     <c>string.IsNullOrEmpty</c> guards remain effective.
    /// </remarks>
    /// <param name="text">Raw text content of a <c>&lt;code&gt;</c> element.</param>
    /// <returns>Dedented code text with no leading or trailing blank lines, or <see cref="string.Empty"/>.</returns>
    private static string DedentCode(string text)
    {
        // Normalize line endings to \n before splitting to handle \r\n and \r sources uniformly
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
                        .Replace('\r', '\n')
                        .Split('\n');

        // Compute minimum leading-whitespace count from non-blank lines only; blank lines must
        // not artificially reduce the common indent and will be preserved as empty in the output
        var minIndent = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.TakeWhile(c => c == ' ' || c == '\t').Count())
            .DefaultIfEmpty(0)
            .Min();

        // Strip the common prefix from every line; lines shorter than minIndent become empty.
        // Normalize any remaining whitespace-only lines to string.Empty so lines that originally
        // carried only indentation (more than minIndent spaces) do not introduce trailing spaces
        // into the fenced code block output.
        var dedented = lines
            .Select(line => line.Length >= minIndent ? line[minIndent..] : string.Empty)
            .Select(line => string.IsNullOrWhiteSpace(line) ? string.Empty : line);

        // Remove leading and trailing blank lines so the output begins and ends with content
        var trimmed = dedented
            .SkipWhile(string.IsNullOrWhiteSpace)
            .Reverse()
            .SkipWhile(string.IsNullOrWhiteSpace)
            .Reverse()
            .ToList();

        return string.Join("\n", trimmed);
    }
}
