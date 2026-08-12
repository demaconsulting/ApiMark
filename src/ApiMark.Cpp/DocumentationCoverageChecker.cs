using ApiMark.Core;
using ApiMark.Cpp.CppAst;

namespace ApiMark.Cpp;

/// <summary>
///     Scans the namespace declarations parsed by <see cref="CppGenerator"/> for classes,
///     functions, fields, enums, and type aliases that lack a Doxygen <c>@brief</c>/summary, at a
///     caller-supplied visibility tier that is independent of the tier used for Markdown emission.
/// </summary>
/// <remarks>
///     Mirrors <c>ApiMark.DotNet.DocumentationCoverageChecker</c> as closely as the C++ AST model
///     allows. Two notable differences from the .NET checker, both documented here and in the
///     C++ design documentation:
///     <list type="bullet">
///       <item>
///         <b>No exclude-pattern support.</b> <see cref="CppGeneratorOptions"/> has no
///         namespace/type exclude-pattern option analogous to
///         <c>DotNetGeneratorOptions.ExcludePatterns</c> — only file-selection glob patterns
///         (<see cref="CppGeneratorOptions.ApiHeaderPatterns"/>). Adding such a feature is out of
///         scope for documentation-coverage enforcement alone, so this checker has no exclude
///         parameter at all.
///       </item>
///       <item>
///         <b>Nested classes need no visibility gate of their own.</b>
///         <see cref="CppClass.NestedClasses"/> is already pre-filtered to public nested
///         classes/structs only by <see cref="ClangAstParser"/> — unlike .NET, where nested-type
///         visibility must be re-evaluated with <c>IsNestedTypeVisible</c>. This checker therefore
///         recurses into every nested class unconditionally, applying the visibility tier only to
///         each nested class's own members.
///       </item>
///     </list>
///     The v1 bar for "documented" mirrors .NET: a declaration is considered documented when its
///     <see cref="CppDocComment.Summary"/> is a non-null, non-whitespace string. Checking for
///     complete <c>@param</c>/<c>@return</c> coverage is out of scope and noted as a future
///     enhancement, exactly as for .NET.
/// </remarks>
internal static class DocumentationCoverageChecker
{
    /// <summary>
    ///     Scans <paramref name="namespaceDecls"/> for classes, functions, fields, enums, enum
    ///     values, and type aliases at or above <paramref name="visibility"/> that lack a
    ///     non-empty Doxygen summary.
    /// </summary>
    /// <param name="namespaceDecls">The namespace declarations collected by <see cref="CppGenerator.Parse"/>.</param>
    /// <param name="visibility">The enforcement visibility tier, independent of any emission visibility tier.</param>
    /// <param name="includeDeprecated">
    ///     When <see langword="false"/> (the default enforcement behavior), declarations carrying
    ///     <c>[[deprecated]]</c> are skipped, mirroring the emission deprecated filter.
    /// </param>
    /// <returns>
    ///     A <see cref="DocumentationCoverageResult"/> describing every undocumented declaration
    ///     found and the total number of declarations checked.
    /// </returns>
    internal static DocumentationCoverageResult Check(
        SortedDictionary<string, CppEmitter.NamespaceDeclarations> namespaceDecls,
        ApiVisibility visibility,
        bool includeDeprecated)
    {
        var undocumented = new List<UndocumentedApiItem>();
        var checkedCount = 0;

        foreach (var ns in namespaceDecls.Values)
        {
            foreach (var cls in ns.Classes.Where(c => includeDeprecated || !c.IsDeprecated))
            {
                CheckClass(cls, ns.DisplayName, visibility, includeDeprecated, undocumented, ref checkedCount);
            }

            foreach (var fn in ns.FreeFunctions.Where(f => includeDeprecated || !f.IsDeprecated))
            {
                CheckFunction(fn, ns.DisplayName, undocumented, ref checkedCount);
            }

            foreach (var en in ns.Enums.Where(e => includeDeprecated || !e.IsDeprecated))
            {
                CheckEnum(en, ns.DisplayName, undocumented, ref checkedCount);
            }

            foreach (var alias in ns.TypeAliases.Where(a => includeDeprecated || !a.IsDeprecated))
            {
                CheckTypeAlias(alias, ns.DisplayName, undocumented, ref checkedCount);
            }
        }

        return new DocumentationCoverageResult(undocumented, checkedCount);
    }

    /// <summary>
    ///     Checks a single class for a missing summary, then checks its visible members, fields,
    ///     and (unconditionally, since they are already pre-filtered to public) nested classes.
    /// </summary>
    /// <param name="cls">The class to check.</param>
    /// <param name="scopeDisplay">The fully-qualified display name of the enclosing scope.</param>
    /// <param name="visibility">The enforcement visibility tier.</param>
    /// <param name="includeDeprecated">Whether deprecated declarations are included in the check.</param>
    /// <param name="undocumented">The accumulator list to append violations to.</param>
    /// <param name="checkedCount">Running count of declarations checked, incremented by reference.</param>
    private static void CheckClass(
        CppClass cls,
        string scopeDisplay,
        ApiVisibility visibility,
        bool includeDeprecated,
        List<UndocumentedApiItem> undocumented,
        ref int checkedCount)
    {
        var classDisplay = $"{scopeDisplay}::{cls.Name}";

        checkedCount++;
        if (string.IsNullOrWhiteSpace(cls.Doc?.Summary))
        {
            undocumented.Add(new UndocumentedApiItem("Class", classDisplay));
        }

        foreach (var member in cls.Members
            .Where(m => IsVisibleMember(m.Accessibility, visibility))
            .Where(m => includeDeprecated || !m.IsDeprecated))
        {
            checkedCount++;
            if (string.IsNullOrWhiteSpace(member.Doc?.Summary))
            {
                var name = member.IsConstructor ? cls.Name : member.Name;
                undocumented.Add(new UndocumentedApiItem("Function", $"{classDisplay}::{name}{FormatParameterSignature(member)}"));
            }
        }

        foreach (var field in cls.Fields
            .Where(f => IsVisibleMember(f.Accessibility, visibility))
            .Where(f => includeDeprecated || !f.IsDeprecated))
        {
            checkedCount++;
            if (string.IsNullOrWhiteSpace(field.Doc?.Summary))
            {
                undocumented.Add(new UndocumentedApiItem("Field", $"{classDisplay}::{field.Name}"));
            }
        }

        foreach (var alias in cls.TypeAliases.Where(a => includeDeprecated || !a.IsDeprecated))
        {
            CheckTypeAlias(alias, classDisplay, undocumented, ref checkedCount);
        }

        // Nested classes are already pre-filtered to public-only by ClangAstParser, so no
        // additional visibility gate is needed here — only their own members are tier-filtered.
        foreach (var nested in cls.NestedClasses.Where(n => includeDeprecated || !n.IsDeprecated))
        {
            CheckClass(nested, classDisplay, visibility, includeDeprecated, undocumented, ref checkedCount);
        }
    }

    /// <summary>Checks a single free function for a missing summary.</summary>
    /// <param name="fn">The free function to check.</param>
    /// <param name="scopeDisplay">The fully-qualified display name of the enclosing namespace.</param>
    /// <param name="undocumented">The accumulator list to append violations to.</param>
    /// <param name="checkedCount">Running count of declarations checked, incremented by reference.</param>
    private static void CheckFunction(
        CppFunction fn,
        string scopeDisplay,
        List<UndocumentedApiItem> undocumented,
        ref int checkedCount)
    {
        checkedCount++;
        if (string.IsNullOrWhiteSpace(fn.Doc?.Summary))
        {
            undocumented.Add(new UndocumentedApiItem("Function", $"{scopeDisplay}::{fn.Name}{FormatParameterSignature(fn)}"));
        }
    }

    /// <summary>Checks a single enum, and each of its enumerator values, for a missing summary.</summary>
    /// <param name="en">The enum to check.</param>
    /// <param name="scopeDisplay">The fully-qualified display name of the enclosing namespace.</param>
    /// <param name="undocumented">The accumulator list to append violations to.</param>
    /// <param name="checkedCount">Running count of declarations checked, incremented by reference.</param>
    private static void CheckEnum(
        CppEnum en,
        string scopeDisplay,
        List<UndocumentedApiItem> undocumented,
        ref int checkedCount)
    {
        var enumDisplay = $"{scopeDisplay}::{en.Name}";

        checkedCount++;
        if (string.IsNullOrWhiteSpace(en.Doc?.Summary))
        {
            undocumented.Add(new UndocumentedApiItem("Enum", enumDisplay));
        }

        foreach (var value in en.Values)
        {
            checkedCount++;
            if (string.IsNullOrWhiteSpace(value.Doc?.Summary))
            {
                undocumented.Add(new UndocumentedApiItem("EnumValue", $"{enumDisplay}::{value.Name}"));
            }
        }
    }

    /// <summary>Checks a single type alias for a missing summary.</summary>
    /// <param name="alias">The type alias to check.</param>
    /// <param name="scopeDisplay">The fully-qualified display name of the enclosing scope.</param>
    /// <param name="undocumented">The accumulator list to append violations to.</param>
    /// <param name="checkedCount">Running count of declarations checked, incremented by reference.</param>
    private static void CheckTypeAlias(
        CppTypeAlias alias,
        string scopeDisplay,
        List<UndocumentedApiItem> undocumented,
        ref int checkedCount)
    {
        checkedCount++;
        if (string.IsNullOrWhiteSpace(alias.Doc?.Summary))
        {
            undocumented.Add(new UndocumentedApiItem("TypeAlias", $"{scopeDisplay}::{alias.Name}"));
        }
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="accessibility"/> satisfies <paramref name="visibility"/>.</summary>
    /// <param name="accessibility">The accessibility of the member.</param>
    /// <param name="visibility">The enforcement visibility tier.</param>
    /// <returns><see langword="true"/> when the member should be checked.</returns>
    private static bool IsVisibleMember(CppAccessibility accessibility, ApiVisibility visibility) => visibility switch
    {
        ApiVisibility.Public => accessibility == CppAccessibility.Public,
        ApiVisibility.PublicAndProtected => accessibility is CppAccessibility.Public or CppAccessibility.Protected,
        ApiVisibility.All => true,
        _ => accessibility == CppAccessibility.Public,
    };

    /// <summary>
    ///     Formats a parenthesized, comma-separated parameter-type signature for
    ///     <paramref name="fn"/>, so that distinct overloads of the same name are reported as
    ///     unambiguous, distinguishable entries rather than collapsing to one identical
    ///     <see cref="UndocumentedApiItem.DisplayName"/>.
    /// </summary>
    /// <param name="fn">The function, method, or constructor to format a signature for.</param>
    /// <returns>
    ///     A string of the form <c>"(int, const std::string&amp;)"</c>, or <c>"()"</c> when
    ///     <paramref name="fn"/> declares no parameters.
    /// </returns>
    private static string FormatParameterSignature(CppFunction fn) =>
        $"({string.Join(", ", fn.Parameters.Select(p => p.TypeName))})";
}
