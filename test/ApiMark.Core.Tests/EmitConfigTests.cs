using ApiMark.Core;
using Xunit;

namespace ApiMark.Core.Tests;

/// <summary>
///     Verifies the <see cref="EmitConfig"/> value object, confirming that default
///     property values are correct and that validation rejects out-of-range inputs.
/// </summary>
public sealed class EmitConfigTests
{
    /// <summary>
    ///     Verifies that setting <see cref="EmitConfig.HeadingDepth"/> to zero throws
    ///     <see cref="ArgumentOutOfRangeException"/> because zero is below the minimum
    ///     valid depth of 1.
    /// </summary>
    [Fact]
    public void EmitConfig_HeadingDepth_BelowMinimum_ThrowsArgumentOutOfRangeException()
    {
        // Arrange / Act / Assert: depth 0 is below the valid range and must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new EmitConfig { HeadingDepth = 0 });
    }

    /// <summary>
    ///     Verifies that setting <see cref="EmitConfig.HeadingDepth"/> to 4 throws
    ///     <see cref="ArgumentOutOfRangeException"/> because 4 is above the maximum valid
    ///     depth of 3 (which would produce an H7 member heading, unsupported by Markdown).
    /// </summary>
    [Fact]
    public void EmitConfig_HeadingDepth_AboveMaximum_ThrowsArgumentOutOfRangeException()
    {
        // Arrange / Act / Assert: depth 4 is above the valid range and must be rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new EmitConfig { HeadingDepth = 4 });
    }

    /// <summary>
    ///     Verifies that setting <see cref="EmitConfig.HeadingDepth"/> to 2 succeeds
    ///     and the property reflects the supplied value.
    /// </summary>
    [Fact]
    public void EmitConfig_HeadingDepth_ValidNonDefault_SetsCorrectly()
    {
        // Arrange / Act: construct EmitConfig with a non-default HeadingDepth
        var config = new EmitConfig { HeadingDepth = 2 };

        // Assert: the property must reflect the supplied value
        Assert.Equal(2, config.HeadingDepth);
    }

    /// <summary>
    ///     Verifies that <see cref="EmitConfig.Format"/> defaults to
    ///     <see cref="OutputFormat.GradualDisclosure"/> when no explicit value is supplied.
    /// </summary>
    [Fact]
    public void EmitConfig_DefaultFormat_IsGradualDisclosure()
    {
        // Arrange / Act: construct EmitConfig with no explicit Format
        var config = new EmitConfig();

        // Assert: the default format must be GradualDisclosure
        Assert.Equal(OutputFormat.GradualDisclosure, config.Format);
    }

    /// <summary>
    ///     Verifies that <see cref="EmitConfig.HeadingDepth"/> defaults to <c>1</c>
    ///     when no explicit value is supplied.
    /// </summary>
    [Fact]
    public void EmitConfig_DefaultHeadingDepth_IsOne()
    {
        // Arrange / Act: construct EmitConfig with no explicit HeadingDepth
        var config = new EmitConfig();

        // Assert: the default heading depth must be 1
        Assert.Equal(1, config.HeadingDepth);
    }
}
