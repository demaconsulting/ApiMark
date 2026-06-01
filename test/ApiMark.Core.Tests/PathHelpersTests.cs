using ApiMark.Core;
using Xunit;

namespace ApiMark.Core.Tests;

/// <summary>
///     Verifies path-safety enforcement for <see cref="PathHelpers"/>.
/// </summary>
public sealed class PathHelpersTests
{
    /// <summary>
    ///     Verifies that valid relative segments are combined without altering the base path.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_ValidPaths_CombinesCorrectly()
    {
        // Arrange: create a base path and a valid relative path
        var basePath = Path.Combine("home", "user", "project");
        var relativePath = Path.Combine("subfolder", "file.txt");

        // Act: invoke SafePathCombine with the test inputs
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: result equals Path.Combine output
        Assert.Equal(Path.Combine(basePath, relativePath), result);
    }

    /// <summary>
    ///     Verifies that traversal using a leading parent-directory segment is rejected.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_PathTraversalWithDoubleDots_ThrowsArgumentException()
    {
        // Arrange: relative path with parent-directory traversal segment
        var basePath = Path.Combine("home", "user", "project");
        var relativePath = Path.Combine("..", "etc", "passwd");

        // Act / Assert: path traversal attempt is rejected
        var exception = Assert.Throws<ArgumentException>(() =>
            PathHelpers.SafePathCombine(basePath, relativePath));
        Assert.Contains("Invalid path component", exception.Message);
    }

    /// <summary>
    ///     Verifies that traversal embedded later in a path is rejected.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_DoubleDotsInMiddle_ThrowsArgumentException()
    {
        // Arrange: relative path with embedded traversal segment
        var basePath = Path.Combine("home", "user", "project");
        var relativePath = Path.Combine("subfolder", "..", "..", "..", "etc", "passwd");

        // Act / Assert: embedded traversal is rejected
        var exception = Assert.Throws<ArgumentException>(() =>
            PathHelpers.SafePathCombine(basePath, relativePath));
        Assert.Contains("Invalid path component", exception.Message);
    }

    /// <summary>
    ///     Verifies that rooted Unix-style paths are rejected as relative segments.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_AbsolutePath_ThrowsArgumentException()
    {
        // Arrange: rooted Unix-style path used as the relative argument
        var basePath = Path.Combine("home", "user", "project");
        var relativePath = Path.DirectorySeparatorChar == '\\' ? "\\etc\\passwd" : "/etc/passwd";

        // Act / Assert: rooted path is rejected
        var exception = Assert.Throws<ArgumentException>(() =>
            PathHelpers.SafePathCombine(basePath, relativePath));
        Assert.Contains("Invalid path component", exception.Message);
    }

    /// <summary>
    ///     Verifies that Windows drive-letter paths are rejected on Windows.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_WindowsAbsolutePath_ThrowsArgumentException()
    {
        // Arrange: Windows drive-letter paths are only rooted on Windows
        if (!OperatingSystem.IsWindows())
        {
            throw Xunit.Sdk.SkipException.ForSkip("Windows absolute-path guard only applies on Windows.");
        }

        var basePath = @"C:\Users\project";
        var relativePath = @"C:\Windows\System32\file.txt";

        // Act / Assert: Windows absolute path is rejected
        var exception = Assert.Throws<ArgumentException>(() =>
            PathHelpers.SafePathCombine(basePath, relativePath));
        Assert.Contains("Invalid path component", exception.Message);
    }

    /// <summary>
    ///     Verifies that current-directory references remain inside the base path.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_CurrentDirectoryReference_CombinesCorrectly()
    {
        // Arrange: relative path starting with a current-directory reference
        var basePath = Path.Combine("home", "user", "project");
        var relativePath = Path.Combine(".", "subfolder", "file.txt");

        // Act: invoke SafePathCombine with the test inputs
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: result equals Path.Combine output
        Assert.Equal(Path.Combine(basePath, relativePath), result);
    }

    /// <summary>
    ///     Verifies that deeply nested relative paths are combined correctly.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_NestedPaths_CombinesCorrectly()
    {
        // Arrange: deeply nested relative path
        var basePath = Path.Combine("home", "user", "project");
        var relativePath = Path.Combine("level1", "level2", "level3", "file.txt");

        // Act: invoke SafePathCombine with the test inputs
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: result equals Path.Combine output
        Assert.Equal(Path.Combine(basePath, relativePath), result);
    }

    /// <summary>
    ///     Verifies that an empty relative path leaves the base path unchanged.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_EmptyRelativePath_ReturnsBasePath()
    {
        // Arrange: empty relative path
        var basePath = Path.Combine("home", "user", "project");
        const string RelativePath = "";

        // Act: invoke SafePathCombine with the test inputs
        var result = PathHelpers.SafePathCombine(basePath, RelativePath);

        // Assert: result equals Path.Combine output
        Assert.Equal(Path.Combine(basePath, RelativePath), result);
    }

    /// <summary>
    ///     Verifies that multiple valid segments are validated and appended in order.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_MultipleSegments_CombinesCorrectly()
    {
        // Arrange: base path and multiple valid relative segments
        var basePath = Path.Combine("home", "user", "project");

        // Act: invoke SafePathCombine with multiple segments
        var result = PathHelpers.SafePathCombine(basePath, "level1", "level2", "file.txt");

        // Assert: result equals Path.Join output for the same segments
        Assert.Equal(Path.Join(basePath, "level1", "level2", "file.txt"), result);
    }

    /// <summary>
    ///     Verifies that traversal in any later params segment is rejected.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_TraversalInLaterSegment_ThrowsArgumentException()
    {
        // Arrange: valid first segment, traversal in second
        var basePath = Path.Combine("home", "user", "project");

        // Act / Assert: traversal in any segment is rejected
        var exception = Assert.Throws<ArgumentException>(() =>
            PathHelpers.SafePathCombine(basePath, "level1", Path.Combine("..", "etc", "passwd")));
        Assert.Contains("Invalid path component", exception.Message);
    }

    /// <summary>
    ///     Verifies that null base paths are rejected before any path operation is attempted.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_NullBasePath_ThrowsArgumentNullException()
    {
        // Arrange: create a null base path
        string? basePath = null;

        // Act / Assert: null basePath is rejected before any path operation
        Assert.Throws<ArgumentNullException>(() =>
            PathHelpers.SafePathCombine(basePath!, "relative", "path"));
    }

    /// <summary>
    ///     Verifies that null relative segments are rejected before path combination.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_NullRelativePath_ThrowsArgumentNullException()
    {
        // Arrange: create a null relative path segment
        var basePath = Path.Combine("home", "user", "project");
        string? relativePath = null;

        // Act / Assert: null relativePath is rejected before any path operation
        Assert.Throws<ArgumentNullException>(() =>
            PathHelpers.SafePathCombine(basePath, relativePath!));
    }
}
