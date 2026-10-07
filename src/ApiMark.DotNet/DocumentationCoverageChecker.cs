using System.Text.RegularExpressions;
using ApiMark.Core;
using Mono.Cecil;

namespace ApiMark.DotNet;

/// <summary>
///     Scans a parsed .NET assembly for types and members that lack an XML documentation
///     <c>&lt;summary&gt;</c>, at a caller-supplied visibility tier that is independent of the
///     tier used for Markdown emission.
/// </summary>
/// <remarks>
///     Reuses the low-level static predicates already defined on <see cref="DotNetEmitter"/>
///     (compiler-generated / obsolete / namespace-doc-carrier detection, member-id construction)
///     and on <see cref="DotNetGenerator"/> (exclude-pattern compilation and matching) rather than
///     duplicating that logic. Only the three-way visibility switch is re-derived locally because
///     the enforcement tier may differ from the emission <see cref="DotNetGeneratorOptions.Visibility"/>
///     tier and therefore cannot reuse <see cref="DotNetEmitter"/>'s emission-scoped instance methods,
///     which are bound to the model's single emission tier.
///     <para>
///     The bar for "documented" is deliberately shallow: a type or member is considered
///     documented when <see cref="XmlDocReader.GetSummary"/> returns a non-null, non-whitespace
///     string for its XML doc member identifier, or — for a member carrying
///     <see cref="System.CodeDom.Compiler.GeneratedCodeAttribute"/> (detected via
///     <see cref="DotNetEmitter.IsGeneratedCode"/>) — when it has a non-empty
///     <c>&lt;remarks&gt;</c> instead (see <see cref="HasDocumentation"/>). The fallback exists
///     because some source generators (e.g. the <c>[GeneratedRegex]</c> partial-method generator)
///     attach their own <c>&lt;remarks&gt;</c> to the generated implementation, silently replacing
///     the author's <c>&lt;summary&gt;</c> in the compiled XML doc. This fallback applies to
///     coverage checking only; it does not change what the emitted Markdown shows for the
///     member's summary. Checking for complete <c>&lt;param&gt;</c>,
///     <c>&lt;returns&gt;</c>, or <c>&lt;exception&gt;</c> coverage is explicitly out of scope and
///     noted as a future enhancement.
///     </para>
/// </remarks>
internal static class DocumentationCoverageChecker
{
    /// <summary>
    ///     Scans <paramref name="assembly"/> for types and members at or above
    ///     <paramref name="visibility"/> that lack a non-empty XML doc <c>&lt;summary&gt;</c>.
    /// </summary>
    /// <param name="assembly">The parsed assembly to scan. Must not be null.</param>
    /// <param name="xmlDocs">The XML documentation index used to look up summaries. Must not be null.</param>
    /// <param name="visibility">The enforcement visibility tier, independent of any emission visibility tier.</param>
    /// <param name="includeObsolete">
    ///     When <see langword="false"/> (the default enforcement behavior), types and members
    ///     carrying <see cref="ObsoleteAttribute"/> are skipped, mirroring the emission obsolete filter.
    /// </param>
    /// <param name="excludePatterns">
    ///     Wildcard patterns (matched via <see cref="DotNetGenerator.IsExcluded"/>) identifying
    ///     namespaces and types to skip entirely, mirroring the emission exclude-pattern filter.
    /// </param>
    /// <returns>
    ///     A <see cref="DocumentationCoverageResult"/> describing every undocumented item found
    ///     and the total number of items checked.
    /// </returns>
    internal static DocumentationCoverageResult Check(
        AssemblyDefinition assembly,
        XmlDocReader xmlDocs,
        ApiVisibility visibility,
        bool includeObsolete,
        IReadOnlyList<string> excludePatterns)
    {
        var compiledExcludePatterns = DotNetGenerator.CompileExcludePatterns(excludePatterns);
        var undocumented = new List<UndocumentedApiItem>();
        var checkedCount = 0;

        // Enumerate every top-level type that is not compiler-generated, not a NamespaceDoc
        // carrier, passes the enforcement visibility tier, the obsolete filter, and the
        // exclude-pattern filter — the same filter chain DotNetGenerator.Parse applies for
        // emission, but re-evaluated at the (possibly different) enforcement tier.
        foreach (var type in assembly.MainModule.Types
            .Where(t => !t.IsNested)
            .Where(t => !DotNetEmitter.IsCompilerGenerated(t))
            .Where(t => !DotNetEmitter.IsNamespaceDocCarrier(t))
            .Where(t => IsTypeVisible(t, visibility))
            .Where(t => includeObsolete || !DotNetEmitter.IsObsolete(t))
            .Where(t => !DotNetGenerator.IsExcluded(t, compiledExcludePatterns)))
        {
            CheckType(type, visibility, includeObsolete, xmlDocs, compiledExcludePatterns, undocumented, ref checkedCount);
        }

        return new DocumentationCoverageResult(undocumented, checkedCount);
    }

    /// <summary>
    ///     Checks a single type (and, recursively, its visible nested types) for a missing
    ///     summary, then checks all of its visible members.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="visibility">The enforcement visibility tier.</param>
    /// <param name="includeObsolete">Whether obsolete types/members are included in the check.</param>
    /// <param name="xmlDocs">The XML documentation index.</param>
    /// <param name="compiledExcludePatterns">The compiled exclude patterns to re-apply at every nesting level.</param>
    /// <param name="undocumented">The accumulator list to append violations to.</param>
    /// <param name="checkedCount">Running count of items checked, incremented by reference.</param>
    private static void CheckType(
        TypeDefinition type,
        ApiVisibility visibility,
        bool includeObsolete,
        XmlDocReader xmlDocs,
        IReadOnlyList<Regex> compiledExcludePatterns,
        List<UndocumentedApiItem> undocumented,
        ref int checkedCount)
    {
        checkedCount++;
        if (!HasDocumentation(type, DotNetEmitter.BuildTypeId(type), xmlDocs))
        {
            undocumented.Add(new UndocumentedApiItem("Type", type.FullName.Replace('/', '.')));
        }

        foreach (var member in GetVisibleMembers(type, visibility, includeObsolete))
        {
            checkedCount++;
            if (!HasDocumentation(member, DotNetEmitter.BuildMemberId(member), xmlDocs))
            {
                undocumented.Add(new UndocumentedApiItem(
                    ToKind(member),
                    $"{type.FullName.Replace('/', '.')}.{DotNetEmitter.GetMemberDisplayName(member)}"));
            }
        }

        // Recurse into nested types visible at the same enforcement tier — nested-type
        // visibility uses the IsNested* flags rather than the top-level IsPublic flag. The
        // exclude-pattern filter is re-applied here so a nested type matching an exclude
        // pattern is skipped just like a top-level type would be.
        foreach (var nested in type.NestedTypes
            .Where(t => !DotNetEmitter.IsCompilerGenerated(t))
            .Where(t => IsNestedTypeVisible(t, visibility))
            .Where(t => includeObsolete || !DotNetEmitter.IsObsolete(t))
            .Where(t => !DotNetGenerator.IsExcluded(t, compiledExcludePatterns)))
        {
            CheckType(nested, visibility, includeObsolete, xmlDocs, compiledExcludePatterns, undocumented, ref checkedCount);
        }
    }

    /// <summary>Enumerates the members of <paramref name="type"/> visible at <paramref name="visibility"/>.</summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="visibility">The enforcement visibility tier.</param>
    /// <param name="includeObsolete">Whether obsolete members are included.</param>
    /// <returns>An enumerable of visible member definitions, using the same member-kind shape as <see cref="DotNetEmitter.GetVisibleMembers"/>.</returns>
    private static IEnumerable<IMemberDefinition> GetVisibleMembers(TypeDefinition type, ApiVisibility visibility, bool includeObsolete)
    {
        bool IsVisible(IMemberDefinition member) =>
            IsMemberVisible(member, visibility) && (includeObsolete || !DotNetEmitter.IsObsolete(member));

        // Methods: exclude special-name accessors, compiler-generated methods, and the
        // compiler-synthesized implicit default constructor — it has no source line to attach a
        // <summary> to, and the C# compiler's own CS1591 missing-documentation warning does not
        // flag it either.
        foreach (var method in type.Methods
            .Where(m => !DotNetEmitter.IsSpecialNameNonConstructor(m) && !DotNetEmitter.IsCompilerGenerated(m) && !DotNetEmitter.IsImplicitDefaultConstructor(m) && IsVisible(m)))
        {
            yield return method;
        }

        // Properties: skip compiler-generated properties such as a record's synthesized
        // `EqualityContract`, mirroring the same filter DotNetEmitter.GetVisibleMembers applies
        // for emission, and consistent with methods already being filtered on IsCompilerGenerated
        // above.
        foreach (var prop in type.Properties.Where(p => !DotNetEmitter.IsCompilerGenerated(p) && IsVisible(p)))
        {
            yield return prop;
        }

        foreach (var field in type.Fields
            .Where(f => f.Name != "value__" && !DotNetEmitter.IsCompilerGeneratedField(f) && IsVisible(f)))
        {
            yield return field;
        }

        foreach (var evt in type.Events.Where(IsVisible))
        {
            yield return evt;
        }
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="type"/> satisfies <paramref name="visibility"/>.</summary>
    /// <param name="type">A non-nested type definition.</param>
    /// <param name="visibility">The enforcement visibility tier.</param>
    /// <returns><see langword="true"/> when the type should be checked.</returns>
    private static bool IsTypeVisible(TypeDefinition type, ApiVisibility visibility) => visibility switch
    {
        ApiVisibility.Public => type.IsPublic,
        ApiVisibility.PublicAndProtected => type.IsPublic,
        ApiVisibility.All => true,
        _ => type.IsPublic,
    };

    /// <summary>Returns <see langword="true"/> when nested type <paramref name="type"/> satisfies <paramref name="visibility"/>.</summary>
    /// <param name="type">A nested type definition.</param>
    /// <param name="visibility">The enforcement visibility tier.</param>
    /// <returns><see langword="true"/> when the nested type should be checked.</returns>
    private static bool IsNestedTypeVisible(TypeDefinition type, ApiVisibility visibility) => visibility switch
    {
        ApiVisibility.Public => type.IsNestedPublic,
        ApiVisibility.PublicAndProtected => type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamilyOrAssembly,
        ApiVisibility.All => true,
        _ => type.IsNestedPublic,
    };

    /// <summary>Returns <see langword="true"/> when <paramref name="member"/> satisfies <paramref name="visibility"/>.</summary>
    /// <param name="member">The member to test.</param>
    /// <param name="visibility">The enforcement visibility tier.</param>
    /// <returns><see langword="true"/> when the member should be checked.</returns>
    private static bool IsMemberVisible(IMemberDefinition member, ApiVisibility visibility) => visibility switch
    {
        ApiVisibility.Public => DotNetEmitter.IsMemberPublic(member),
        ApiVisibility.PublicAndProtected => DotNetEmitter.IsMemberPublicOrProtected(member),
        ApiVisibility.All => true,
        _ => DotNetEmitter.IsMemberPublic(member),
    };

    /// <summary>Maps a Mono.Cecil member definition to its display <c>Kind</c> label.</summary>
    /// <param name="member">The member to classify.</param>
    /// <returns>The corresponding kind label (e.g. <c>"Method"</c>, <c>"Property"</c>).</returns>
    private static string ToKind(IMemberDefinition member) => member switch
    {
        MethodDefinition => "Method",
        PropertyDefinition => "Property",
        FieldDefinition => "Field",
        EventDefinition => "Event",
        _ => "Method",
    };

    /// <summary>
    ///     Returns <see langword="true"/> when <paramref name="id"/> has a non-empty <c>&lt;summary&gt;</c>
    ///     in <paramref name="xmlDocs"/>, or — for a source-generator-produced <em>member</em> (one
    ///     carrying <c>GeneratedCodeAttribute</c>, e.g. a <c>[GeneratedRegex]</c> partial method) — a
    ///     non-empty <c>&lt;remarks&gt;</c> instead.
    /// </summary>
    /// <remarks>
    ///     The regex source generator attaches its own <c>&lt;remarks&gt;</c> (a plain-language
    ///     pattern explanation) to the compiler-generated partial-method implementation, which
    ///     silently replaces any hand-written <c>&lt;summary&gt;</c> from the partial method's
    ///     defining declaration in the compiled XML documentation — see
    ///     <see cref="DotNetEmitter.IsGeneratedCode(ICustomAttributeProvider)"/>. The <c>&lt;remarks&gt;</c>
    ///     fallback is scoped to <c>GeneratedCodeAttribute</c>-carrying <em>members</em> only — never
    ///     types, even when a type itself carries <c>GeneratedCodeAttribute</c> — so a hand-written
    ///     member, or any type, that merely lacks a <c>&lt;summary&gt;</c> in favor of a
    ///     <c>&lt;remarks&gt;</c> is still reported as undocumented.
    /// </remarks>
    /// <param name="provider">The type or member being checked, used to test for <c>GeneratedCodeAttribute</c>.</param>
    /// <param name="id">The XML doc member identifier to look up.</param>
    /// <param name="xmlDocs">The XML documentation index.</param>
    /// <returns><see langword="true"/> when the element is considered documented.</returns>
    private static bool HasDocumentation(ICustomAttributeProvider provider, string id, XmlDocReader xmlDocs)
    {
        if (!string.IsNullOrWhiteSpace(xmlDocs.GetSummary(id)))
        {
            return true;
        }

        return provider is not TypeDefinition
            && DotNetEmitter.IsGeneratedCode(provider)
            && !string.IsNullOrWhiteSpace(xmlDocs.GetRemarks(id));
    }
}
