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

        var el = member.Element("summary");
        return el?.Value.Trim();
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

        var el = member.Element("remarks");
        return el?.Value.Trim();
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
                p.Value.Trim()))
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

        var el = member.Element("returns");
        return el?.Value.Trim();
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
}
