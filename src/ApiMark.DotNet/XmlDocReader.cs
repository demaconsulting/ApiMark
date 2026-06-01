using System.Text;
using System.Xml.Linq;

namespace ApiMark.DotNet;

/// <summary>Reads and indexes a .NET XML documentation file for fast member-level lookups.</summary>
public sealed class XmlDocReader
{
    /// <summary>Index of documentation members keyed by their XML doc identifier.</summary>
    private readonly Dictionary<string, XElement> _members;

    /// <summary>Initializes a new instance of <see cref="XmlDocReader"/> from the given path.</summary>
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
        // individual lookups are O(1) rather than scanning all descendants each call
        var doc = XDocument.Load(xmlDocPath);
        _members = doc.Descendants("member")
            .Where(m => m.Attribute("name") != null)
            .ToDictionary(m => m.Attribute("name")!.Value, m => m);
    }

    /// <summary>Returns the trimmed summary text for <paramref name="memberId"/>, or <c>null</c> if absent.</summary>
    /// <param name="memberId">The XML doc member identifier (e.g. <c>T:MyNamespace.MyClass</c>).</param>
    /// <returns>Trimmed summary text, or <c>null</c>.</returns>
    public string? GetSummary(string memberId)
    {
        if (!_members.TryGetValue(memberId, out var member))
        {
            return null;
        }

        return GetDocumentationText(member.Element("summary"));
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

    /// <summary>Returns <c>true</c> if the remarks for <paramref name="memberId"/> span more than one non-empty line.</summary>
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns><c>true</c> if remarks has more than one non-empty line.</returns>
    public bool IsMultiLineRemarks(string memberId)
    {
        var remarks = GetRemarks(memberId);
        if (remarks == null)
        {
            return false;
        }

        // Count non-empty lines after trimming each; single-line remarks do not
        // warrant a dedicated member page under the complexity rule
        var lines = remarks.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
        return lines.Count > 1;
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
    /// <param name="memberId">The XML doc member identifier.</param>
    /// <returns>Trimmed example text, or <c>null</c>.</returns>
    public string? GetExample(string memberId)
    {
        if (!_members.TryGetValue(memberId, out var member))
        {
            return null;
        }

        var el = member.Element("example");
        return el?.Value.Trim();
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

        var cref = element.Attribute("cref")?.Value;
        if (!string.IsNullOrWhiteSpace(cref))
        {
            return FormatCref(cref);
        }

        return NormalizeDocumentationText(element.Value);
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
