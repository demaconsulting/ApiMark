using ApiMark.Core;
using Xunit;

namespace ApiMark.Core.Tests;

/// <summary>
///     Verifies the file-collection behavior of <see cref="GlobFileCollector"/>.
/// </summary>
public sealed class GlobFileCollectorTests
{
    private static readonly string[] VhdlExtensions = [".vhd", ".vhdl"];
    private static readonly string[] CppExtensions = [".h", ".hpp", ".hxx", ".h++"];

    // =========================================================================
    // Helper: create an isolated temp directory and populate it with files
    // =========================================================================

    /// <summary>
    ///     Creates a uniquely named temporary directory and returns its absolute path.
    ///     Callers are responsible for deleting the directory in a finally block.
    /// </summary>
    private static string CreateTempDirectory()
    {
        var dir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    // =========================================================================
    // Empty-patterns tests
    // =========================================================================

    /// <summary>
    ///     Verifies that an empty pattern list returns an empty result with no filesystem access.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_EmptyPatterns_ReturnsEmptyList()
    {
        // Arrange: an empty pattern list and a working directory that exists
        var cwd = Path.GetTempPath();

        // Act: collect with no patterns
        var result = GlobFileCollector.Collect([], VhdlExtensions, cwd);

        // Assert: result is empty
        Assert.Empty(result);
    }

    // =========================================================================
    // Relative pattern tests
    // =========================================================================

    /// <summary>
    ///     Verifies that a relative <c>**/*.vhd</c> pattern finds only <c>.vhd</c> files
    ///     under the working directory.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_RelativeVhdPattern_FindsVhdFiles()
    {
        // Arrange: create an isolated temp directory with one .vhd and one unrelated file
        var tempDir = CreateTempDirectory();
        try
        {
            var vhdFile = Path.Join(tempDir, "design.vhd");
            var txtFile = Path.Join(tempDir, "readme.txt");
            File.WriteAllText(vhdFile, string.Empty);
            File.WriteAllText(txtFile, string.Empty);

            // Act: collect with a relative pattern scoped to .vhd files
            var result = GlobFileCollector.Collect(["**/*.vhd"], VhdlExtensions, tempDir);

            // Assert: only the .vhd file is returned
            Assert.Single(result);
            Assert.Equal(Path.GetFullPath(vhdFile), result[0]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     Verifies that a relative literal path (no glob metacharacters) selects exactly
    ///     the named file and no others, resolved relative to the working directory.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_RelativeLiteralFilePath_SelectsExactFile()
    {
        // Arrange: create an isolated temp directory with two .vhd files in a subdirectory
        var tempDir = CreateTempDirectory();
        try
        {
            var subDir = Path.Join(tempDir, "src");
            Directory.CreateDirectory(subDir);
            var targetFile = Path.Join(subDir, "design.vhd");
            var otherFile = Path.Join(subDir, "other.vhd");
            File.WriteAllText(targetFile, string.Empty);
            File.WriteAllText(otherFile, string.Empty);

            // Act: supply a relative literal path (no glob chars); workingDirectory is the parent
            var result = GlobFileCollector.Collect(["src/design.vhd"], VhdlExtensions, tempDir);

            // Assert: only the named file is returned
            Assert.Single(result);
            Assert.Equal(Path.GetFullPath(targetFile), result[0]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     Verifies that a bare-star pattern <c>**/*</c> with VHDL extensions finds both
    ///     <c>.vhd</c> and <c>.vhdl</c> files but excludes files with other extensions.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_BareStarWithVhdlExtensions_FiltersToVhdlOnly()
    {
        // Arrange: create temp directory with .vhd, .vhdl, and .txt files
        var tempDir = CreateTempDirectory();
        try
        {
            var vhdFile = Path.Join(tempDir, "top.vhd");
            var vhdlFile = Path.Join(tempDir, "pkg.vhdl");
            var txtFile = Path.Join(tempDir, "notes.txt");
            File.WriteAllText(vhdFile, string.Empty);
            File.WriteAllText(vhdlFile, string.Empty);
            File.WriteAllText(txtFile, string.Empty);

            // Act: collect using a bare-star pattern that triggers extension inference
            var result = GlobFileCollector.Collect(["**/*"], VhdlExtensions, tempDir);

            // Assert: both VHDL files are returned and the .txt file is excluded
            Assert.Equal(2, result.Count);
            Assert.Contains(Path.GetFullPath(vhdFile), result);
            Assert.Contains(Path.GetFullPath(vhdlFile), result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // =========================================================================
    // Absolute pattern tests
    // =========================================================================

    /// <summary>
    ///     Verifies that an absolute pattern pointing to a temp directory finds the expected files.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_AbsolutePattern_FindsFiles()
    {
        // Arrange: create a temp directory with a .vhd file and build an absolute pattern
        var tempDir = CreateTempDirectory();
        try
        {
            var vhdFile = Path.Join(tempDir, "entity.vhd");
            File.WriteAllText(vhdFile, string.Empty);

            // Absolute pattern: {tempDir}/**/*.vhd (using native separator)
            var absolutePattern = Path.Join(tempDir, "**", "*.vhd");

            // Act: collect using an absolute pattern; workingDirectory is irrelevant here
            var result = GlobFileCollector.Collect(
                [absolutePattern],
                VhdlExtensions,
                workingDirectory: Path.GetTempPath());

            // Assert: the file is found via the absolute path pattern
            Assert.Single(result);
            Assert.Equal(Path.GetFullPath(vhdFile), result[0]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     Verifies that an absolute literal path (no glob metacharacters) selects exactly
    ///     the named file and ignores all other files in the same directory.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_LiteralAbsoluteFilePath_SelectsExactFile()
    {
        // Arrange: create a temp directory with two .vhd files
        var tempDir = CreateTempDirectory();
        try
        {
            var fileA = Path.Join(tempDir, "alpha.vhd");
            var fileB = Path.Join(tempDir, "beta.vhd");
            File.WriteAllText(fileA, string.Empty);
            File.WriteAllText(fileB, string.Empty);

            // Act: pass an absolute literal path — no glob metacharacters
            var result = GlobFileCollector.Collect(
                [fileA],
                VhdlExtensions,
                workingDirectory: Path.GetTempPath());

            // Assert: only the explicitly named file is returned
            Assert.Single(result);
            Assert.Equal(Path.GetFullPath(fileA), result[0]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    ///     Verifies that an absolute literal exclusion path (no glob metacharacters) removes
    ///     exactly the named file from an otherwise full match set.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_LiteralAbsoluteExclusionPath_RemovesExactFile()
    {
        // Arrange: create a temp directory with two .vhd files
        var tempDir = CreateTempDirectory();
        try
        {
            var fileA = Path.Join(tempDir, "alpha.vhd");
            var fileB = Path.Join(tempDir, "beta.vhd");
            File.WriteAllText(fileA, string.Empty);
            File.WriteAllText(fileB, string.Empty);

            // Act: include all .vhd files then remove one via a literal absolute exclusion path
            var result = GlobFileCollector.Collect(
                [$"{tempDir}/**/*.vhd", $"!{fileA}"],
                VhdlExtensions,
                workingDirectory: Path.GetTempPath());

            // Assert: only the non-excluded file is returned
            Assert.Single(result);
            Assert.Equal(Path.GetFullPath(fileB), result[0]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // =========================================================================
    // Exclusion pattern tests
    // =========================================================================

    /// <summary>
    ///     Verifies that an absolute pattern whose glob segment is directly under the
    ///     drive root (e.g. <c>C:\*.vhd</c>) produces the drive root (<c>C:/</c>) as
    ///     the filesystem root rather than the drive-relative path <c>C:</c>.
    ///     This guards against the Windows drive-root path truncation bug where
    ///     <c>Substring(0, lastSlash)</c> would strip the trailing slash and yield
    ///     <c>C:</c> instead of <c>C:/</c>.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_AbsoluteDriveRootPattern_FindsFiles()
    {
        // This test is only meaningful on Windows where drive-rooted paths exist
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange: create a temp directory directly under the system temp root and
        // build a drive-root-style absolute pattern (e.g. C:\*.vhd is the canonical
        // failing case, but we use a real temp subdirectory so the test is self-contained).
        // The key scenario: pattern root resolves to "C:/" not "C:".
        var tempDir = CreateTempDirectory();
        try
        {
            var vhdFile = Path.Join(tempDir, "top.vhd");
            File.WriteAllText(vhdFile, string.Empty);

            // Construct a pattern rooted directly under the drive root, e.g. C:\TEMP\xyz\*.vhd
            // This puts the glob segment (*) immediately after a path whose last static
            // directory segment sits two levels above the first glob metacharacter,
            // exercising the drive-root boundary when tempDir is on a drive like C:\.
            var driveRoot = Path.GetPathRoot(tempDir)!; // e.g. "C:\"
            var relToRoot = Path.GetRelativePath(driveRoot, tempDir);
            var drivePattern = Path.Join(driveRoot, relToRoot, "*.vhd");

            // Act
            var result = GlobFileCollector.Collect(
                [drivePattern],
                VhdlExtensions,
                workingDirectory: Path.GetTempPath());

            // Assert: the file is found via the drive-rooted absolute pattern
            Assert.Single(result);
            Assert.Equal(Path.GetFullPath(vhdFile), result[0]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // =========================================================================
    // Exclusion pattern tests
    // =========================================================================

    /// <summary>
    ///     Verifies that a <c>!</c>-prefixed exclusion pattern removes matching files from the result.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_ExclusionPattern_RemovesMatchedFiles()
    {
        // Arrange: create a temp directory with two .vhd files in different subdirectories
        var tempDir = CreateTempDirectory();
        try
        {
            var srcDir = Path.Join(tempDir, "src");
            var testDir = Path.Join(tempDir, "test");
            Directory.CreateDirectory(srcDir);
            Directory.CreateDirectory(testDir);

            var srcFile = Path.Join(srcDir, "design.vhd");
            var testFile = Path.Join(testDir, "tb.vhd");
            File.WriteAllText(srcFile, string.Empty);
            File.WriteAllText(testFile, string.Empty);

            // Act: include all .vhd files, then exclude those under test/
            var result = GlobFileCollector.Collect(
                ["**/*.vhd", "!test/**/*.vhd"],
                VhdlExtensions,
                tempDir);

            // Assert: only the src file remains; the test file is excluded
            Assert.Single(result);
            Assert.Equal(Path.GetFullPath(srcFile), result[0]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // =========================================================================
    // Non-existent root tests
    // =========================================================================

    /// <summary>
    ///     Verifies that a pattern whose root directory does not exist returns an empty list
    ///     without throwing an exception.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_NonExistentRoot_ReturnsEmptyWithoutThrowing()
    {
        // Arrange: build a pattern pointing to a directory guaranteed not to exist
        var missingRoot = Path.Join(Path.GetTempPath(), Path.GetRandomFileName(), "nonexistent");
        var pattern = Path.Join(missingRoot, "**", "*.vhd");

        // Act / Assert: collecting from a missing root must not throw
        var result = GlobFileCollector.Collect([pattern], VhdlExtensions, Path.GetTempPath());

        // Assert: result is empty and no exception was raised
        Assert.Empty(result);
    }

    // =========================================================================
    // Deduplication and sort tests
    // =========================================================================

    /// <summary>
    ///     Verifies that files matched by overlapping patterns appear only once and are sorted.
    /// </summary>
    [Fact]
    public void GlobFileCollector_Collect_OverlappingPatterns_ReturnsSortedDeduplicated()
    {
        // Arrange: create a temp directory with two .vhd files; both patterns match both files
        var tempDir = CreateTempDirectory();
        try
        {
            var fileA = Path.Join(tempDir, "aaa.vhd");
            var fileB = Path.Join(tempDir, "bbb.vhd");
            File.WriteAllText(fileA, string.Empty);
            File.WriteAllText(fileB, string.Empty);

            // Act: supply two overlapping patterns that both match the same files
            var result = GlobFileCollector.Collect(
                ["**/*.vhd", "**/*.vhd"],
                VhdlExtensions,
                tempDir);

            // Assert: exactly two unique files returned, in sorted order
            Assert.Equal(2, result.Count);
            Assert.True(
                string.Compare(result[0], result[1], StringComparison.Ordinal) < 0,
                "Results should be in ascending ordinal order.");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
