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
                return false;
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
}
