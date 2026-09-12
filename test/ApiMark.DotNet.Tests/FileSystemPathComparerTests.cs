using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Unit tests for <see cref="FileSystemPathComparer"/>.</summary>
public class FileSystemPathComparerTests
{
    private static string CreateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ApiMarkFileSystemPathComparerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    ///     Validates that <see cref="FileSystemPathComparer.NormalizeCase"/> resolves a
    ///     differently-cased input path to the real, on-disk casing of an existing directory and
    ///     file, regardless of the current platform or file system's own case sensitivity — the
    ///     normalization is driven entirely by looking up the actual directory entries, not by any
    ///     platform assumption.
    /// </summary>
    [Fact]
    public void FileSystemPathComparer_NormalizeCase_ExistingPathWithDifferentCaseInput_ResolvesToActualOnDiskCasing()
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
            var normalized = FileSystemPathComparer.NormalizeCase(differentlyCasedInput);

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
    ///     Validates that two differently-cased input paths naming the SAME real, existing file
    ///     normalize to an identical result, proving they can be safely compared with a
    ///     case-sensitive comparer after normalization.
    /// </summary>
    [Fact]
    public void FileSystemPathComparer_NormalizeCase_TwoCaseVariantsOfSamePath_ProduceIdenticalResult()
    {
        var root = CreateTempDirectory();
        try
        {
            var actualSubDir = Path.Combine(root, "Lib");
            Directory.CreateDirectory(actualSubDir);
            var actualFilePath = Path.Combine(actualSubDir, "Foo.dll");
            File.WriteAllBytes(actualFilePath, []);

            var normalizedLower = FileSystemPathComparer.NormalizeCase(Path.Combine(root, "lib", "foo.dll"));
            var normalizedUpper = FileSystemPathComparer.NormalizeCase(Path.Combine(root, "LIB", "FOO.DLL"));

            Assert.Equal(normalizedLower, normalizedUpper, StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that <see cref="FileSystemPathComparer.NormalizeCase"/> falls back to the
    ///     caller-supplied casing for segments that do not exist on disk (e.g. a reference path
    ///     that does not resolve to a real file), rather than throwing.
    /// </summary>
    [Fact]
    public void FileSystemPathComparer_NormalizeCase_NonExistentPath_PreservesSuppliedCasing()
    {
        var root = CreateTempDirectory();
        try
        {
            var nonExistentPath = Path.Combine(root, "DoesNotExist", "Missing.dll");

            var normalized = FileSystemPathComparer.NormalizeCase(nonExistentPath);

            Assert.Equal(nonExistentPath, normalized, StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
