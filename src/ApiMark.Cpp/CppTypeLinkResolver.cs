// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

namespace ApiMark.Cpp;

/// <summary>
///     Resolves C++ type strings to Markdown link text suitable for table cells in
///     the generated API documentation.
/// </summary>
/// <remarks>
///     Linkification is applied only in table cells — never inside fenced code blocks,
///     because Markdown links do not render inside fences. Three outcomes are possible
///     for each type string:
///     <list type="bullet">
///       <item>
///         <term>Intra-library type</term>
///         <description>
///             When the stripped base name matches a key in <see cref="_knownTypes"/>,
///             a relative Markdown link to the type's page is emitted.
///         </description>
///       </item>
///       <item>
///         <term>Primitive or <c>std::</c> type</term>
///         <description>
///             Emitted as plain text and NOT tracked as an external type.
///         </description>
///       </item>
///       <item>
///         <term>Non-std external type</term>
///         <description>
///             Emitted as plain text and added to the caller-supplied
///             <see cref="CppExternalTypeInfo"/> set for later emission in the
///             "External Types" section.
///         </description>
///       </item>
///     </list>
///     Stateless with respect to type-link resolution itself; mutable state is
///     carried only via the caller-supplied <see cref="ISet{T}"/> parameter.
///     Thread-safe for concurrent resolution when each caller supplies its own
///     <see cref="ISet{T}"/> instance.
/// </remarks>
internal sealed class CppTypeLinkResolver
{
    /// <summary>
    ///     C++ primitive type names that are always rendered as plain text and never
    ///     tracked as external dependencies.
    /// </summary>
    private static readonly HashSet<string> Primitives = new(StringComparer.Ordinal)
    {
        "void", "bool", "int", "short", "long", "unsigned", "float", "double",
        "char", "wchar_t", "size_t", "ptrdiff_t", "auto", "nullptr_t",
        "int8_t", "int16_t", "int32_t", "int64_t",
        "uint8_t", "uint16_t", "uint32_t", "uint64_t",
    };

    /// <summary>
    ///     Maps fully-qualified C++ type names (using <c>::</c> separators) to their
    ///     documentation page keys (using <c>/</c> separators).
    ///     For example, <c>"fixtures::SampleClass"</c> maps to <c>"fixtures/SampleClass"</c>.
    /// </summary>
    private readonly IReadOnlyDictionary<string, string> _knownTypes;

    /// <summary>
    ///     Initializes a new instance of <see cref="CppTypeLinkResolver"/> with the
    ///     map of known intra-library types.
    /// </summary>
    /// <param name="knownTypes">
    ///     Dictionary mapping fully-qualified C++ type names (<c>::</c> separators) to
    ///     page keys (<c>/</c> separators). Must not be null.
    /// </param>
    public CppTypeLinkResolver(IReadOnlyDictionary<string, string> knownTypes)
    {
        _knownTypes = knownTypes;
    }

    /// <summary>
    ///     Resolves <paramref name="cppTypeString"/> to a Markdown link when it names
    ///     an intra-library type, plain text when it is a primitive or <c>std::</c> type,
    ///     or plain text with external tracking when it names a non-std external type.
    /// </summary>
    /// <param name="cppTypeString">
    ///     The simplified C++ type string to resolve (e.g. <c>"const fixtures::SampleClass &amp;"</c>).
    ///     Must not be null.
    /// </param>
    /// <param name="currentFolder">
    ///     The folder path of the Markdown file that will contain the link, relative to the
    ///     documentation output root (e.g. <c>"fixtures/SampleClass"</c>).
    ///     Used to compute relative path hrefs. Pass an empty string for root-level files.
    /// </param>
    /// <param name="externalTypes">
    ///     Mutable set that accumulates non-std external type references found during
    ///     resolution. The caller creates this set per output file and emits the "External
    ///     Types" section after all table rows have been written.
    /// </param>
    /// <returns>
    ///     A Markdown string: either a link of the form <c>[Name](relative/path.md)</c>,
    ///     or the original <paramref name="cppTypeString"/> unchanged.
    /// </returns>
    public string Linkify(
        string cppTypeString,
        string currentFolder,
        ISet<CppExternalTypeInfo> externalTypes)
    {
        if (string.IsNullOrWhiteSpace(cppTypeString))
        {
            return cppTypeString;
        }

        // Strip qualifiers to isolate the base type name for lookup
        var stripped = StripQualifiers(cppTypeString);
        if (string.IsNullOrEmpty(stripped))
        {
            return cppTypeString;
        }

        // Primitives are always rendered as plain text
        if (Primitives.Contains(stripped))
        {
            return cppTypeString;
        }

        // std:: types are always rendered as plain text and never tracked as external;
        // only the stripped base name is checked so that intra-library types whose
        // signatures contain std:: template arguments (e.g. Foo<std::string>) are
        // still linkified correctly
        if (stripped.StartsWith("std::", StringComparison.Ordinal))
        {
            return cppTypeString;
        }

        // Check for an exact qualified-name match first, then fall back to short-name matching
        var pageKey = FindPageKey(stripped);

        if (pageKey != null)
        {
            // Intra-library type: replace only the base type token in the original string,
            // preserving qualifiers (const, *, &, etc.) around the link
            var from = currentFolder.Length > 0 ? currentFolder : ".";
            var relativePath = Path.GetRelativePath(from, pageKey + ".md").Replace('\\', '/');
            var shortName = stripped.Contains("::", StringComparison.Ordinal)
                ? stripped[(stripped.LastIndexOf("::", StringComparison.Ordinal) + 2)..]
                : stripped;
            var linked = $"[{shortName}]({relativePath})";
            return cppTypeString.Replace(shortName, linked, StringComparison.Ordinal);
        }

        // External type with a namespace: track for the External Types section
        var ns = ExtractNamespace(stripped);
        if (!string.IsNullOrEmpty(ns) && ns != "std" && !ns.StartsWith("std::", StringComparison.Ordinal))
        {
            var lastSep = stripped.LastIndexOf("::", StringComparison.Ordinal);
            var shortName = lastSep >= 0 ? stripped[(lastSep + 2)..] : stripped;
            externalTypes.Add(new CppExternalTypeInfo(shortName, ns));
        }

        return cppTypeString;
    }

    /// <summary>
    ///     Looks up the page key for <paramref name="stripped"/> in <see cref="_knownTypes"/>.
    ///     First tries an exact qualified match; falls back to matching by short (unqualified) name.
    /// </summary>
    /// <param name="stripped">The base type name after qualifier removal.</param>
    /// <returns>The page key when found; <see langword="null"/> otherwise.</returns>
    private string? FindPageKey(string stripped)
    {
        // Exact match on the fully-qualified name
        if (_knownTypes.TryGetValue(stripped, out var key))
        {
            return key;
        }

        // Short-name fallback: only used when exactly one known type has this unqualified name.
        // If multiple types share the same short name (e.g. Outer::size_type and Other::size_type),
        // the reference is ambiguous and we fall through to external-type tracking instead.
        string? matchKey = null;
        var ambiguous = false;
        foreach (var (knownQualified, knownKey) in _knownTypes)
        {
            var lastSep = knownQualified.LastIndexOf("::", StringComparison.Ordinal);
            var shortName = lastSep >= 0 ? knownQualified[(lastSep + 2)..] : knownQualified;
            if (shortName == stripped)
            {
                if (matchKey != null)
                {
                    ambiguous = true;
                    break;
                }

                matchKey = knownKey;
            }
        }

        return ambiguous ? null : matchKey;
    }

    /// <summary>
    ///     Removes C++ cv-qualifiers (<c>const</c>, <c>volatile</c>), reference qualifiers
    ///     (<c>&amp;</c>, <c>&amp;&amp;</c>), pointer qualifiers (<c>*</c>), and generic
    ///     template arguments from a type string to isolate the base type name.
    /// </summary>
    /// <remarks>
    ///     Template arguments are removed because the base name is what identifies the type
    ///     in the documentation tree; e.g. <c>Stack&lt;T&gt;</c> resolves via <c>Stack</c>.
    /// </remarks>
    /// <param name="typeString">The raw C++ type string to strip.</param>
    /// <returns>The base type name without qualifiers or template arguments.</returns>
    internal static string StripQualifiers(string typeString)
    {
        var s = typeString.Trim();

        // Remove leading cv-qualifiers
        if (s.StartsWith("const ", StringComparison.Ordinal))
        {
            s = s[6..].TrimStart();
        }

        if (s.StartsWith("volatile ", StringComparison.Ordinal))
        {
            s = s[9..].TrimStart();
        }

        // Remove trailing reference, pointer, and trailing-const qualifiers iteratively
        s = s.TrimEnd();
        while (true)
        {
            if (s.EndsWith(" &&", StringComparison.Ordinal)) { s = s[..^3].TrimEnd(); continue; }
            if (s.EndsWith("&&", StringComparison.Ordinal)) { s = s[..^2].TrimEnd(); continue; }
            if (s.EndsWith(" &", StringComparison.Ordinal)) { s = s[..^2].TrimEnd(); continue; }
            if (s.EndsWith('&')) { s = s[..^1].TrimEnd(); continue; }
            if (s.EndsWith(" *", StringComparison.Ordinal)) { s = s[..^2].TrimEnd(); continue; }
            if (s.EndsWith('*')) { s = s[..^1].TrimEnd(); continue; }
            if (s.EndsWith(" const", StringComparison.OrdinalIgnoreCase)) { s = s[..^6].TrimEnd(); continue; }

            break;
        }

        // Remove template arguments — base name is everything before the first '<'
        var ltIdx = s.IndexOf('<');
        if (ltIdx >= 0)
        {
            s = s[..ltIdx].TrimEnd();
        }

        return s;
    }

    /// <summary>
    ///     Extracts the namespace portion from a qualified C++ name by taking everything
    ///     before the last <c>::</c> separator.
    /// </summary>
    /// <param name="qualifiedName">The qualified C++ name (e.g. <c>"acme::io::Logger"</c>).</param>
    /// <returns>
    ///     The namespace (e.g. <c>"acme::io"</c>), or an empty string when the name is
    ///     unqualified.
    /// </returns>
    private static string ExtractNamespace(string qualifiedName)
    {
        var lastSep = qualifiedName.LastIndexOf("::", StringComparison.Ordinal);
        return lastSep >= 0 ? qualifiedName[..lastSep] : string.Empty;
    }
}
