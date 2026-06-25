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

        // Assert: unambiguous short name produces a link with correct path; no external type tracked
        Assert.Contains("[Bar]", result, StringComparison.Ordinal);
        Assert.Contains("Bar.md", result, StringComparison.Ordinal);
        Assert.Empty(externalTypes);
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

    /// <summary>Validates that primitive types are returned unchanged and are not tracked as external types.</summary>
    [Fact]
    public void CppTypeLinkResolver_Linkify_PrimitiveType_ReturnsUnchanged()
    {
        // Arrange
        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal));
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Act
        var result = resolver.Linkify("int", string.Empty, externalTypes);

        // Assert
        Assert.Equal("int", result);
        Assert.Empty(externalTypes);
    }

    /// <summary>Validates that <c>std::</c> types are returned unchanged and are not tracked as external types.</summary>
    [Fact]
    public void CppTypeLinkResolver_Linkify_StdType_ReturnsUnchanged()
    {
        // Arrange
        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal));
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Act
        var result = resolver.Linkify("const std::string &", string.Empty, externalTypes);

        // Assert
        Assert.Equal("const std::string &", result);
        Assert.Empty(externalTypes);
    }

    /// <summary>Validates that a null type string is returned unchanged.</summary>
    [Fact]
    public void CppTypeLinkResolver_Linkify_NullInput_ReturnsNull()
    {
        // Arrange
        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal));
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Act
        var result = resolver.Linkify(null, string.Empty, externalTypes);

        // Assert
        Assert.Null(result);
        Assert.Empty(externalTypes);
    }

    /// <summary>Validates that a whitespace-only type string is returned unchanged.</summary>
    [Fact]
    public void CppTypeLinkResolver_Linkify_WhitespaceInput_ReturnsUnchanged()
    {
        // Arrange
        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal));
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Act
        var result = resolver.Linkify("   ", string.Empty, externalTypes);

        // Assert
        Assert.Equal("   ", result);
        Assert.Empty(externalTypes);
    }

    /// <summary>Validates that an external namespaced type is tracked for the External Types section.</summary>
    [Fact]
    public void CppTypeLinkResolver_Linkify_ExternalType_AddsToExternalTypesSet()
    {
        // Arrange
        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal));
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Act
        var result = resolver.Linkify("acme::Logger *", string.Empty, externalTypes);

        // Assert
        Assert.Equal("acme::Logger *", result);
        Assert.Single(externalTypes);
        Assert.Equal("Logger", externalTypes.First().TypeString);
        Assert.Equal("acme", externalTypes.First().Namespace);
    }

    /// <summary>Validates that leading qualifiers are removed repeatedly before lookup so qualified types still resolve.</summary>
    [Fact]
    public void CppTypeLinkResolver_Linkify_QualifiedType_StripsQualifiersBeforeLookup()
    {
        // Arrange
        var knownTypes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "fixtures::SampleClass", "fixtures/SampleClass" },
        };
        var resolver = new CppTypeLinkResolver(knownTypes);
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Act
        var result = resolver.Linkify("volatile const fixtures::SampleClass &", "fixtures", externalTypes);

        // Assert
        Assert.Contains("[SampleClass]", result, StringComparison.Ordinal);
        Assert.Contains("SampleClass.md", result, StringComparison.Ordinal);
        Assert.Empty(externalTypes);
    }

    /// <summary>
    ///     Validates that the template-argument prefix corruption prevention algorithm
    ///     links only the actual type token and leaves a sharing-prefix template argument unchanged.
    /// </summary>
    [Fact]
    public void CppTypeLinkResolver_Linkify_QualifiedTypeWithSameNamePrefixInTemplateArg_EmitsLinkWithoutCorruption()
    {
        // Arrange: Foo is a known intra-library type; FooBar is a template argument not in knownTypes
        var knownTypes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "ns::Foo", "ns/Foo" },
        };
        var resolver = new CppTypeLinkResolver(knownTypes);
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Act
        var result = resolver.Linkify("ns::Foo<FooBar>", "ns", externalTypes);

        // Assert: "Foo" is linked but "FooBar" is not wrapped in a link
        Assert.Contains("[Foo](", result, StringComparison.Ordinal);
        Assert.DoesNotContain("[FooBar]", result, StringComparison.Ordinal);
        Assert.EndsWith("<FooBar>", result, StringComparison.Ordinal);
    }

    /// <summary>Validates that the constructor throws when knownTypes is null.</summary>
    [Fact]
    public void CppTypeLinkResolver_Constructor_NullKnownTypes_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CppTypeLinkResolver(null!));
    }

    /// <summary>Validates that Linkify throws when externalTypes is null.</summary>
    [Fact]
    public void CppTypeLinkResolver_Linkify_NullExternalTypes_ThrowsArgumentNullException()
    {
        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal));
        Assert.Throws<ArgumentNullException>(() => resolver.Linkify("SomeType", string.Empty, null!));
    }
}
