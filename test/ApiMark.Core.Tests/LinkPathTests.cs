using ApiMark.Core;
using Xunit;

namespace ApiMark.Core.Tests;

/// <summary>Verifies <see cref="LinkPath"/> computes relative link destinations with pure string arithmetic.</summary>
public class LinkPathTests
{
    /// <summary>Validates relative destinations between pages.</summary>
    /// <param name="from">The source directory.</param>
    /// <param name="target">The target path including extension.</param>
    /// <param name="expected">The expected relative destination.</param>
    [Theory]
    [InlineData("", "api.md", "api.md")]
    [InlineData("A", "api.md", "../api.md")]
    [InlineData("A/B", "api.md", "../../api.md")]
    [InlineData("A", "A/B.md", "B.md")]
    [InlineData("A/B", "A/B.md", "../B.md")]
    [InlineData("A/B", "A/C/D.md", "../C/D.md")]
    [InlineData("A", "A.md", "../A.md")]
    [InlineData("A/B", "A/B/C.md", "C.md")]
    public void LinkPath_Relative_Pages_ReturnsForwardSlashRelativePath(string from, string target, string expected)
    {
        // Arrange / Act / Assert
        Assert.Equal(expected, LinkPath.Relative(from, target));
    }

    /// <summary>Validates that characters that would break a Markdown link are encoded.</summary>
    [Fact]
    public void LinkPath_Escape_LinkHostileCharacters_AreEncoded()
    {
        // Arrange / Act
        var result = LinkPath.Escape("a b(c)<d>%20");

        // Assert
        Assert.Equal("a%20b%28c%29%3Cd%3E%20", result);
    }
}
