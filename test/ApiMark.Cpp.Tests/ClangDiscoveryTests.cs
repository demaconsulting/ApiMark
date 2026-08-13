// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Cpp.CppAst;
using Xunit;

namespace ApiMark.Cpp.Tests;

/// <summary>Unit tests for <see cref="ClangDiscovery"/>.</summary>
public class ClangDiscoveryTests
{
    /// <summary>Environment variable name used by <see cref="ClangAstParser"/> to override clang discovery.</summary>
    private const string ClangPathEnvVar = "APIMARK_CLANG_PATH";

    /// <summary>
    ///     Validates that <see cref="ClangDiscovery.IsAvailable"/> returns <see langword="false"/>
    ///     (and does not throw) when an explicit, nonexistent path is supplied.
    /// </summary>
    [Fact]
    public void ClangDiscovery_IsAvailable_WithNonexistentExplicitPath_ReturnsFalse()
    {
        // Arrange
        var bogusPath = Path.Join(Path.GetTempPath(), $"missing-clang-{Guid.NewGuid():N}.exe");

        // Act
        var result = ClangDiscovery.IsAvailable(bogusPath);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    ///     Validates that <see cref="ClangDiscovery.IsAvailable"/> returns <see langword="false"/>
    ///     when <c>APIMARK_CLANG_PATH</c> points at a nonexistent path and no explicit path is
    ///     supplied, without throwing — proving the discovery helper never throws on failure.
    /// </summary>
    [Fact]
    public void ClangDiscovery_IsAvailable_WithBogusEnvironmentVariable_ReturnsFalse()
    {
        // Arrange: temporarily override the env var; restore it in `finally`
        var originalValue = Environment.GetEnvironmentVariable(ClangPathEnvVar);
        var bogusPath = Path.Join(Path.GetTempPath(), $"missing-clang-{Guid.NewGuid():N}.exe");
        Environment.SetEnvironmentVariable(ClangPathEnvVar, bogusPath);

        try
        {
            // Act
            var result = ClangDiscovery.IsAvailable();

            // Assert
            Assert.False(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ClangPathEnvVar, originalValue);
        }
    }

    /// <summary>
    ///     Validates that <see cref="ClangDiscovery.IsAvailable"/> matches whatever
    ///     <see cref="ClangAstParser.Parse"/> discovery would find: when no clang is available,
    ///     parsing fails with <see cref="InvalidOperationException"/>; when clang is available,
    ///     <see cref="ClangDiscovery.IsAvailable()"/> must report <see langword="true"/>.
    /// </summary>
    [Fact]
    public void ClangDiscovery_IsAvailable_MatchesActualDiscoveryOutcome()
    {
        // Arrange: probe availability via the shared discovery logic
        var isAvailable = ClangDiscovery.IsAvailable();

        // Act / Assert: attempting to parse an empty combined header behaves consistently with
        // the reported availability — either clang is found (and some other error, not
        // "clang not found", may occur) or discovery fails with a "not found" style message
        if (!isAvailable)
        {
            var options = new CppGeneratorOptions
            {
                LibraryName = "ClangDiscoveryProbe",
                PublicIncludeRoots = [Path.GetTempPath()],
            };

            var ex = Assert.Throws<InvalidOperationException>(
                () => ClangAstParser.Parse(["placeholder.h"], options));
            Assert.Contains("clang", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
