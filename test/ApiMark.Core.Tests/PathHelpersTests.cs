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
        var basePath = Path.Join("home", "user", "project");
        var relativePath = Path.Join("subfolder", "file.txt");

        // Act: invoke SafePathCombine with the test inputs
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: result equals Path.Join output
        Assert.Equal(Path.Join(basePath, relativePath), result);
    }

    /// <summary>
    ///     Verifies that traversal using a leading parent-directory segment is rejected.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_PathTraversalWithDoubleDots_ThrowsArgumentException()
    {
        // Arrange: relative path with parent-directory traversal segment
        var basePath = Path.Join("home", "user", "project");
        var relativePath = Path.Join("..", "etc", "passwd");

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
        var basePath = Path.Join("home", "user", "project");
        var relativePath = Path.Join("subfolder", "..", "..", "..", "etc", "passwd");

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
        var basePath = Path.Join("home", "user", "project");
        var segment = Path.DirectorySeparatorChar + "sub";
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
        var basePath = Path.GetFullPath(Path.Join("home", "user", "project"));

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
        var basePath = Path.Join("home", "user", "project");
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
        var basePath = Path.Join("home", "user", "project");
        var relativePath = Path.Join(".", "subfolder", "file.txt");

        // Act: invoke SafePathCombine with the test inputs
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: result equals Path.Join output
        Assert.Equal(Path.Join(basePath, relativePath), result);
    }

    /// <summary>
    ///     Verifies that deeply nested relative paths are combined correctly.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_NestedPaths_CombinesCorrectly()
    {
        // Arrange: deeply nested relative path
        var basePath = Path.Join("home", "user", "project");
        var relativePath = Path.Join("level1", "level2", "level3", "file.txt");

        // Act: invoke SafePathCombine with the test inputs
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: result equals Path.Join output
        Assert.Equal(Path.Join(basePath, relativePath), result);
    }

    /// <summary>
    ///     Verifies that an empty relative path leaves the base path unchanged.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_EmptyRelativePath_ReturnsBasePath()
    {
        // Arrange: empty relative path
        var basePath = Path.Join("home", "user", "project");
        const string RelativePath = "";

        // Act: invoke SafePathCombine with the test inputs
        var result = PathHelpers.SafePathCombine(basePath, RelativePath);

        // Assert: result equals Path.Join output
        Assert.Equal(Path.Join(basePath, RelativePath), result);
    }

    /// <summary>
    ///     Verifies that multiple valid segments are validated and appended in order.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_MultipleSegments_CombinesCorrectly()
    {
        // Arrange: base path and multiple valid relative segments
        var basePath = Path.Join("home", "user", "project");

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
        var basePath = Path.Join("home", "user", "project");

        // Act / Assert: traversal that escapes the base in any segment is rejected
        var exception = Assert.Throws<ArgumentException>(() =>
            PathHelpers.SafePathCombine(basePath, "level1", Path.Join("..", "..", "..", "etc", "passwd")));
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
        var basePath = Path.Join("home", "user", "project");
        string? relativePath = null;

        // Act / Assert: null segment is rejected before path combination
        Assert.Throws<ArgumentNullException>(() =>
            PathHelpers.SafePathCombine(basePath, relativePath!));
    }

    /// <summary>
    ///     Verifies that calling <see cref="PathHelpers.SafePathCombine"/> with zero
    ///     segments returns the base path unchanged.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_NoSegments_ReturnsBasePath()
    {
        // Arrange: a valid base path
        var basePath = Path.Join("home", "user", "project");

        // Act: call SafePathCombine with no additional segments
        var result = PathHelpers.SafePathCombine(basePath);

        // Assert: the result must equal the base path because no segments were appended
        Assert.Equal(basePath, result);
    }

    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ApiMarkPathHelpersTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    ///     Validates that <see cref="PathHelpers.NormalizeCase"/> resolves a
    ///     differently-cased input path to the real, on-disk casing of an existing directory and
    ///     file, regardless of the current platform or file system's own case sensitivity — the
    ///     normalization is driven entirely by looking up the actual directory entries, not by any
    ///     platform assumption.
    /// </summary>
    [Fact]
    public void PathHelpers_NormalizeCase_ExistingPathWithDifferentCaseInput_ResolvesToActualOnDiskCasing()
    {
        // Arrange: create a directory and file with a known, specific mixed-case spelling.
        var root = CreateTempDirectory();
        try
        {
            var actualSubDir = Path.Combine(root, "MyLib");
            Directory.CreateDirectory(actualSubDir);
            var actualFilePath = Path.Combine(actualSubDir, "Foo.dll");
            File.WriteAllBytes(actualFilePath, []);

            // Act: normalize an input path that names the same file but with different casing on
            // both the directory and file-name segments.
            var differentlyCasedInput = Path.Combine(root, "MYLIB", "FOO.DLL");
            var normalized = PathHelpers.NormalizeCase(differentlyCasedInput);

            // Assert: the normalized result matches the real on-disk casing exactly, not the
            // differently-cased input.
            Assert.Equal(actualFilePath, normalized, StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that <see cref="PathHelpers.NormalizeCase"/> canonicalizes a
    ///     Windows drive-letter root prefix so that two differently-cased spellings of the same
    ///     drive (e.g. <c>c:\...</c> and <c>C:\...</c>) normalize to an identical result — closing
    ///     a gap where the root prefix, unlike every subsequent segment, cannot be resolved via a
    ///     directory-entry lookup and was previously preserved verbatim, breaking the
    ///     documented at-most-once/deduplication guarantee for two spellings of the same root.
    /// </summary>
    [Fact]
    public void PathHelpers_NormalizeCase_DifferentlyCasedDriveLetterRoot_ProducesIdenticalResult()
    {
        var root = CreateTempDirectory();
        try
        {
            var actualFilePath = Path.Combine(root, "Foo.dll");
            File.WriteAllBytes(actualFilePath, []);

            // This scenario only applies on platforms with a drive-letter root (Windows); on
            // other platforms Path.GetPathRoot returns "/" and there is no drive letter to vary.
            var rootPrefix = Path.GetPathRoot(actualFilePath) ?? string.Empty;
            if (rootPrefix.Length is not (2 or 3) || rootPrefix[1] != ':')
            {
                return;
            }

            var lowerCasedInput = char.ToLowerInvariant(actualFilePath[0]) + actualFilePath[1..];
            var upperCasedInput = char.ToUpperInvariant(actualFilePath[0]) + actualFilePath[1..];

            var normalizedLower = PathHelpers.NormalizeCase(lowerCasedInput);
            var normalizedUpper = PathHelpers.NormalizeCase(upperCasedInput);

            // Assert: both spellings of the drive letter normalize to the exact same string, so
            // downstream Ordinal comparison correctly treats them as the same file.
            Assert.Equal(normalizedUpper, normalizedLower, StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that two differently-cased input paths naming the SAME real, existing file
    ///     normalize to an identical result, proving they can be safely compared with a
    ///     case-sensitive comparer after normalization.
    /// </summary>
    [Fact]
    public void PathHelpers_NormalizeCase_TwoCaseVariantsOfSamePath_ProduceIdenticalResult()
    {
        var root = CreateTempDirectory();
        try
        {
            var actualSubDir = Path.Combine(root, "Lib");
            Directory.CreateDirectory(actualSubDir);
            var actualFilePath = Path.Combine(actualSubDir, "Foo.dll");
            File.WriteAllBytes(actualFilePath, []);

            var normalizedLower = PathHelpers.NormalizeCase(Path.Combine(root, "lib", "foo.dll"));
            var normalizedUpper = PathHelpers.NormalizeCase(Path.Combine(root, "LIB", "FOO.DLL"));

            Assert.Equal(normalizedLower, normalizedUpper, StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that <see cref="PathHelpers.NormalizeCase"/> falls back to the
    ///     caller-supplied casing for segments that do not exist on disk (e.g. a reference path
    ///     that does not resolve to a real file), rather than throwing.
    /// </summary>
    [Fact]
    public void PathHelpers_NormalizeCase_NonExistentPath_PreservesSuppliedCasing()
    {
        var root = CreateTempDirectory();
        try
        {
            var nonExistentPath = Path.Combine(root, "DoesNotExist", "Missing.dll");

            var normalized = PathHelpers.NormalizeCase(nonExistentPath);

            Assert.Equal(nonExistentPath, normalized, StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that when two entries coexist in the same directory differing only by case
    ///     (possible on a case-sensitive file system), <see cref="PathHelpers.NormalizeCase"/>
    ///     prefers the exact (ordinal) match over an incidental case-insensitive one, rather than
    ///     letting unspecified directory-enumeration order decide which entry "wins".
    /// </summary>
    [Fact]
    public void PathHelpers_NormalizeCase_CaseSensitiveFileSystemWithBothCasings_PrefersExactMatch()
    {
        var root = CreateTempDirectory();
        try
        {
            var lower = Path.Combine(root, "foo.dll");
            var upper = Path.Combine(root, "Foo.dll");
            File.WriteAllBytes(lower, []);
            File.WriteAllBytes(upper, []);

            // On a case-insensitive file system, the second write overwrites the same physical
            // file as the first, leaving only one directory entry — this scenario cannot be
            // exercised there, so skip rather than assert something meaningless.
            if (Directory.EnumerateFileSystemEntries(root).Count() < 2)
            {
                return;
            }

            // Act: request the path spelled exactly as the entry actually named "Foo.dll"
            var normalized = PathHelpers.NormalizeCase(upper);

            // Assert: the exact match is returned regardless of enumeration order
            Assert.Equal(upper, normalized, StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that when two entries coexist in the same directory differing only by case
    ///     (possible on a case-sensitive file system) and the supplied segment matches neither one
    ///     exactly (a third casing), <see cref="PathHelpers.NormalizeCase"/> refuses to
    ///     guess which of the two coexisting entries was meant — preserving the as-supplied casing
    ///     — rather than letting unspecified directory-enumeration order silently pick one.
    /// </summary>
    [Fact]
    public void PathHelpers_NormalizeCase_CaseSensitiveFileSystemWithBothCasings_AmbiguousThirdCasing_PreservesSuppliedCasing()
    {
        var root = CreateTempDirectory();
        try
        {
            var lower = Path.Combine(root, "foo.dll");
            var upper = Path.Combine(root, "Foo.dll");
            File.WriteAllBytes(lower, []);
            File.WriteAllBytes(upper, []);

            // On a case-insensitive file system, the second write overwrites the same physical
            // file as the first, leaving only one directory entry — this scenario cannot be
            // exercised there, so skip rather than assert something meaningless.
            if (Directory.EnumerateFileSystemEntries(root).Count() < 2)
            {
                return;
            }

            // Act: request a third casing that matches neither coexisting entry exactly
            var thirdCasing = Path.Combine(root, "FOO.dll");
            var normalized = PathHelpers.NormalizeCase(thirdCasing);

            // Assert: neither coexisting entry is guessed at; the supplied casing is preserved
            Assert.Equal(thirdCasing, normalized, StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that a caller-supplied <c>directoryEntryCache</c> dictionary, shared across
    ///     multiple <see cref="PathHelpers.NormalizeCase"/> calls for paths under the
    ///     same ancestor directory, still resolves each path correctly — proving the cache reuse
    ///     added to avoid redundant directory enumeration does not corrupt results for a second,
    ///     differently-named file once the shared ancestor directory's entries are cached.
    /// </summary>
    [Fact]
    public void PathHelpers_NormalizeCase_WithSharedDirectoryEntryCache_ResolvesMultipleSiblingPaths()
    {
        var root = CreateTempDirectory();
        try
        {
            var actualSubDir = Path.Combine(root, "Shared");
            Directory.CreateDirectory(actualSubDir);
            var fileA = Path.Combine(actualSubDir, "A.dll");
            var fileB = Path.Combine(actualSubDir, "B.dll");
            File.WriteAllBytes(fileA, []);
            File.WriteAllBytes(fileB, []);

            var cache = new Dictionary<string, string[]>(PathHelpers.Comparer);
            var normalizedA = PathHelpers.NormalizeCase(Path.Combine(root, "SHARED", "a.dll"), cache);
            var normalizedB = PathHelpers.NormalizeCase(Path.Combine(root, "SHARED", "b.dll"), cache);

            Assert.Equal(fileA, normalizedA, StringComparer.Ordinal);
            Assert.Equal(fileB, normalizedB, StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
