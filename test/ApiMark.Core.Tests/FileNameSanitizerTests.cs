using ApiMark.Core;
using Xunit;

namespace ApiMark.Core.Tests;

/// <summary>Verifies <see cref="FileNameSanitizer"/> produces legal, deterministic, distinct names.</summary>
public class FileNameSanitizerTests
{
    /// <summary>Validates that angle brackets map to braces without a hash suffix.</summary>
    [Fact]
    public void FileNameSanitizer_SanitizeSegment_AngleBrackets_BecomeBraces()
    {
        // Arrange / Act
        var result = FileNameSanitizer.SanitizeSegment("IFoo<System.Int32>.Method");

        // Assert
        Assert.Equal("IFoo{System.Int32}.Method", result);
    }

    /// <summary>Validates that ordinary names are returned unchanged.</summary>
    /// <param name="name">The legal name.</param>
    [Theory]
    [InlineData("MyClass")]
    [InlineData("Foo-2")]
    [InlineData("Ns.Sub.Type")]
    [InlineData("caf\u00e9")]
    public void FileNameSanitizer_SanitizeSegment_LegalName_IsUnchanged(string name)
    {
        // Arrange / Act / Assert
        Assert.Equal(name, FileNameSanitizer.SanitizeSegment(name));
    }

    /// <summary>Validates that every illegal character is replaced and a hash suffix is appended.</summary>
    /// <param name="name">The raw name containing an illegal character.</param>
    [Theory]
    [InlineData("a:b")]
    [InlineData("a\"b")]
    [InlineData("a|b")]
    [InlineData("a?b")]
    [InlineData("a*b")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a\tb")]
    [InlineData("a\u007Fb")]
    [InlineData("int[]")]
    [InlineData("a b")]
    public void FileNameSanitizer_SanitizeSegment_IllegalCharacter_IsReplacedWithHashSuffix(string name)
    {
        // Arrange / Act
        var result = FileNameSanitizer.SanitizeSegment(name);

        // Assert
        Assert.True(FileNameSanitizer.IsLegalSegment(result), result);
        Assert.EndsWith(FileNameSanitizer.Hash(name), result, StringComparison.Ordinal);
    }

    /// <summary>Validates that a trailing dot is not preserved.</summary>
    [Fact]
    public void FileNameSanitizer_SanitizeSegment_TrailingDot_IsLegal()
    {
        // Arrange / Act
        var result = FileNameSanitizer.SanitizeSegment("name.");

        // Assert
        Assert.True(FileNameSanitizer.IsLegalSegment(result), result);
    }

    /// <summary>Validates that reserved device names are moved aside, with or without an extension part.</summary>
    /// <param name="name">The reserved name.</param>
    /// <param name="expected">The expected sanitized name.</param>
    [Theory]
    [InlineData("CON", "CON-")]
    [InlineData("con", "con-")]
    [InlineData("Aux", "Aux-")]
    [InlineData("NUL", "NUL-")]
    [InlineData("PRN", "PRN-")]
    [InlineData("COM1", "COM1-")]
    [InlineData("lpt9", "lpt9-")]
    [InlineData("Con.Foo", "Con-.Foo")]
    [InlineData("CON1", "CON1")]
    [InlineData("Console", "Console")]
    public void FileNameSanitizer_SanitizeSegment_ReservedDeviceName_IsMovedAside(string name, string expected)
    {
        // Arrange / Act / Assert
        Assert.Equal(expected, FileNameSanitizer.SanitizeSegment(name));
    }

    /// <summary>Validates that an empty name still yields a legal segment.</summary>
    [Fact]
    public void FileNameSanitizer_SanitizeSegment_EmptyName_ReturnsUnderscore()
    {
        // Arrange / Act / Assert
        Assert.Equal("_", FileNameSanitizer.SanitizeSegment(string.Empty));
    }

    /// <summary>Validates that a null name is rejected.</summary>
    [Fact]
    public void FileNameSanitizer_SanitizeSegment_NullName_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => FileNameSanitizer.SanitizeSegment(null!));
    }

    /// <summary>Validates that distinct lossy inputs stay distinct and the mapping is idempotent.</summary>
    [Fact]
    public void FileNameSanitizer_SanitizeSegment_DistinctLossyInputs_StayDistinctAndIdempotent()
    {
        // Arrange
        string[] inputs = ["a:b", "a|b", "a?b", "a_b", "A<B>", "CON", "x.", "x-"];

        // Act
        var results = inputs.Select(FileNameSanitizer.SanitizeSegment).ToList();

        // Assert
        Assert.Equal(inputs.Length, results.Distinct(StringComparer.Ordinal).Count());
        Assert.All(results, r => Assert.Equal(r, FileNameSanitizer.SanitizeSegment(r)));
        Assert.Equal(FileNameSanitizer.SanitizeSegment("a:b"), FileNameSanitizer.SanitizeSegment("a:b"));
        Assert.NotEqual(FileNameSanitizer.SanitizeSegment("a:b"), FileNameSanitizer.SanitizeSegment("a|b"));
    }

    /// <summary>Validates the legality predicate rejects each illegal shape.</summary>
    /// <param name="name">The illegal name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("a:b")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a\"b")]
    [InlineData("a|b")]
    [InlineData("a?b")]
    [InlineData("a*b")]
    [InlineData("a\u0001b")]
    [InlineData("a.")]
    [InlineData("a ")]
    [InlineData("CON")]
    [InlineData("nul.txt")]
    [InlineData("COM\u00B9")]
    public void FileNameSanitizer_IsLegalSegment_IllegalName_ReturnsFalse(string? name)
    {
        // Arrange / Act / Assert
        Assert.False(FileNameSanitizer.IsLegalSegment(name));
    }

    /// <summary>Validates the legality predicate accepts legal names, including braces and hyphens.</summary>
    /// <param name="name">The legal name.</param>
    [Theory]
    [InlineData("api")]
    [InlineData("IFoo{T}.Method")]
    [InlineData("CON-")]
    [InlineData("Foo-1")]
    public void FileNameSanitizer_IsLegalSegment_LegalName_ReturnsTrue(string name)
    {
        // Arrange / Act / Assert
        Assert.True(FileNameSanitizer.IsLegalSegment(name));
    }
}
