using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;

namespace ApiMark.DotNet;

/// <summary>Reads and indexes a .NET XML documentation file for fast member-level lookups.</summary>
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

    /// <summary>Initializes a new instance of <see cref="XmlDocReader"/> from the given path.</summary>
    /// <remarks>
    ///     When duplicate member names appear in the XML doc file, the first occurrence is used
    ///     and subsequent duplicates are silently discarded. This is a defensive policy for
    ///     malformed but real-world XML doc files where the compiler emits the same member ID
    ///     more than once (e.g., due to partial-class splits or tooling bugs).
    /// </remarks>
    /// <param name="xmlDocPath">Path to the XML documentation file.</param>
    /// <param name="inheritanceChain">
    ///     Optional map of member ID to ordered list of base member IDs. Used to resolve
    ///     bare <c>&lt;inheritdoc /&gt;</c> elements that carry no <c>cref</c> attribute.
    ///     When <c>null</c>, bare inheritdoc resolution returns <c>null</c> or empty.
    /// </param>
    /// <exception cref="FileNotFoundException">Thrown when <paramref name="xmlDocPath"/> does not exist.</exception>
    public XmlDocReader(string xmlDocPath, IReadOnlyDictionary<string, IReadOnlyList<string>>? inheritanceChain = null)
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
    }

    /// <summary>Returns the trimmed summary text for <paramref name="memberId"/>, or <c>null</c> if absent.</summary>
    /// <remarks>
    ///     Summary text is always normalized to a single line because summaries are by convention
    ///     brief one-liner descriptions. Multi-paragraph content belongs in <c>&lt;remarks&gt;</c>.
    ///     Use <see cref="GetRemarks"/> to retrieve multi-line content.
    ///     When the member carries an <c>&lt;inheritdoc /&gt;</c> element, the summary is
    ///     resolved from the referenced or inherited base member recursively.
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier (e.g. <c>T:MyNamespace.MyClass</c>).</param>
    /// <returns>Single-line trimmed summary text, or <c>null</c>.</returns>
    public string? GetSummary(string memberId)
    {
        var member = ResolveMemberElement(memberId, new HashSet<string>(StringComparer.Ordinal));
        if (member == null)
        {
            return null;
        }

        // Use single-line normalization — summaries must fit on one line by convention
        return GetSingleLineDocumentationText(member.Element("summary"));
    }

    /// <summary>Returns the trimmed remarks text for <paramref name="memberId"/>, or <c>null</c> if absent.</summary>
    /// <remarks>
    ///     When the member carries an <c>&lt;inheritdoc /&gt;</c> element, the remarks are
    ///     resolved from the referenced or inherited base member recursively.
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>Trimmed remarks text, or <c>null</c>.</returns>
    public string? GetRemarks(string memberId)
    {
        var member = ResolveMemberElement(memberId, new HashSet<string>(StringComparer.Ordinal));
        if (member == null)
        {
            return null;
        }

        return GetDocumentationText(member.Element("remarks"));
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
    ///     resolved from the referenced or inherited base member recursively.
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
                var type = string.IsNullOrWhiteSpace(cref) ? string.Empty : FormatCref(cref);
                var description = GetDocumentationText(e);
                return (type, description);
            })
            .Where(e => e.Type.Length > 0)
            .ToList();
    }

    /// <summary>Returns parameter names and descriptions for <paramref name="memberId"/>.</summary>
    /// <remarks>
    ///     When the member carries an <c>&lt;inheritdoc /&gt;</c> element, parameters are
    ///     resolved from the referenced or inherited base member recursively.
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>A read-only list of (Name, Description) tuples.</returns>
    public IReadOnlyList<(string Name, string? Description)> GetParams(string memberId)
    {
        var member = ResolveMemberElement(memberId, new HashSet<string>(StringComparer.Ordinal));
        if (member == null)
        {
            return Array.Empty<(string, string?)>();
        }

        return member.Elements("param")
            .Select<XElement, (string Name, string? Description)>(p => (
                p.Attribute("name")?.Value ?? string.Empty,
                GetDocumentationText(p)))
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
    public string? GetReturns(string memberId)
    {
        var member = ResolveMemberElement(memberId, new HashSet<string>(StringComparer.Ordinal));
        if (member == null)
        {
            return null;
        }

        return GetDocumentationText(member.Element("returns"));
    }

    /// <summary>Returns the trimmed example text for <paramref name="memberId"/>, or <c>null</c> if absent.</summary>
    /// <remarks>
    ///     Returns <c>null</c> when the <c>&lt;example&gt;</c> element is absent or contains only
    ///     whitespace, matching the null-for-missing contract of <see cref="GetSummary"/>,
    ///     <see cref="GetRemarks"/>, and <see cref="GetReturns"/>.
    ///     When the member carries an <c>&lt;inheritdoc /&gt;</c> element, the example is
    ///     resolved from the referenced or inherited base member recursively.
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
    public IReadOnlyList<(bool IsCode, string Content)> GetExampleParts(string memberId)
    {
        var member = ResolveMemberElement(memberId, new HashSet<string>(StringComparer.Ordinal));
        if (member == null)
        {
            return Array.Empty<(bool, string)>();
        }

        var el = member.Element("example");
        if (el == null)
        {
            return Array.Empty<(bool, string)>();
        }

        // When no <code> children exist, treat the entire value as a single code block
        if (!el.Elements("code").Any())
        {
            var text = el.Value.Trim();
            return string.IsNullOrEmpty(text)
                ? Array.Empty<(bool, string)>()
                : [(true, text)];
        }

        // Mixed content: process child nodes in order, separating <code> blocks from prose.
        // Consecutive non-<code> nodes (text nodes and inline elements such as <see>, <c>,
        // <paramref>) are accumulated into a shared StringBuilder and flushed as a single
        // prose part when a <code> block or the end of the sequence is reached. This ensures
        // inline references within a prose run are rendered coherently via AppendNodeText /
        // AppendElementText rather than being emitted as isolated, broken fragments.
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

                var code = codeElement.Value.Trim();
                if (code.Length > 0)
                {
                    parts.Add((true, code));
                }
            }
            else
            {
                // Text nodes and inline elements — accumulate for combined prose rendering
                AppendNodeText(proseBuilder, [node]);
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
    ///         <item>If the member is absent from the index, return <c>null</c>.</item>
    ///         <item>If the member has no <c>&lt;inheritdoc /&gt;</c> child, return the member element directly.</item>
    ///         <item>If a <c>cref</c> attribute is present, resolve the cref target recursively.</item>
    ///         <item>Otherwise, try each candidate in the injected inheritance chain in priority order.</item>
    ///         <item>When a <c>path</c> attribute is present, apply it as an XPath expression to the resolved
    ///               source element and return the matches wrapped in a synthetic <c>&lt;member&gt;</c> element.</item>
    ///     </list>
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier to resolve.</param>
    /// <param name="visited">Set of member IDs visited on the current resolution path; updated in-place.</param>
    /// <returns>
    ///     The resolved member element (possibly synthetic when a path filter is applied), or
    ///     <c>null</c> when the member is absent, a cycle is detected, or no valid target is found.
    /// </returns>
    private XElement? ResolveMemberElement(string memberId, HashSet<string> visited)
    {
        // Cycle detection: stop if this ID has already been visited on the current resolution path
        if (!visited.Add(memberId))
        {
            return null;
        }

        if (!_members.TryGetValue(memberId, out var member))
        {
            return null;
        }

        var inheritdoc = member.Element("inheritdoc");
        if (inheritdoc == null)
        {
            // No inheritdoc — return the member element directly
            return member;
        }

        var cref = inheritdoc.Attribute("cref")?.Value;
        if (string.IsNullOrWhiteSpace(cref))
        {
            cref = null;
        }
        var path = inheritdoc.Attribute("path")?.Value;

        // Determine the source member: explicit cref takes priority over bare chain lookup
        XElement? source = null;
        if (cref != null)
        {
            // Explicit cref target — recurse in case the target itself also inherits
            source = ResolveMemberElement(cref, visited);
        }
        else if (_inheritanceChain != null && _inheritanceChain.TryGetValue(memberId, out var targets))
        {
            // Bare inheritdoc — try each candidate in priority order, stop at first hit.
            // Use a branch-local copy of visited for each candidate so that failed
            // traversals in one branch do not poison the visited set seen by sibling
            // candidates in the same priority list.
            foreach (var target in targets)
            {
                var branchVisited = new HashSet<string>(visited, StringComparer.Ordinal);
                source = ResolveMemberElement(target, branchVisited);
                if (source != null)
                {
                    break;
                }
            }
        }

        if (source == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            // Absent or whitespace path — treat as no filter and return the full resolved source
            return source;
        }

        // Apply the XPath path filter to the resolved source element and wrap matching nodes
        // in a synthetic <member> element so callers can use the same child-extraction logic
        // regardless of whether a path filter was applied.
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
    /// <param name="element">The XML element whose text content to extract, or <c>null</c>.</param>
    /// <returns>Normalized text, or <c>null</c> when the element is absent or empty.</returns>
    private static string? GetDocumentationText(XElement? element)
    {
        if (element == null)
        {
            return null;
        }

        var builder = new StringBuilder();
        AppendNodeText(builder, element.Nodes());
        var text = NormalizeDocumentationText(builder.ToString());
        return text.Length == 0 ? null : text;
    }

    /// <summary>
    ///     Builds documentation text from <paramref name="element"/> and normalizes it to a
    ///     single line by collapsing all non-empty trimmed lines into one space-separated string.
    /// </summary>
    /// <param name="element">The XML element whose text content to extract, or <c>null</c>.</param>
    /// <returns>Single-line trimmed text, or <c>null</c> when the element is absent or empty.</returns>
    private static string? GetSingleLineDocumentationText(XElement? element)
    {
        if (element == null)
        {
            return null;
        }

        var builder = new StringBuilder();
        AppendNodeText(builder, element.Nodes());
        var text = NormalizeSingleLine(builder.ToString());
        return text.Length == 0 ? null : text;
    }

    /// <summary>
    ///     Iterates <paramref name="nodes"/> and appends each node's text representation
    ///     to <paramref name="builder"/>, dispatching XML elements to
    ///     <see cref="AppendElementText"/>.
    /// </summary>
    /// <param name="builder">The string builder that accumulates the output text.</param>
    /// <param name="nodes">The sequence of XML nodes to process.</param>
    private static void AppendNodeText(StringBuilder builder, IEnumerable<XNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case XText text:
                    builder.Append(text.Value);
                    break;
                case XElement element:
                    AppendElementText(builder, element);
                    break;
            }
        }
    }

    /// <summary>
    ///     Appends the text representation of a single XML element to <paramref name="builder"/>,
    ///     applying element-specific rendering rules for inline references, parameter references,
    ///     inline code, paragraphs, and generic XML elements.
    /// </summary>
    /// <param name="builder">The string builder that accumulates the output text.</param>
    /// <param name="element">The XML element to render.</param>
    private static void AppendElementText(StringBuilder builder, XElement element)
    {
        switch (element.Name.LocalName)
        {
            case "see":
            case "seealso":
                builder.Append(GetInlineReferenceText(element));
                break;
            case "paramref":
            case "typeparamref":
                builder.Append(element.Attribute("name")?.Value ?? string.Empty);
                break;
            case "para":
                AppendNodeText(builder, element.Nodes());
                builder.AppendLine();
                break;
            case "c":
                // Render inline code in backticks so markdown consumers display it as
                // monospace text — matches the intent of <c> in XML documentation
                builder.Append('`');
                builder.Append(element.Value);
                builder.Append('`');
                break;
            default:
                AppendNodeText(builder, element.Nodes());
                break;
        }
    }

    /// <summary>
    ///     Returns the display text for a <c>&lt;see&gt;</c> or <c>&lt;seealso&gt;</c> inline reference
    ///     element, preferring the <c>langword</c> attribute, then explicit element text, then a
    ///     formatted <c>cref</c> attribute, and finally an empty string when none are present.
    /// </summary>
    /// <param name="element">The inline reference element to render.</param>
    /// <returns>A non-null display string; may be empty when no renderable content is found.</returns>
    private static string GetInlineReferenceText(XElement element)
    {
        var langword = element.Attribute("langword")?.Value;
        if (!string.IsNullOrWhiteSpace(langword))
        {
            return langword;
        }

        // If the element provides explicit display text, prefer it over formatting the cref.
        var explicitText = NormalizeDocumentationText(element.Value);
        if (explicitText.Length > 0)
        {
            return explicitText;
        }

        var cref = element.Attribute("cref")?.Value;
        if (!string.IsNullOrWhiteSpace(cref))
        {
            return FormatCref(cref);
        }

        return string.Empty;
    }

    /// <summary>
    ///     Converts a raw XML-doc <c>cref</c> attribute value into a short, readable display string
    ///     by stripping the type-kind prefix and simplifying the qualified member name.
    /// </summary>
    /// <param name="cref">Raw cref string, e.g. <c>T:System.ArgumentNullException</c> or <c>M:Foo.Bar.Go(System.Int32)</c>.</param>
    /// <returns>A concise display name, e.g. <c>ArgumentNullException</c> or <c>Bar.Go()</c>.</returns>
    private static string FormatCref(string cref)
    {
        var separatorIndex = cref.IndexOf(':');
        var kind = separatorIndex > 0 ? cref[0] : '\0';
        var target = separatorIndex > 0 ? cref[(separatorIndex + 1)..] : cref;

        var parameterIndex = target.IndexOf('(');
        var parameters = parameterIndex >= 0 ? target[parameterIndex..] : string.Empty;
        var memberTarget = parameterIndex >= 0 ? target[..parameterIndex] : target;

        return kind switch
        {
            'T' => FormatTypeName(memberTarget),
            'M' or 'P' or 'F' or 'E' => FormatMemberReference(kind, memberTarget, parameters),
            _ => target,
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
    /// <returns>A concise display string suitable for inline documentation text.</returns>
    private static string FormatMemberReference(char kind, string target, string parameters)
    {
        var lastDot = target.LastIndexOf('.');
        if (lastDot < 0)
        {
            return target;
        }

        var typeName = target[..lastDot];
        var memberName = target[(lastDot + 1)..];

        if (kind == 'M' && memberName == "#ctor")
        {
            return FormatTypeName(typeName);
        }

        var formattedTypeName = FormatTypeName(typeName);
        var formattedMemberName = StripArity(memberName);
        var shouldIncludeTypeName = kind == 'M' || IsPrimitiveTypeName(typeName);
        var memberDisplay = shouldIncludeTypeName
            ? $"{formattedTypeName}.{formattedMemberName}"
            : formattedMemberName;

        return kind == 'M' && parameters.Length > 0
            ? $"{memberDisplay}()"
            : memberDisplay;
    }

    /// <summary>
    ///     Returns a short C# display name for a fully-qualified type name, applying primitive
    ///     aliases for well-known <c>System.*</c> types and stripping the namespace from all others.
    /// </summary>
    /// <param name="typeName">Fully-qualified CLR type name, e.g. <c>System.Int32</c> or <c>My.Namespace.Foo</c>.</param>
    /// <returns>A C# keyword alias (e.g. <c>int</c>) or the unqualified simple name (e.g. <c>Foo</c>).</returns>
    private static string FormatTypeName(string typeName)
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
            _ => StripArity(typeName[(typeName.LastIndexOf('.') + 1)..]),
        };
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="typeName"/> is a <c>System.*</c> type name.</summary>
    /// <remarks>
    ///     Used to decide whether to include the declaring type name in a formatted member reference.
    ///     Any <c>System.*</c> type qualifies, not only the language primitives mapped by
    ///     <see cref="FormatTypeName"/>.
    /// </remarks>
    /// <param name="typeName">Fully-qualified type name to classify.</param>
    /// <returns><see langword="true"/> when the name starts with <c>System.</c>; <see langword="false"/> otherwise.</returns>
    private static bool IsPrimitiveTypeName(string typeName) => typeName.StartsWith("System.", StringComparison.Ordinal);

    /// <summary>
    ///     Removes the generic arity backtick suffix from a type name, e.g. converting <c>List`1</c>
    ///     to <c>List</c>. Returns the original string unchanged when no backtick is present.
    /// </summary>
    /// <param name="typeName">Raw type name that may contain a backtick arity suffix.</param>
    /// <returns>The name without the backtick and trailing digit(s).</returns>
    private static string StripArity(string typeName)
    {
        var tickIndex = typeName.IndexOf('`');
        return tickIndex >= 0 ? typeName[..tickIndex] : typeName;
    }

    /// <summary>
    ///     Normalizes raw documentation text by collapsing runs of internal whitespace on each
    ///     line, trimming every line, and preserving non-empty lines separated by newlines.
    /// </summary>
    /// <param name="text">Raw text extracted from an XML documentation element.</param>
    /// <returns>Normalized multi-line text with no leading/trailing whitespace.</returns>
    private static string NormalizeDocumentationText(string text)
    {
        return string.Join(
                "\n",
                text.Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace('\r', '\n')
                    .Split('\n')
                    .Select(CollapseWhitespace)
                    .Select(line => line.Trim()))
            .Trim();
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
}
