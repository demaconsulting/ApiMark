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
        Assert.Contains("escapes base directory", exception.Message);
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
        Assert.Contains("escapes base directory", exception.Message);
    }

    /// <summary>
    ///     Verifies that rooted Unix-style paths are accepted when the combined result stays within the base.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_AbsoluteSegment_WithinBase_CombinesCorrectly()
    {
        // Arrange: a segment starting with the directory separator — Path.Join folds it within the base
        var basePath = Path.Combine("home", "user", "project");
        var segment = Path.DirectorySeparatorChar + "sub";

        // Act: the segment does not escape basePath so the call succeeds
        var result = PathHelpers.SafePathCombine(basePath, segment);

        // Assert: result is under the base path
        Assert.StartsWith(basePath, result);
    }

    /// <summary>
    ///     Verifies that backtracking within the base directory is allowed.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_BacktrackWithinBase_CombinesCorrectly()
    {
        // Arrange: segments that use ".." but stay within the base
        var basePath = Path.GetFullPath(Path.Combine("home", "user", "project"));

        // Act: "baa/.." resolves back to basePath — still within the base
        var result = PathHelpers.SafePathCombine(basePath, "baa", "..");

        // Assert: result resolves to the base path
        Assert.Equal(Path.GetFullPath(result), Path.GetFullPath(basePath));
    }

    /// <summary>
    ///     Verifies that a filename containing ".." as a substring is accepted.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_FilenameWithDoubleDots_CombinesCorrectly()
    {
        // Arrange: filename with ".." as part of the name, not a traversal segment
        var basePath = Path.Combine("home", "user", "project");
        const string FileName = "v1..2.md";

        // Act: the filename does not escape basePath
        var result = PathHelpers.SafePathCombine(basePath, FileName);

        // Assert: result equals the expected combined path
        Assert.Equal(Path.Join(basePath, FileName), result);
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
    ///     Verifies that traversal in any later params segment is rejected when it escapes the base.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_TraversalInLaterSegment_ThrowsArgumentException()
    {
        // Arrange: valid first segment, then enough ".." to escape the base
        var basePath = Path.Combine("home", "user", "project");

        // Act / Assert: traversal that escapes the base in any segment is rejected
        var exception = Assert.Throws<ArgumentException>(() =>
            PathHelpers.SafePathCombine(basePath, "level1", Path.Combine("..", "..", "..", "etc", "passwd")));
        Assert.Contains("escapes base directory", exception.Message);
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
