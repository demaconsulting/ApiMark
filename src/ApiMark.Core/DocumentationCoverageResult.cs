namespace ApiMark.Core;

/// <summary>Describes a single declaration found to be missing a documentation summary.</summary>
/// <remarks>
///     Shared across every language-specific documentation-coverage checker
///     (<c>ApiMark.DotNet</c>, <c>ApiMark.Cpp</c>, <c>ApiMark.Vhdl</c>). <see cref="Kind"/> is a
///     plain, language-owned display label (e.g. <c>"Type"</c>, <c>"Function"</c>, <c>"Entity"</c>)
///     rather than a shared enum: each language has its own vocabulary of declaration kinds, and
///     the only consumer of <see cref="Kind"/> (<c>Program.cs</c>) interpolates it directly into a
///     display string without switching on it, so a combinatorial cross-language enum would add
///     maintenance cost without any corresponding benefit.
/// </remarks>
/// <param name="Kind">
///     The language-specific kind label of the undocumented declaration (e.g. <c>"Type"</c>,
///     <c>"Method"</c>, <c>"Entity"</c>, <c>"Port"</c>).
/// </param>
/// <param name="DisplayName">
///     The fully qualified or otherwise unambiguous display name of the declaration, in whatever
///     format is idiomatic for the owning language.
/// </param>
public sealed record UndocumentedApiItem(string Kind, string DisplayName);

/// <summary>The result of a language-specific documentation-coverage scan.</summary>
/// <remarks>
///     Returned by every implementation of <see cref="IDocumentationCoverageCapable"/>. Shared
///     across languages so that <c>ApiMark.Tool</c>'s reporting logic
///     (<c>Program.ReportDocumentationCoverage</c>) is entirely language-agnostic.
/// </remarks>
public sealed class DocumentationCoverageResult
{
    /// <summary>Initializes a new instance of <see cref="DocumentationCoverageResult"/>.</summary>
    /// <param name="undocumentedItems">The undocumented items found during the scan.</param>
    /// <param name="checkedCount">The total number of declarations checked.</param>
    public DocumentationCoverageResult(IReadOnlyList<UndocumentedApiItem> undocumentedItems, int checkedCount)
    {
        UndocumentedItems = undocumentedItems;
        CheckedCount = checkedCount;
    }

    /// <summary>Gets the list of declarations found to be missing a documentation summary.</summary>
    public IReadOnlyList<UndocumentedApiItem> UndocumentedItems { get; }

    /// <summary>Gets the total number of declarations checked, documented or not.</summary>
    public int CheckedCount { get; }

    /// <summary>Gets the number of undocumented items found.</summary>
    public int UndocumentedCount => UndocumentedItems.Count;

    /// <summary>Gets a value indicating whether any undocumented items were found.</summary>
    public bool HasViolations => UndocumentedCount > 0;
}
