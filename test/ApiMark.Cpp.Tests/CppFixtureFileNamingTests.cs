// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using Xunit;

namespace ApiMark.Cpp.Tests;

/// <summary>
///     Whole-output naming properties of the clang-generated fixture documentation. These tests require clang.
/// </summary>
[Collection("ClangEnvironment")]
public class CppFixtureFileNamingTests : IClassFixture<CppGeneratorFixture>
{
    /// <summary>The shared clang-generated fixture output.</summary>
    private readonly CppGeneratorFixture _fixture;

    /// <summary>Initializes a new instance of the <see cref="CppFixtureFileNamingTests"/> class.</summary>
    /// <param name="fixture">The shared fixture.</param>
    public CppFixtureFileNamingTests(CppGeneratorFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>The fixture output is unique, legal, and fully linked for every visibility tier.</summary>
    [Fact]
    public void CppGenerator_FixtureOutput_AllPagesUniqueLegalAndLinksResolve()
    {
        // Arrange
        var factories = new[]
        {
            _fixture.PublicFactory,
            _fixture.WithDeprecatedFactory,
            _fixture.PublicAndProtectedFactory,
            _fixture.AllFactory,
        };

        // Act / Assert
        foreach (var factory in factories)
        {
            CppFileNamingTests.AssertUniqueAndLegal(factory.Writers.Keys);
            Assert.True(CppFileNamingTests.AssertAllLinksResolve(factory.Writers) > 0);
        }
    }
}
