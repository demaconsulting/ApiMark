using System.Text;
using System.Xml.Linq;

namespace ApiMark.DotNet;

/// <summary>Reads and indexes a .NET XML documentation file for fast member-level lookups.</summary>
public sealed class XmlDocReader
{
    /// <summary>Index of documentation members keyed by their XML doc identifier.</summary>
    private readonly Dictionary<string, XElement> _members;

    /// <summary>Initializes a new instance of <see cref="XmlDocReader"/> from the given path.</summary>
    /// <remarks>
    ///     When duplicate member names appear in the XML doc file, the first occurrence is used
    ///     and subsequent duplicates are silently discarded. This is a defensive policy for
    ///     malformed but real-world XML doc files where the compiler emits the same member ID
    ///     more than once (e.g., due to partial-class splits or tooling bugs).
    /// </remarks>
    /// <param name="xmlDocPath">Path to the XML documentation file.</param>
    /// <exception cref="FileNotFoundException">Thrown when <paramref name="xmlDocPath"/> does not exist.</exception>
    public XmlDocReader(string xmlDocPath)
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
    }

    /// <summary>Returns the trimmed summary text for <paramref name="memberId"/>, or <c>null</c> if absent.</summary>
    /// <remarks>
    ///     Summary text is always normalized to a single line because summaries are by convention
    ///     brief one-liner descriptions. Multi-paragraph content belongs in <c>&lt;remarks&gt;</c>.
    ///     Use <see cref="GetRemarks"/> to retrieve multi-line content.
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier (e.g. <c>T:MyNamespace.MyClass</c>).</param>
    /// <returns>Single-line trimmed summary text, or <c>null</c>.</returns>
    public string? GetSummary(string memberId)
    {
        if (!_members.TryGetValue(memberId, out var member))
        {
            return null;
        }

        // Use single-line normalization — summaries must fit on one line by convention
        return GetSingleLineDocumentationText(member.Element("summary"));
    }

    /// <summary>Returns the trimmed remarks text for <paramref name="memberId"/>, or <c>null</c> if absent.</summary>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>Trimmed remarks text, or <c>null</c>.</returns>
    public string? GetRemarks(string memberId)
    {
        if (!_members.TryGetValue(memberId, out var member))
        {
            return null;
        }

        return GetDocumentationText(member.Element("remarks"));
    }

    /// <summary>Returns all <c>cref</c> attribute values from <c>&lt;exception&gt;</c> elements for <paramref name="memberId"/>.</summary>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>A read-only list of exception cref strings.</returns>
    public IReadOnlyList<string> GetExceptions(string memberId)
    {
        if (!_members.TryGetValue(memberId, out var member))
        {
            return Array.Empty<string>();
        }

        return member.Elements("exception")
            .Select(e => e.Attribute("cref")?.Value ?? string.Empty)
            .Where(v => v.Length > 0)
            .ToList();
    }

    /// <summary>Returns exception types and descriptions from <c>&lt;exception&gt;</c> elements for <paramref name="memberId"/>.</summary>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>A read-only list of (Type, Description) tuples.</returns>
    public IReadOnlyList<(string Type, string? Description)> GetExceptionDetails(string memberId)
    {
        if (!_members.TryGetValue(memberId, out var member))
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
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>A read-only list of (Name, Description) tuples.</returns>
    public IReadOnlyList<(string Name, string? Description)> GetParams(string memberId)
    {
        if (!_members.TryGetValue(memberId, out var member))
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
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>Trimmed returns text, or <c>null</c>.</returns>
    public string? GetReturns(string memberId)
    {
        if (!_members.TryGetValue(memberId, out var member))
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
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>Trimmed example text, or <c>null</c> when the element is absent or whitespace-only.</returns>
    public string? GetExample(string memberId)
    {
        if (!_members.TryGetValue(memberId, out var member))
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
    ///     present, text nodes become prose parts and <c>&lt;code&gt;</c> elements become code parts.
    /// </remarks>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>
    ///     A list of (IsCode, Content) pairs, or an empty list when the member is absent or has
    ///     no <c>&lt;example&gt;</c> element.
    /// </returns>
    public IReadOnlyList<(bool IsCode, string Content)> GetExampleParts(string memberId)
    {
        if (!_members.TryGetValue(memberId, out var member))
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

        // Mixed content: process child nodes in order, separating <code> blocks from prose
        var parts = new List<(bool IsCode, string Content)>();
        foreach (var node in el.Nodes())
        {
            switch (node)
            {
                case XText textNode:
                    {
                        var text = textNode.Value.Trim();
                        if (text.Length > 0)
                        {
                            parts.Add((false, text));
                        }

                        break;
                    }

                case XElement childElement when childElement.Name.LocalName == "code":
                    {
                        var code = childElement.Value.Trim();
                        if (code.Length > 0)
                        {
                            parts.Add((true, code));
                        }

                        break;
                    }

                case XElement otherElement:
                    {
                        var text = otherElement.Value.Trim();
                        if (text.Length > 0)
                        {
                            parts.Add((false, text));
                        }

                        break;
                    }
            }
        }

        return parts;
    }

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
            default:
                AppendNodeText(builder, element.Nodes());
                break;
        }
    }

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

    private static bool IsPrimitiveTypeName(string typeName) => typeName.StartsWith("System.", StringComparison.Ordinal);

    private static string StripArity(string typeName)
    {
        var tickIndex = typeName.IndexOf('`');
        return tickIndex >= 0 ? typeName[..tickIndex] : typeName;
    }

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
