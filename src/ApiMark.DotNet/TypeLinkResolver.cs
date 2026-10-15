// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using Mono.Cecil;

namespace ApiMark.DotNet;

/// <summary>
///     Resolves Mono.Cecil <see cref="TypeReference"/> instances to Markdown link
///     text suitable for table cells in the generated API documentation.
/// </summary>
/// <remarks>
///     Linkification is applied only in table cells — never inside fenced code blocks,
///     because Markdown links do not render inside fences. Four outcomes are possible
///     for each type reference:
///     <list type="bullet">
///       <item>
///         <term>Intra-assembly type</term>
///         <description>
///             Detected when <see cref="TypeReference.Scope"/> is a
///             <see cref="ModuleDefinition"/>, meaning the type is declared inside the
///             assembly being documented. A relative Markdown link to the type's page is
///             emitted.
///         </description>
///       </item>
///       <item>
///         <term>C# primitive or System.* type</term>
///         <description>
///             Emitted as plain text using <see cref="TypeNameSimplifier.Simplify(TypeReference, string, bool)"/>
///             and NOT tracked as an external type.
///         </description>
///       </item>
///       <item>
///         <term>Mapped external type</term>
///         <description>
///             Detected when the type's assembly has an entry in the external link map. A link
///             into that assembly's documentation is emitted and the type is NOT tracked as an
///             external type.
///         </description>
///       </item>
///       <item>
///         <term>Unmapped non-System external type</term>
///         <description>
///             Emitted as plain text and added to the caller-supplied
///             <see cref="ExternalTypeInfo"/> set for later emission in the
///             "External Types" section.
///         </description>
///       </item>
///     </list>
///     Stateless with respect to the type-link resolution itself; mutable state is
///     carried only via the caller-supplied <see cref="ISet{T}"/> parameter.
///     Thread-safe for concurrent resolution of different type references when each
///     caller supplies its own <see cref="ISet{T}"/> instance.
/// </remarks>
internal sealed class TypeLinkResolver
{
    /// <summary>
    ///     Describes where another assembly's generated API documentation lives so types from that
    ///     assembly can be linked instead of listed in the External Types table.
    /// </summary>
    /// <param name="ApiPath">
    ///     Either a relative path from this documentation's root to the other API root
    ///     (for example <c>../OtherLib/api</c>) or an absolute <c>http(s)</c> base URL.
    /// </param>
    /// <param name="RootNamespaces">
    ///     The other assembly's root namespaces, used to derive its namespace folders exactly as that
    ///     assembly's own documentation run did.
    /// </param>
    /// <param name="Namespaces">
    ///     Every namespace that declares a public type in the other assembly, or <see langword="null"/> when
    ///     unknown. Used to separate a cref's namespace from its (possibly nested) type chain.
    /// </param>
    internal sealed record ExternalLinkTarget(string ApiPath, IReadOnlyList<string> RootNamespaces, IReadOnlyList<string>? Namespaces = null);

    /// <summary>
    ///     Full CLR names of C# primitive types that are always rendered as their keyword
    ///     alias and never tracked as external dependencies.
    /// </summary>
    private static readonly HashSet<string> PrimitiveFullNames = new(StringComparer.Ordinal)
    {
        "System.Boolean", "System.Byte", "System.SByte", "System.Int16", "System.UInt16",
        "System.Int32", "System.UInt32", "System.Int64", "System.UInt64", "System.Single",
        "System.Double", "System.Decimal", "System.Char", "System.String", "System.Object",
        "System.Void", "System.IntPtr", "System.UIntPtr",
    };

    /// <summary>Root namespaces of the assembly, used to derive relative documentation paths.</summary>
    private readonly IReadOnlyList<string> _rootNamespaces;

    /// <summary>
    ///     When <see langword="false"/>, intra-assembly type references are rendered as plain
    ///     text instead of Markdown links. Used by the single-file emitter where relative file
    ///     links are meaningless and same-name anchors across types would collide.
    /// </summary>
    private readonly bool _generateLinks;

    /// <summary>Map from referenced assembly name to its documentation location (case-insensitive).</summary>
    private readonly Dictionary<string, ExternalLinkTarget> _externalLinks;

    /// <summary>
    ///     Initializes a new instance of <see cref="TypeLinkResolver"/> with the assembly root namespaces.
    /// </summary>
    /// <param name="rootNamespaces">
    ///     The root namespaces identified in the assembly being documented. These are forwarded to
    ///     <see cref="DotNetEmitter.GetNamespaceFolderPath"/> to derive the documentation path for
    ///     each referenced type.
    /// </param>
    /// <param name="generateLinks">
    ///     When <see langword="true"/> (default), intra-assembly types are rendered as relative
    ///     Markdown links. When <see langword="false"/>, all types are rendered as plain text —
    ///     used in single-file output where file-relative links are invalid.
    /// </param>
    /// <param name="externalLinks">
    ///     Optional map from referenced assembly name to the location of that assembly's generated
    ///     documentation. Types from a mapped assembly are linked and left out of the External Types
    ///     table; types from unmapped assemblies keep the table-only behavior. Ignored when
    ///     <paramref name="generateLinks"/> is <see langword="false"/>.
    /// </param>
    public TypeLinkResolver(
        IReadOnlyList<string> rootNamespaces,
        bool generateLinks = true,
        IReadOnlyDictionary<string, ExternalLinkTarget>? externalLinks = null)
    {
        _rootNamespaces = rootNamespaces;
        _generateLinks = generateLinks;

        // Always copy into a case-insensitive dictionary: assembly names compare case-insensitively
        // regardless of the comparer of the caller-supplied dictionary.
        _externalLinks = new Dictionary<string, ExternalLinkTarget>(StringComparer.OrdinalIgnoreCase);
        if (externalLinks != null)
        {
            foreach (var (name, target) in externalLinks)
            {
                _externalLinks[name] = target;
            }
        }
    }

    /// <summary>
    ///     Escapes characters in a link target that would terminate or corrupt a Markdown inline link.
    /// </summary>
    /// <param name="target">The raw link target.</param>
    /// <returns>The target with spaces, parentheses, and angle brackets percent-encoded.</returns>
    private static string EscapeLinkTarget(string target) =>
        target
            .Replace(" ", "%20", StringComparison.Ordinal)
            .Replace("(", "%28", StringComparison.Ordinal)
            .Replace(")", "%29", StringComparison.Ordinal)
            .Replace("<", "%3C", StringComparison.Ordinal)
            .Replace(">", "%3E", StringComparison.Ordinal);

    /// <summary>
    ///     Builds a link to a type documented by another assembly's ApiMark run, when that assembly is mapped.
    /// </summary>
    /// <param name="typeRef">The external type reference (open type for generic instances).</param>
    /// <param name="currentFolder">Folder of the page that will contain the link.</param>
    /// <param name="displayName">The link text.</param>
    /// <returns>A Markdown link, or <see langword="null"/> when linking is disabled or the assembly is not mapped.</returns>
    private string? TryLinkExternalType(TypeReference typeRef, string currentFolder, string displayName)
    {
        if (!_generateLinks ||
            typeRef.Scope is not AssemblyNameReference assemblyRef ||
            !_externalLinks.TryGetValue(assemblyRef.Name, out var target))
        {
            return null;
        }

        return BuildExternalLink(target, BuildTypePageKey(typeRef, target.RootNamespaces), currentFolder, displayName);
    }

    /// <summary>Builds the Markdown link to page <paramref name="key"/> of another assembly's documentation.</summary>
    /// <param name="target">The mapped documentation location.</param>
    /// <param name="key">The page key (relative path without extension) within that documentation.</param>
    /// <param name="currentFolder">Folder of the page that will contain the link.</param>
    /// <param name="displayName">The link text.</param>
    /// <returns>A Markdown link whose target is absolute or relative according to the mapping.</returns>
    private static string BuildExternalLink(ExternalLinkTarget target, string key, string currentFolder, string displayName)
    {
        var apiPath = target.ApiPath.Replace('\\', '/').TrimEnd('/');

        // Absolute bases (http(s) URL, file: URI) are used as-is, never made relative
        if (apiPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            apiPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            apiPath.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return $"[{displayName}]({EscapeLinkTarget($"{apiPath}/{key}.md")})";
        }

        // Rooted file-system paths (/x or C:/x) become file: URIs; relative climbing would be wrong
        if (apiPath.StartsWith('/') || Path.IsPathRooted(target.ApiPath))
        {
            var fileBase = "file://" + (apiPath.StartsWith('/') ? string.Empty : "/") + apiPath;
            return $"[{displayName}]({EscapeLinkTarget($"{fileBase}/{key}.md")})";
        }

        // Relative mapping: climb out of the current folder, then descend into the other API root
        var depth = currentFolder.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
        var prefix = string.Concat(Enumerable.Repeat("../", depth));
        while (apiPath.StartsWith("./", StringComparison.Ordinal))
        {
            apiPath = apiPath.Substring(2);
        }

        return $"[{displayName}]({EscapeLinkTarget($"{prefix}{apiPath}/{key}.md")})";
    }

    /// <summary>
    ///     Resolves <paramref name="typeRef"/> to a Markdown link when it is an intra-assembly
    ///     type, plain text when it is a primitive or System type, or plain text with external
    ///     tracking when it is a non-System type from another assembly.
    /// </summary>
    /// <param name="typeRef">The Mono.Cecil type reference to resolve. When <see langword="null"/>, an empty string is returned.</param>
    /// <param name="currentFolder">
    ///     The folder path of the Markdown file that will contain the link, relative to the
    ///     documentation output root (e.g. <c>ApiMark.DotNet.Fixtures/SampleClass</c>).
    ///     Used to compute relative path hrefs. Pass an empty string for root-level files.
    /// </param>
    /// <param name="contextNamespace">
    ///     The namespace of the type that owns this reference. Forwarded to
    ///     <see cref="TypeNameSimplifier.Simplify(TypeReference, string, bool)"/> so that same-namespace names are
    ///     displayed without their namespace prefix.
    /// </param>
    /// <param name="externalTypes">
    ///     Mutable set that accumulates non-System external type references found during
    ///     resolution. The caller creates this set per output file and emits the "External
    ///     Types" section after all table rows have been written.
    /// </param>
    /// <param name="isNullableAnnotated">
    ///     When <see langword="true"/>, the member carrying this type reference has a
    ///     nullable-reference annotation. See
    ///     <see cref="TypeNameSimplifier.Simplify(TypeReference, string, bool)"/> for details on when to pass
    ///     <see langword="true"/>.
    /// </param>
    /// <returns>
    ///     A Markdown string: either a link of the form <c>[Name](relative/path.md)</c>,
    ///     a plain simplified name, or a generic composite such as
    ///     <c>[Container](path.md)\&lt;Arg\&gt;</c>.
    /// </returns>
    public string Linkify(
        TypeReference typeRef,
        string currentFolder,
        string contextNamespace,
        ISet<ExternalTypeInfo> externalTypes,
        bool isNullableAnnotated = false) =>
        Linkify(typeRef, currentFolder, contextNamespace, externalTypes, NullabilityCursor.ForOutermostFlag(isNullableAnnotated));

    /// <summary>
    ///     Resolves <paramref name="typeRef"/> like the boolean overload, but applies a per-position
    ///     nullable-reference annotation to the outer type, array elements, and generic arguments.
    /// </summary>
    /// <param name="typeRef">The Mono.Cecil type reference to resolve. When <see langword="null"/>, an empty string is returned.</param>
    /// <param name="currentFolder">Folder path of the Markdown file that will contain the link.</param>
    /// <param name="contextNamespace">Namespace context for name simplification.</param>
    /// <param name="externalTypes">Accumulator for non-System external type references.</param>
    /// <param name="nullability">The nullable flag cursor for the member, or <see langword="null"/> for none.</param>
    /// <returns>A Markdown string for the type.</returns>
    internal string Linkify(
        TypeReference typeRef,
        string currentFolder,
        string contextNamespace,
        ISet<ExternalTypeInfo> externalTypes,
        NullabilityCursor? nullability)
    {
        if (typeRef == null)
        {
            return string.Empty;
        }

        // Byref parameter types (ref/out/in) are marked by Cecil with a trailing "&" on the type
        // name. The ref/out/in keyword itself is the caller's responsibility (see
        // DotNetEmitter.GetRefKindKeyword) — unwrap to the underlying element type here so both
        // the display text and the computed link target use the real (un-suffixed) type name.
        // The byref wrapper consumes no nullable flag.
        if (typeRef is ByReferenceType byRefType)
        {
            return Linkify(byRefType.ElementType, currentFolder, contextNamespace, externalTypes, nullability);
        }

        // Every other type position (except non-generic value types and Nullable<T>, which the
        // compiler does not encode) consumes exactly one nullable flag, in pre-order
        var flag = TypeNameSimplifier.ConsumesNullableFlag(typeRef) ? (nullability?.Next() ?? (byte)0) : (byte)0;
        var annotated = TypeNameSimplifier.IsAnnotated(typeRef, flag);

        // Generic type parameters (e.g. T, TKey) are not real types — render as plain text,
        // appending "?" when the annotation indicates the parameter itself is nullable
        if (typeRef is GenericParameter genericParam)
        {
            return LinkifyGenericParameter(genericParam, annotated);
        }

        // C# tuples render as (A a, B b), with each element linkified in turn
        if (typeRef is GenericInstanceType tupleType && TypeNameSimplifier.IsTupleType(tupleType))
        {
            return TypeNameSimplifier.BuildTuple(
                tupleType,
                nullability,
                e => Linkify(e, currentFolder, contextNamespace, externalTypes, nullability));
        }

        // Handle Nullable<T> → T?: Nullable itself is not encoded; the inner type consumes its own flag (if any)
        if (typeRef is GenericInstanceType gitNullable &&
            typeRef.FullName.StartsWith("System.Nullable`1<", StringComparison.Ordinal))
        {
            var inner = gitNullable.GenericArguments[0];
            return Linkify(inner, currentFolder, contextNamespace, externalTypes, nullability) + "?";
        }

        // Handle array types by resolving the element type and appending the rank-aware suffix
        if (typeRef is ArrayType arrayType)
        {
            return LinkifyArrayType(arrayType, currentFolder, contextNamespace, externalTypes, nullability, annotated);
        }

        // Handle generic instance types: linkify the container when intra-assembly, else track external
        if (typeRef is GenericInstanceType genericType)
        {
            return LinkifyGenericType(genericType, currentFolder, contextNamespace, externalTypes, nullability, annotated);
        }

        // Primitives and Nullable<> open type render as plain C# aliases, never as external types
        if (PrimitiveFullNames.Contains(typeRef.FullName) ||
            typeRef.FullName == "System.Nullable`1")
        {
            return LinkifyPrimitiveType(typeRef, contextNamespace, annotated);
        }

        // Intra-assembly types get a relative Markdown link to their documentation page
        if (IsIntraAssembly(typeRef))
        {
            return LinkifyIntraAssemblyType(typeRef, currentFolder, annotated);
        }

        // Non-System external types are tracked for the External Types section
        return LinkifyExternalType(typeRef, currentFolder, externalTypes, annotated);
    }

    /// <summary>
    ///     Resolves a <c>cref</c> target already known to be a type declared in the assembly
    ///     being documented to a Markdown link wrapping <paramref name="displayText"/>.
    /// </summary>
    /// <remarks>
    ///     Reuses the same <see cref="GetTypePageKey"/>/<see cref="ComputeRelativePath"/> helpers
    ///     that <see cref="Linkify(TypeReference, string, string, ISet{ExternalTypeInfo}, bool)"/> already uses for intra-assembly <see cref="TypeReference"/>
    ///     values — <see cref="TypeDefinition"/> is itself a <see cref="TypeReference"/>, so no new
    ///     page-key derivation logic is required. Callers are expected to have already confirmed
    ///     (via <see cref="CrefTargetResolver"/> and the emitter's visibility rules) that
    ///     <paramref name="type"/> will actually be emitted as a page before calling this method;
    ///     this method itself performs no such check and always computes a link when link
    ///     generation is enabled.
    /// </remarks>
    /// <param name="type">The resolved intra-assembly type the <c>cref</c> refers to.</param>
    /// <param name="displayText">
    ///     The already-formatted display text (e.g. from <c>XmlDocReader.FormatCref</c>) to wrap
    ///     in the Markdown link.
    /// </param>
    /// <param name="currentFolder">Folder path of the containing Markdown file.</param>
    /// <returns>
    ///     A Markdown link of the form <c>[displayText](relative/path.md)</c>, or
    ///     <paramref name="displayText"/> unchanged when link generation is disabled (single-file
    ///     mode).
    /// </returns>
    internal string LinkifyResolvedType(TypeDefinition type, string displayText, string currentFolder)
    {
        if (!_generateLinks)
        {
            return displayText;
        }

        var pageKey = GetTypePageKey(type);
        var relativePath = ComputeRelativePath(currentFolder, pageKey);
        return $"[{displayText}]({relativePath})";
    }

    /// <summary>
    ///     Resolves a <c>cref</c> target already known to be a member declared in the assembly
    ///     being documented to a Markdown link wrapping <paramref name="displayText"/>, using the
    ///     gradual-disclosure member-to-page index.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="LinkifyResolvedType"/>, there is no closed-form page-key computation
    ///     for a member — in gradual-disclosure mode, members may share a page with their overloads,
    ///     with same-named-but-different-kind members (case-collision grouping), or with every
    ///     other operator overload of the same type, decisions made per-type while generating that
    ///     type's own pages (see <c>DotNetEmitterGradualDisclosure.BuildMemberPageIndex</c>).
    ///     <paramref name="memberPageIndex"/> is therefore required to resolve the member's actual
    ///     page path.
    /// </remarks>
    /// <param name="member">The resolved intra-assembly member the <c>cref</c> refers to.</param>
    /// <param name="displayText">
    ///     The already-formatted display text (e.g. from <c>XmlDocReader.FormatMemberReference</c>)
    ///     to wrap in the Markdown link.
    /// </param>
    /// <param name="currentFolder">Folder path of the containing Markdown file.</param>
    /// <param name="memberPageIndex">
    ///     Map from a member's XML-doc identifier (see <see cref="DotNetEmitter.BuildMemberId"/>)
    ///     to its page key (a forward-slash-separated relative path, without the <c>.md</c>
    ///     extension).
    /// </param>
    /// <returns>
    ///     A Markdown link of the form <c>[displayText](relative/path.md)</c>, or
    ///     <paramref name="displayText"/> unchanged when link generation is disabled (single-file
    ///     mode) or when <paramref name="member"/> has no entry in
    ///     <paramref name="memberPageIndex"/> (e.g. the member is filtered out of the generated
    ///     documentation).
    /// </returns>
    internal string LinkifyResolvedMember(
        IMemberDefinition member,
        string displayText,
        string currentFolder,
        IReadOnlyDictionary<string, string> memberPageIndex)
    {
        if (!_generateLinks)
        {
            return displayText;
        }

        var memberId = DotNetEmitter.BuildMemberId(member);
        if (!memberPageIndex.TryGetValue(memberId, out var pageKey))
        {
            return displayText;
        }

        // The index value may carry a "#anchor" fragment pointing at the member's section on a shared overload page
        var fragmentIndex = pageKey.IndexOf('#', StringComparison.Ordinal);
        var fragment = fragmentIndex >= 0 ? pageKey[fragmentIndex..] : string.Empty;
        var pagePath = fragmentIndex >= 0 ? pageKey[..fragmentIndex] : pageKey;
        var relativePath = ComputeRelativePath(currentFolder, pagePath) + fragment;
        return $"[{displayText}]({relativePath})";
    }

    /// <summary>
    ///     Links a <c>cref</c> that names a type (or a member of a type) documented by another
    ///     assembly's ApiMark run, using the <see cref="ExternalLinkTarget"/> whose root namespaces
    ///     contain the cref's namespace (longest root namespace wins).
    /// </summary>
    /// <remarks>
    ///     A member cref links to its containing type's page, because the other documentation's
    ///     per-member page layout cannot be known from the cref alone. The namespace is the longest
    ///     dotted prefix that is a real namespace of the external assembly (read from its metadata),
    ///     and the remaining segments form the nested-type chain, so <c>Ns.Outer.Inner</c> links to
    ///     <c>Ns/Outer/Inner.md</c> and generic arities (<c>Outer`1</c>) are flattened like the other
    ///     run's page names. When the namespace list is unknown, the last segment before any member
    ///     is taken as a namespace-level type. Returns <see langword="null"/> (so the caller keeps its plain code
    ///     span) when links are disabled, nothing is mapped, or no mapped namespace matches.
    /// </remarks>
    /// <param name="cref">The raw <c>cref</c> attribute value including its kind prefix.</param>
    /// <param name="displayText">The already-formatted display text used as the link label.</param>
    /// <param name="currentFolder">Folder path of the containing Markdown file.</param>
    /// <returns>A Markdown link, or <see langword="null"/> when the cref is not covered by a mapping.</returns>
    internal string? TryLinkifyExternalCref(string cref, string displayText, string currentFolder)
    {
        if (!_generateLinks || _externalLinks.Count == 0 || cref.Length < 3 || cref[1] != ':' || cref[0] is not ('T' or 'M' or 'P' or 'F' or 'E'))
        {
            return null;
        }

        var name = cref.Substring(2);
        var parenthesis = name.IndexOf('(');
        if (parenthesis >= 0)
        {
            name = name.Substring(0, parenthesis);
        }

        var segments = name.Split('.').ToList();
        if (cref[0] != 'T')
        {
            // Drop the member name so only the declaring type remains
            segments.RemoveAt(segments.Count - 1);
        }

        if (segments.Count < 2)
        {
            return null;
        }

        ExternalLinkTarget? best = null;
        var bestNamespace = string.Empty;
        var bestLength = -1;
        foreach (var target in _externalLinks.Values)
        {
            var namespaceName = FindExternalNamespace(segments, target);
            if (namespaceName != null && namespaceName.Length > bestLength)
            {
                best = target;
                bestNamespace = namespaceName;
                bestLength = namespaceName.Length;
            }
        }

        if (best == null)
        {
            return null;
        }

        // The segments after the namespace form the nested-type chain; flatten each name as the other run did
        var namespaceSegmentCount = bestNamespace.Split('.').Length;
        var path = string.Join('/', segments.Skip(namespaceSegmentCount).Select(TypeNameSimplifier.FlattenArity));
        var folder = DotNetEmitter.GetNamespaceFolderPath(bestNamespace, best.RootNamespaces);
        return BuildExternalLink(best, folder.Length > 0 ? $"{folder}/{path}" : path, currentFolder, displayText);
    }

    /// <summary>
    ///     Finds the namespace part of a dotted cref name for an external target, leaving at least one
    ///     segment for the type chain.
    /// </summary>
    /// <remarks>
    ///     When the target's full namespace list is known, the longest prefix that is a real namespace
    ///     wins, so nested types (<c>Ns.Outer.Inner</c>) are not mistaken for namespaces. Without the
    ///     list, every segment but the last is taken as the namespace, which is correct only for
    ///     namespace-level types, and it must still lie under a root namespace.
    /// </remarks>
    /// <param name="segments">The dotted cref name split into segments, with any member removed.</param>
    /// <param name="target">The external target to match.</param>
    /// <returns>The namespace, or <see langword="null"/> when the target does not own the name.</returns>
    private static string? FindExternalNamespace(IReadOnlyList<string> segments, ExternalLinkTarget target)
    {
        if (target.Namespaces == null)
        {
            var guess = string.Join('.', segments.Take(segments.Count - 1));
            return target.RootNamespaces.Any(r => guess == r || guess.StartsWith(r + ".", StringComparison.Ordinal)) ? guess : null;
        }

        for (var count = segments.Count - 1; count >= 1; count--)
        {
            var candidate = string.Join('.', segments.Take(count));
            if (target.Namespaces.Contains(candidate, StringComparer.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }
    /// <summary>Renders a generic type parameter (e.g. <c>T</c>, <c>TKey</c>) as plain text.</summary>
    /// <param name="genericParam">The generic parameter reference.</param>
    /// <param name="isNullableAnnotated">Whether the parameter itself is annotated nullable.</param>
    /// <returns>The parameter name, with a trailing <c>?</c> when nullable-annotated.</returns>
    private static string LinkifyGenericParameter(GenericParameter genericParam, bool isNullableAnnotated) =>
        isNullableAnnotated ? genericParam.Name + "?" : genericParam.Name;

    /// <summary>Resolves an array type by linkifying its element type and appending the rank-aware suffix.</summary>
    /// <param name="arrayType">The array type reference.</param>
    /// <param name="currentFolder">Folder path of the containing Markdown file.</param>
    /// <param name="contextNamespace">Namespace context for name simplification.</param>
    /// <param name="externalTypes">Accumulator for external type references.</param>
    /// <param name="nullability">The nullable flag cursor, positioned at the array's element type.</param>
    /// <param name="isNullableAnnotated">Whether the array itself is annotated nullable.</param>
    /// <returns>A Markdown cell string for the array type.</returns>
    private string LinkifyArrayType(
        ArrayType arrayType,
        string currentFolder,
        string contextNamespace,
        ISet<ExternalTypeInfo> externalTypes,
        NullabilityCursor? nullability,
        bool isNullableAnnotated)
    {
        var elementText = Linkify(arrayType.ElementType, currentFolder, contextNamespace, externalTypes, nullability);
        var suffix = "[" + new string(',', arrayType.Rank - 1) + "]";
        return isNullableAnnotated ? elementText + suffix + "?" : elementText + suffix;
    }

    /// <summary>Renders a primitive type (or the open <c>Nullable&lt;&gt;</c> type) as its simplified C# alias.</summary>
    /// <param name="typeRef">The primitive type reference.</param>
    /// <param name="contextNamespace">Namespace context for name simplification.</param>
    /// <param name="isNullableAnnotated">Whether the nullable-reference annotation is present.</param>
    /// <returns>The simplified alias, with a trailing <c>?</c> when nullable-annotated.</returns>
    private static string LinkifyPrimitiveType(TypeReference typeRef, string contextNamespace, bool isNullableAnnotated)
    {
        var alias = TypeNameSimplifier.Simplify(typeRef, contextNamespace);
        return isNullableAnnotated ? alias + "?" : alias;
    }

    /// <summary>Tracks a non-System external type reference and renders its simplified name.</summary>
    /// <param name="typeRef">The external type reference.</param>
    /// <param name="currentFolder">Folder path of the containing Markdown file.</param>
    /// <param name="externalTypes">Accumulator for external type references.</param>
    /// <param name="isNullableAnnotated">Whether the nullable-reference annotation is present.</param>
    /// <returns>The simplified type name, with a trailing <c>?</c> when nullable-annotated.</returns>
    private string LinkifyExternalType(TypeReference typeRef, string currentFolder, ISet<ExternalTypeInfo> externalTypes, bool isNullableAnnotated)
    {
        var name = TypeNameSimplifier.StripArity(typeRef.Name);
        var mapped = TryLinkExternalType(typeRef, currentFolder, name);
        if (mapped != null)
        {
            return isNullableAnnotated ? mapped + "?" : mapped;
        }

        TrackExternalType(typeRef, externalTypes);
        return isNullableAnnotated ? name + "?" : name;
    }

    /// <summary>
    ///     Resolves a generic instance type to a Markdown cell value, linking the container
    ///     when it is intra-assembly and tracking it as external otherwise.
    /// </summary>
    /// <param name="genericType">The generic instance type to resolve.</param>
    /// <param name="currentFolder">Folder path of the containing Markdown file.</param>
    /// <param name="contextNamespace">Namespace context for name simplification.</param>
    /// <param name="externalTypes">Accumulator for external type references.</param>
    /// <param name="nullability">The nullable flag cursor, positioned at the first generic argument.</param>
    /// <param name="isNullableAnnotated">Whether the generic type itself is annotated nullable.</param>
    /// <returns>A Markdown cell string for the generic type.</returns>
    private string LinkifyGenericType(
        GenericInstanceType genericType,
        string currentFolder,
        string contextNamespace,
        ISet<ExternalTypeInfo> externalTypes,
        NullabilityCursor? nullability,
        bool isNullableAnnotated)
    {
        var containerName = TypeNameSimplifier.StripArity(genericType.Name);
        var argTexts = genericType.GenericArguments
            .Select(arg => Linkify(arg, currentFolder, contextNamespace, externalTypes, nullability))
            .ToList();
        var argsText = string.Join(", ", argTexts);
        var suffix = isNullableAnnotated ? "?" : string.Empty;

        if (IsIntraAssembly(genericType))
        {
            if (_generateLinks)
            {
                var pageKey = GetTypePageKey(genericType);
                var relativePath = ComputeRelativePath(currentFolder, pageKey);
                var containerLink = $"[{containerName}]({relativePath})";
                return $"{containerLink}\\<{argsText}\\>{suffix}";
            }

            return $"{containerName}\\<{argsText}\\>{suffix}";
        }

        var mappedContainer = TryLinkExternalType(genericType.ElementType, currentFolder, containerName);
        if (mappedContainer != null)
        {
            return $"{mappedContainer}\\<{argsText}\\>{suffix}";
        }

        TrackExternalType(genericType, externalTypes);
        return $"{containerName}\\<{argsText}\\>{suffix}";
    }

    /// <summary>
    ///     Resolves a simple intra-assembly type reference to a Markdown link cell.
    /// </summary>
    /// <param name="typeRef">The intra-assembly type reference.</param>
    /// <param name="currentFolder">Folder path of the containing Markdown file.</param>
    /// <param name="isNullableAnnotated">Whether the nullable-reference annotation is present.</param>
    /// <returns>A Markdown link of the form <c>[Name](relative/path.md)</c>.</returns>
    private string LinkifyIntraAssemblyType(TypeReference typeRef, string currentFolder, bool isNullableAnnotated)
    {
        var simpleName = TypeNameSimplifier.StripArity(typeRef.Name);
        if (!_generateLinks)
        {
            return isNullableAnnotated ? simpleName + "?" : simpleName;
        }

        var pageKey = GetTypePageKey(typeRef);
        var relativePath = ComputeRelativePath(currentFolder, pageKey);
        return isNullableAnnotated
            ? $"[{simpleName}]({relativePath})?"
            : $"[{simpleName}]({relativePath})";
    }

    /// <summary>
    ///     Returns <see langword="true"/> when <paramref name="typeRef"/> is declared in the
    ///     assembly being documented, identified by its scope being a <see cref="ModuleDefinition"/>.
    /// </summary>
    /// <param name="typeRef">The type reference to test.</param>
    /// <returns>
    ///     <see langword="true"/> when the type is in the current module; <see langword="false"/>
    ///     when it is from an external assembly.
    /// </returns>
    private static bool IsIntraAssembly(TypeReference typeRef) =>
        typeRef.Scope is ModuleDefinition;

    /// <summary>
    ///     Computes the documentation page key (relative path without extension) for a type.
    /// </summary>
    /// <remarks>
    ///     Mono.Cecil gives nested types an EMPTY <see cref="TypeReference.Namespace"/> — the
    ///     namespace belongs only to the outermost non-nested ancestor. Using <c>typeRef.Namespace</c>
    ///     directly therefore produced broken links for any nested type. This walks
    ///     <see cref="TypeReference.DeclaringType"/> up to that outermost ancestor to derive the
    ///     namespace folder, then appends every declaring-type name from outermost to innermost
    ///     (each flattened via <see cref="TypeNameSimplifier.FlattenArity"/>), mirroring exactly how
    ///     <c>DotNetEmitterGradualDisclosure.WriteNestedTypesSection</c> builds each nested type's
    ///     page path (<c>{NamespaceFolderPath}/{ParentTypeName}/{NestedTypeName}</c>, recursively for
    ///     arbitrary nesting depth).
    /// </remarks>
    /// <param name="typeRef">The type whose page key to compute.</param>
    /// <returns>A forward-slash-separated page key such as <c>MyLib/MyType</c> or <c>MyLib/Outer/Inner</c>.</returns>
    private string GetTypePageKey(TypeReference typeRef) => BuildTypePageKey(typeRef, _rootNamespaces);

    /// <summary>Computes the page key for <paramref name="typeRef"/> using the supplied root namespaces.</summary>
    /// <param name="typeRef">The type whose page key to compute.</param>
    /// <param name="rootNamespaces">The root namespaces of the assembly that documents the type.</param>
    /// <returns>A forward-slash-separated page key.</returns>
    private static string BuildTypePageKey(TypeReference typeRef, IReadOnlyList<string> rootNamespaces)
    {
        // Walk the declaring-type chain, collecting ancestors from outermost to innermost
        // (including typeRef itself as the last entry).
        var chain = new List<TypeReference>();
        for (var current = typeRef; current != null; current = current.DeclaringType)
        {
            chain.Insert(0, current);
        }

        // The namespace folder is derived from the outermost ancestor — the only member of the
        // chain that carries a non-empty Namespace.
        var outermost = chain[0];
        var folder = DotNetEmitter.GetNamespaceFolderPath(outermost.Namespace, rootNamespaces);

        // Append each declaring-type name outermost-to-innermost, flattened for file-system safety.
        var path = string.Join('/', chain.Select(t => TypeNameSimplifier.FlattenArity(t.Name)));
        return folder.Length > 0 ? $"{folder}/{path}" : path;
    }

    /// <summary>
    ///     Computes a relative path from a source folder to a target page file.
    /// </summary>
    /// <param name="currentFolder">
    ///     The folder containing the Markdown file that will contain the link.
    ///     An empty string is treated as the root (current directory <c>.</c>).
    /// </param>
    /// <param name="targetKey">
    ///     The page key of the link target (e.g. <c>MyLib/MyType</c>). The <c>.md</c>
    ///     extension is appended during path computation.
    /// </param>
    /// <returns>
    ///     A forward-slash-separated relative path suitable for a Markdown link href
    ///     (e.g. <c>../MyType.md</c> or <c>MyType.md</c>).
    /// </returns>
    private static string ComputeRelativePath(string currentFolder, string targetKey)
    {
        var from = currentFolder.Length > 0 ? currentFolder : ".";
        return Path.GetRelativePath(from, targetKey + ".md").Replace('\\', '/');
    }

    /// <summary>
    ///     Records <paramref name="typeRef"/> as an external type in <paramref name="externalTypes"/>
    ///     when its namespace does not start with <c>System</c>.
    /// </summary>
    /// <remarks>
    ///     System-namespace types (e.g. <c>System.IO.Stream</c>) are excluded because they are
    ///     part of the .NET runtime and do not require readers to install separate packages.
    ///     Generic instance types record the open-generic form with escaped angle brackets so
    ///     that the External Types table shows a readable representation.
    /// </remarks>
    /// <param name="typeRef">The type reference to evaluate.</param>
    /// <param name="externalTypes">The set to add to when the type qualifies.</param>
    private static void TrackExternalType(TypeReference typeRef, ISet<ExternalTypeInfo> externalTypes)
    {
        if (typeRef.Namespace == "System" ||
            typeRef.Namespace.StartsWith("System.", StringComparison.Ordinal))
        {
            return;
        }

        // Build the display name with the same simplifier the signatures use, so generic arguments
        // keep their aliases (string, int), nested generics, arrays and nullable markers; then escape
        // the angle brackets so the cell shows them literally instead of parsing them as HTML tags
        var simpleName = DotNetEmitter.EscapeMarkdownHeading(TypeNameSimplifier.Simplify(typeRef, string.Empty));

        externalTypes.Add(new ExternalTypeInfo(simpleName, typeRef.Namespace));
    }
}

/// <summary>
///     Represents a non-standard external type reference found in the documentation.
/// </summary>
/// <remarks>
///     Instances are collected per generated Markdown file and emitted in the
///     "External Types" section so that readers know which additional packages or
///     headers they must include to use the documented API. System-namespace types
///     and C# primitives are excluded; only types whose namespace does not start
///     with "System" and are not CLR value-type primitives are recorded here.
/// </remarks>
/// <param name="SimplifiedName">The simplified type name as it appears in the documentation table.</param>
/// <param name="Namespace">The .NET namespace that declares the type.</param>
internal sealed record ExternalTypeInfo(string SimplifiedName, string Namespace)
    : IComparable<ExternalTypeInfo>
{
    /// <summary>
    ///     Compares this instance to <paramref name="other"/> by simplified name,
    ///     enabling deterministic alphabetical sorting of the External Types table.
    /// </summary>
    /// <param name="other">The other instance to compare against, or <see langword="null"/>.</param>
    /// <returns>
    ///     A negative value when this instance sorts before <paramref name="other"/>,
    ///     zero when equal, or a positive value when it sorts after.
    /// </returns>
    public int CompareTo(ExternalTypeInfo? other)
    {
        var nameComparison = StringComparer.Ordinal.Compare(SimplifiedName, other?.SimplifiedName);
        return nameComparison != 0
            ? nameComparison
            : StringComparer.Ordinal.Compare(Namespace, other?.Namespace);
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts before <paramref name="right"/>.</summary>
    public static bool operator <(ExternalTypeInfo left, ExternalTypeInfo right) =>
        left.CompareTo(right) < 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts before or equal to <paramref name="right"/>.</summary>
    public static bool operator <=(ExternalTypeInfo left, ExternalTypeInfo right) =>
        left.CompareTo(right) <= 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts after <paramref name="right"/>.</summary>
    public static bool operator >(ExternalTypeInfo left, ExternalTypeInfo right) =>
        left.CompareTo(right) > 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts after or equal to <paramref name="right"/>.</summary>
    public static bool operator >=(ExternalTypeInfo left, ExternalTypeInfo right) =>
        left.CompareTo(right) >= 0;
}
