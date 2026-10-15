using ApiMark.Core;
using Xunit;

namespace ApiMark.Core.Tests;

/// <summary>Verifies <see cref="FileNameRules"/> escapes names exactly as the numbered naming rules state.</summary>
public class FileNameRulesTests
{
    /// <summary>Validates that a plain identifier is unchanged and reported as an identity name.</summary>
    /// <param name="name">The legal name.</param>
    [Theory]
    [InlineData("MyClass")]
    [InlineData("Foo-2")]
    [InlineData("Ns.Sub.Type")]
    [InlineData("caf\u00e9")]
    [InlineData(".ctor")]
    public void FileNameRules_Escape_LegalName_IsUnchangedAndNotTransformed(string name)
    {
        // Arrange / Act
        var result = FileNameRules.Escape(name, out var transformed);

        // Assert
        Assert.Equal(name, result);
        Assert.False(transformed);
    }

    /// <summary>Validates that structural braces are kept and mark the name as transformed.</summary>
    [Fact]
    public void FileNameRules_Escape_StructuralBraces_AreKeptAndTransformed()
    {
        // Arrange / Act
        var result = FileNameRules.Escape("IPair{int-string}.Get", out var transformed);

        // Assert
        Assert.Equal("IPair{int-string}.Get", result);
        Assert.True(transformed);
    }

    /// <summary>Validates that every illegal character becomes an underscore, without a hash.</summary>
    /// <param name="name">The raw name containing an illegal character.</param>
    /// <param name="expected">The expected escaped name.</param>
    [Theory]
    [InlineData("a:b", "a_b")]
    [InlineData("a\"b", "a_b")]
    [InlineData("a|b", "a_b")]
    [InlineData("a?b", "a_b")]
    [InlineData("a*b", "a_b")]
    [InlineData("a/b", "a_b")]
    [InlineData("a\\b", "a_b")]
    [InlineData("a\tb", "a_b")]
    [InlineData("a\u007Fb", "a_b")]
    [InlineData("int[]", "int__")]
    [InlineData("a b", "a_b")]
    [InlineData("a<b>", "a_b_")]
    [InlineData("a#b%c^d`e(f)", "a_b_c_d_e_f_")]
    public void FileNameRules_Escape_IllegalCharacter_BecomesUnderscore(string name, string expected)
    {
        // Arrange / Act
        var result = FileNameRules.Escape(name, out var transformed);

        // Assert
        Assert.Equal(expected, result);
        Assert.True(transformed);
        Assert.True(FileNameRules.IsLegalSegment(result));
    }

    /// <summary>Validates that a trailing dot is replaced.</summary>
    [Fact]
    public void FileNameRules_Escape_TrailingDot_IsReplaced()
    {
        // Arrange / Act
        var result = FileNameRules.Escape("Name.", out var transformed);

        // Assert
        Assert.Equal("Name_", result);
        Assert.True(transformed);
    }

    /// <summary>Validates that Windows reserved device names get an underscore suffix before the first dot.</summary>
    /// <param name="name">The reserved name.</param>
    /// <param name="expected">The expected escaped name.</param>
    [Theory]
    [InlineData("CON", "CON_")]
    [InlineData("aux", "aux_")]
    [InlineData("Com1", "Com1_")]
    [InlineData("LPT9", "LPT9_")]
    [InlineData("Con.Foo", "Con_.Foo")]
    [InlineData("COM\u00B2", "COM\u00B2_")]
    public void FileNameRules_Escape_ReservedDeviceName_GetsUnderscoreSuffix(string name, string expected)
    {
        // Arrange / Act
        var result = FileNameRules.Escape(name, out var transformed);

        // Assert
        Assert.Equal(expected, result);
        Assert.True(transformed);
        Assert.True(FileNameRules.IsLegalSegment(result));
    }

    /// <summary>Validates that an empty name becomes an underscore.</summary>
    [Fact]
    public void FileNameRules_Escape_EmptyName_ReturnsUnderscore()
    {
        // Arrange / Act
        var result = FileNameRules.Escape(string.Empty, out var transformed);

        // Assert
        Assert.Equal("_", result);
        Assert.True(transformed);
    }

    /// <summary>Validates that a name is normalized to Unicode form C.</summary>
    [Fact]
    public void FileNameRules_Escape_DecomposedName_IsNormalizedToFormC()
    {
        // Arrange
        var decomposed = "cafe\u0301";

        // Act
        var result = FileNameRules.Escape(decomposed, out var transformed);

        // Assert
        Assert.Equal("caf\u00e9", result);
        Assert.True(transformed);
    }

    /// <summary>Validates that an over-long name is truncated without splitting a surrogate pair.</summary>
    [Fact]
    public void FileNameRules_Escape_OverLongName_IsTruncated()
    {
        // Arrange
        var name = new string('a', FileNameRules.MaxNameLength - 1) + "\U0001F600" + "tail";

        // Act
        var result = FileNameRules.Escape(name, out var transformed);

        // Assert
        Assert.True(result.Length <= FileNameRules.MaxNameLength);
        Assert.Equal(new string('a', FileNameRules.MaxNameLength - 1), result);
        Assert.True(transformed);
    }

    /// <summary>Validates that a null name is rejected.</summary>
    [Fact]
    public void FileNameRules_Escape_NullName_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => FileNameRules.Escape(null!, out _));
    }

    /// <summary>Validates that illegal names are reported as illegal.</summary>
    /// <param name="name">The illegal name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a<b")]
    [InlineData("a:b")]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    [InlineData("a\tb")]
    [InlineData("a b")]
    [InlineData("a[b")]
    [InlineData("a]b")]
    [InlineData("a(b")]
    [InlineData("a)b")]
    [InlineData("a#b")]
    [InlineData("a%b")]
    [InlineData("a^b")]
    [InlineData("a`b")]
    [InlineData("a>b")]
    [InlineData("a|b")]
    [InlineData("a?b")]
    [InlineData("a*b")]
    [InlineData("a\"b")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a\u007Fb")]
    public void FileNameRules_IsLegalSegment_IllegalName_ReturnsFalse(string? name)
    {
        // Arrange / Act / Assert
        Assert.False(FileNameRules.IsLegalSegment(name));
    }

    /// <summary>
    ///     Validates that <see cref="FileNameRules.IsLegalSegment"/> and <see cref="FileNameRules.Escape"/>
    ///     share one character set: every character Escape replaces is illegal, and Escape output is legal.
    /// </summary>
    [Fact]
    public void FileNameRules_IsLegalSegment_EveryEscapedCharacter_IsIllegalAndEscapeOutputIsLegal()
    {
        for (var code = 0; code < 0x80; code++)
        {
            var c = (char)code;
            var name = "a" + c + "b";
            var escaped = FileNameRules.Escape(name, out _);

            Assert.True(FileNameRules.IsLegalSegment(escaped), $"Escape output for U+{code:X4} is not legal");
            if (c is not '{' and not '}' && escaped != name)
            {
                Assert.False(FileNameRules.IsLegalSegment(name), $"U+{code:X4} is replaced by Escape but legal");
            }
        }
    }

    /// <summary>Validates that legal names are reported as legal.</summary>
    /// <param name="name">The legal name.</param>
    [Theory]
    [InlineData("Foo")]
    [InlineData("Foo{2}")]
    [InlineData("IFoo{int}.Get")]
    [InlineData("CON_")]
    [InlineData("Console")]
    public void FileNameRules_IsLegalSegment_LegalName_ReturnsTrue(string name)
    {
        // Arrange / Act / Assert
        Assert.True(FileNameRules.IsLegalSegment(name));
    }
}
