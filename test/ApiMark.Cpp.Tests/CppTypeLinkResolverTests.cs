using System.Collections.Generic;
using Xunit;

namespace ApiMark.Cpp.Tests;

/// <summary>Unit tests for <see cref="CppTypeLinkResolver"/>.</summary>
public class CppTypeLinkResolverTests
{
    /// <summary>
    ///     Validates that an exact qualified-name match in <c>knownTypes</c> produces a Markdown link.
    /// </summary>
    [Fact]
    public void CppTypeLinkResolver_Linkify_ExactQualifiedMatch_EmitsLink()
    {
        // Arrange
        var knownTypes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "ns::Foo", "ns/Foo" },
        };
        var resolver = new CppTypeLinkResolver(knownTypes);
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Act
        var result = resolver.Linkify("ns::Foo", "ns", externalTypes);

        // Assert: exact qualified match produces a link
        Assert.Contains("[Foo]", result, StringComparison.Ordinal);
        Assert.Contains("Foo.md", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an unqualified short-name reference that matches exactly one known type
    ///     produces a Markdown link via the short-name fallback.
    /// </summary>
    [Fact]
    public void CppTypeLinkResolver_Linkify_UnambiguousShortName_EmitsLink()
    {
        // Arrange: only one type has the unqualified name "Bar"
        var knownTypes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "ns::Bar", "ns/Bar" },
        };
        var resolver = new CppTypeLinkResolver(knownTypes);
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Act
        var result = resolver.Linkify("Bar", "ns", externalTypes);

        // Assert: unambiguous short name produces a link
        Assert.Contains("[Bar]", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that when two documented types share the same unqualified name (e.g.
    ///     <c>Outer::size_type</c> and <c>Other::size_type</c>), an unqualified reference to
    ///     <c>size_type</c> does NOT produce a link — the short-name fallback must not return
    ///     a non-deterministic result.
    /// </summary>
    [Fact]
    public void CppTypeLinkResolver_Linkify_AmbiguousShortName_EmitsPlainText()
    {
        // Arrange: two types share the unqualified name "size_type"
        var knownTypes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "ns::Outer::size_type", "ns/Outer/size_type" },
            { "ns::Other::size_type", "ns/Other/size_type" },
        };
        var resolver = new CppTypeLinkResolver(knownTypes);
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Act
        var result = resolver.Linkify("size_type", "ns", externalTypes);

        // Assert: ambiguous short name must not produce a link
        Assert.DoesNotContain("[size_type]", result, StringComparison.Ordinal);
        Assert.Equal("size_type", result);
    }

    /// <summary>
    ///     Validates that a fully-qualified reference to one of two ambiguously-named types
    ///     still resolves correctly via the exact-match path.
    /// </summary>
    [Fact]
    public void CppTypeLinkResolver_Linkify_QualifiedReferenceToAmbiguousType_EmitsCorrectLink()
    {
        // Arrange: two types share the unqualified name "size_type"
        var knownTypes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "ns::Outer::size_type", "ns/Outer/size_type" },
            { "ns::Other::size_type", "ns/Other/size_type" },
        };
        var resolver = new CppTypeLinkResolver(knownTypes);
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Act: use the fully-qualified name to disambiguate
        var result = resolver.Linkify("ns::Outer::size_type", "ns", externalTypes);

        // Assert: qualified reference resolves to the correct page
        Assert.Contains("[size_type]", result, StringComparison.Ordinal);
        Assert.Contains("Outer/size_type.md", result, StringComparison.Ordinal);
    }
}
