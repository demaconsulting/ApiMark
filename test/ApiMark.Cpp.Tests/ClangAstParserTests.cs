// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using ApiMark.Cpp;
using ApiMark.Cpp.CppAst;
using Xunit;

namespace ApiMark.Cpp.Tests;

/// <summary>Integration-flavoured unit tests for <see cref="ClangAstParser"/> that require clang to be available.</summary>
public class ClangAstParserTests
{
    /// <summary>Checks whether clang is available on the current system by attempting to run "clang --version".</summary>
    /// <returns><see langword="true"/> when clang is discoverable; <see langword="false"/> otherwise.</returns>
    private static bool IsClangAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("clang", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(psi);
            if (process == null)
            {
                return false;
            }

            if (!process.WaitForExit(5000))
            {
                // Timeout — kill the process tree and treat as unavailable
                process.Kill(entireProcessTree: true);
                return false;
            }
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Builds <see cref="CppGeneratorOptions"/> pointing at the fixture include directory.</summary>
    private static CppGeneratorOptions BuildOptions() => new()
    {
        LibraryName = "Fixtures",
        PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
    };

    /// <summary>Validates that null headers are rejected before any clang discovery occurs.</summary>
    [Fact]
    public void ClangAstParser_Parse_NullHeaders_ThrowsArgumentNullException()
    {
        // Arrange
        var options = BuildOptions();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ClangAstParser.Parse(null!, options));
    }

    /// <summary>Validates that a null options argument is rejected before any clang discovery occurs.</summary>
    [Fact]
    public void ClangAstParser_Parse_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange: a non-empty header list that would otherwise be valid
        var headers = new List<string> { "placeholder.h" };

        // Act / Assert: null options must be rejected immediately
        Assert.Throws<ArgumentNullException>(() => ClangAstParser.Parse(headers, null!));
    }

    /// <summary>Validates that an empty header list is rejected before any clang discovery occurs.</summary>
    [Fact]
    public void ClangAstParser_Parse_EmptyHeaders_ThrowsArgumentException()
    {
        // Arrange
        var options = BuildOptions();

        // Act / Assert
        Assert.Throws<ArgumentException>(() => ClangAstParser.Parse([], options));
    }

    /// <summary>Validates that an invalid explicit clang path produces a clear <see cref="InvalidOperationException"/>.</summary>
    [Fact]
    public void ClangAstParser_Parse_InvalidExplicitClangPath_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = BuildOptions();
        options.ClangPath = Path.Join(Path.GetTempPath(), $"missing-clang-{Guid.NewGuid():N}.exe");
        var headers = Directory.GetFiles(FixturePaths.GetFixtureNamespaceDir(), "*.h").ToList();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => ClangAstParser.Parse(headers, options));
    }

    /// <summary>Validates that parsing fixture headers returns non-empty namespaces.</summary>
    [Fact]
    public void ClangAstParser_Parse_FixtureHeaders_ReturnsNonEmptyNamespaces()
    {
        // Skip when clang is not available
        if (!IsClangAvailable())
        {
            Assert.Skip("clang is not available on this system.");
        }

        // Arrange
        var options = BuildOptions();
        var headers = Directory.GetFiles(FixturePaths.GetFixtureNamespaceDir(), "*.h").ToList();

        // Act
        var result = ClangAstParser.Parse(headers, options);

        // Assert
        Assert.NotEmpty(result.Namespaces);
    }

    /// <summary>Validates that parsing fixture headers produces the fixtures namespace.</summary>
    [Fact]
    public void ClangAstParser_Parse_FixtureHeaders_ContainsFixturesNamespace()
    {
        // Skip when clang is not available
        if (!IsClangAvailable())
        {
            Assert.Skip("clang is not available on this system.");
        }

        // Arrange
        var options = BuildOptions();
        var headers = Directory.GetFiles(FixturePaths.GetFixtureNamespaceDir(), "*.h").ToList();

        // Act
        var result = ClangAstParser.Parse(headers, options);

        // Assert
        Assert.Contains(result.Namespaces, ns => ns.QualifiedName.Contains("fixtures", StringComparison.Ordinal));
    }

    /// <summary>Validates that the fixtures namespace contains a SampleClass.</summary>
    [Fact]
    public void ClangAstParser_Parse_FixtureHeaders_FixturesNamespaceContainsSampleClass()
    {
        // Skip when clang is not available
        if (!IsClangAvailable())
        {
            Assert.Skip("clang is not available on this system.");
        }

        // Arrange
        var options = BuildOptions();
        var headers = Directory.GetFiles(FixturePaths.GetFixtureNamespaceDir(), "*.h").ToList();

        // Act
        var result = ClangAstParser.Parse(headers, options);

        // Assert
        var fixturesNs = result.Namespaces.FirstOrDefault(ns => ns.QualifiedName.Contains("fixtures", StringComparison.Ordinal));
        Assert.NotNull(fixturesNs);
        Assert.Contains(fixturesNs.Classes, c => c.Name == "SampleClass");
    }

    /// <summary>Validates that the SampleClass in the fixtures namespace has members.</summary>
    [Fact]
    public void ClangAstParser_Parse_FixtureHeaders_SampleClassHasMembers()
    {
        // Skip when clang is not available
        if (!IsClangAvailable())
        {
            Assert.Skip("clang is not available on this system.");
        }

        // Arrange
        var options = BuildOptions();
        var headers = Directory.GetFiles(FixturePaths.GetFixtureNamespaceDir(), "*.h").ToList();

        // Act
        var result = ClangAstParser.Parse(headers, options);

        // Assert
        var fixturesNs = result.Namespaces.FirstOrDefault(ns => ns.QualifiedName.Contains("fixtures", StringComparison.Ordinal));
        Assert.NotNull(fixturesNs);
        var sampleClass = fixturesNs.Classes.FirstOrDefault(c => c.Name == "SampleClass");
        Assert.NotNull(sampleClass);
        Assert.NotEmpty(sampleClass.Members);
    }

    /// <summary>Validates that parsing well-formed fixture headers produces an empty errors collection.</summary>
    [Fact]
    public void ClangAstParser_Parse_FixtureHeaders_ErrorsCollectionIsEmpty()
    {
        // Skip when clang is not available
        if (!IsClangAvailable())
        {
            Assert.Skip("clang is not available on this system.");
        }

        // Arrange
        var options = BuildOptions();
        var headers = Directory.GetFiles(FixturePaths.GetFixtureNamespaceDir(), "*.h").ToList();

        // Act
        var result = ClangAstParser.Parse(headers, options);

        // Assert: clean fixture headers must not produce any error-class diagnostics
        Assert.Empty(result.Errors);
    }

    /// <summary>
    ///     Validates that <see cref="ClangAstParser.Parse"/>, invoked directly (bypassing
    ///     <see cref="CppGenerator"/>), still resolves an angle-bracket <c>#include</c> via a
    ///     <see cref="CppGeneratorOptions.PublicIncludeRoots"/> entry supplied with different
    ///     casing than its on-disk spelling, on a case-sensitive file system.
    /// </summary>
    /// <remarks>
    ///     Regression test: <see cref="ClangAstParser"/> previously normalized
    ///     <see cref="CppGeneratorOptions.PublicIncludeRoots"/> only for its own ownership check,
    ///     while the clang <c>-I</c> arguments built by <c>BuildArguments</c> still read the
    ///     un-normalized <see cref="CppGeneratorOptions"/> supplied by the caller. That was masked
    ///     when parsing was reached via <see cref="CppGenerator.Parse"/> (which normalizes the
    ///     roots itself before calling this method), but a direct caller supplying a differently
    ///     cased root would give clang an unusable <c>-I</c> path and fail to resolve the include,
    ///     even though ownership filtering used the corrected path. <see cref="ClangAstParser.Parse"/>
    ///     now normalizes the roots itself, once, before building the clang arguments.
    /// </remarks>
    [Fact]
    public void ClangAstParser_Parse_CalledDirectlyWithDifferentlyCasedIncludeRoot_ResolvesAngleBracketInclude()
    {
        // Skip when clang is not available
        if (!IsClangAvailable())
        {
            Assert.Skip("clang is not available on this system.");
        }

        // Arrange: an include root "MyLib" containing a header reachable only via an
        // angle-bracket #include (which is resolved via the -I search path, unlike a
        // quoted #include which also checks the including file's own directory), and a
        // separate entry header — outside the root — that includes it by angle brackets
        // and referenced with a differently-cased spelling of the same root ("MYLIB").
        var tempParent = Path.Combine(Path.GetTempPath(), "ApiMarkClangAstParserTests_" + Guid.NewGuid().ToString("N"));
        var actualIncludeDir = Path.Combine(tempParent, "MyLib");
        Directory.CreateDirectory(actualIncludeDir);
        File.WriteAllText(
            Path.Combine(actualIncludeDir, "dep.h"),
            "class Dep { public: int Value() const { return 1; } };");
        var entryHeader = Path.Combine(tempParent, "entry.h");
        File.WriteAllText(entryHeader, "#include <dep.h>\nDep MakeDep();");
        try
        {
            var differentlyCasedIncludeDir = Path.Combine(tempParent, "MYLIB");
            var options = new CppGeneratorOptions
            {
                LibraryName = "Fixtures",
                PublicIncludeRoots = [differentlyCasedIncludeDir],
            };

            // Act: call ClangAstParser.Parse directly, bypassing CppGenerator entirely, so
            // this exercises BuildArguments with the caller-supplied (not pre-normalized) options
            var result = ClangAstParser.Parse([entryHeader], options);

            // Assert: no error-class diagnostics — proving clang located dep.h via the -I flag
            // built from the differently-cased root
            Assert.Empty(result.Errors);
        }
        finally
        {
            Directory.Delete(tempParent, recursive: true);
        }
    }
}
