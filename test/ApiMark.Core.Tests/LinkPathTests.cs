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

    /// <summary>Validates that every character that would break a Markdown link destination is encoded.</summary>
    /// <param name="raw">The raw destination.</param>
    /// <param name="expected">The expected encoded destination.</param>
    [Theory]
    [InlineData("a b", "a%20b")]
    [InlineData("a(b)", "a%28b%29")]
    [InlineData("a<b>", "a%3Cb%3E")]
    [InlineData("a#b", "a%23b")]
    [InlineData("a%b", "a%25b")]
    [InlineData("a?b", "a%3Fb")]
    [InlineData("A/B{2}/C.md", "A/B{2}/C.md")]
    [InlineData("../Foo.md", "../Foo.md")]
    public void LinkPath_Escape_LinkHostileCharacters_AreEncoded(string raw, string expected)
    {
        // Arrange / Act / Assert
        Assert.Equal(expected, LinkPath.Escape(raw));
    }

    /// <summary>
    ///     Validates the raw-input contract: a raw percent sign is encoded, so already encoded text is
    ///     encoded again (callers must pass raw registry names only), and the result decodes back to the input.
    /// </summary>
    [Fact]
    public void LinkPath_Escape_RawAndAlreadyEncodedInput_IsEncodedExactlyOncePerCall()
    {
        // Arrange
        const string raw = "dir/C#%20x.md";

        // Act
        var once = LinkPath.Escape(raw);
        var twice = LinkPath.Escape(once);

        // Assert
        Assert.Equal("dir/C%23%2520x.md", once);
        Assert.Equal(raw, Uri.UnescapeDataString(once));
        Assert.Equal("dir/C%2523%252520x.md", twice);
    }
}
