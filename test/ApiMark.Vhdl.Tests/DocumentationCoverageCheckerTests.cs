using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using Xunit;

namespace ApiMark.Vhdl.Tests;

/// <summary>Unit tests for <see cref="DocumentationCoverageChecker"/> and <see cref="VhdlGenerator.CheckDocumentationCoverage"/>.</summary>
/// <remarks>
///     Uses <c>mux.vhd</c>/<c>counter.vhd</c>/<c>common_types.vhd</c> (all fully documented) for
///     the zero-violation and tier-parity scenarios, and the dedicated
///     <c>undocumented.vhd</c> fixture (entity/generic/port/package/type/constant/component/
///     subprogram each intentionally missing a doc comment, plus an architecture with an
///     intentionally undocumented internal signal) for violation-kind and scope-boundary
///     scenarios.
/// </remarks>
public class DocumentationCoverageCheckerTests
{
    /// <summary>Builds a <see cref="VhdlGeneratorOptions"/> scoped to a single named fixture file.</summary>
    /// <param name="fileName">The fixture file name under <see cref="FixturePaths.FixturesDirectory"/>.</param>
    /// <returns>A configured <see cref="VhdlGeneratorOptions"/>.</returns>
    private static VhdlGeneratorOptions BuildOptions(string fileName)
    {
        return new VhdlGeneratorOptions
        {
            LibraryName = "TestLib",
            WorkingDirectory = FixturePaths.FixturesDirectory,
            Sources = [fileName],
        };
    }

    /// <summary>Validates that fully-documented entities and packages report zero violations.</summary>
    [Fact]
    public void Check_FullyDocumentedFiles_ReportsZeroViolations()
    {
        // Arrange
        var generator = new VhdlGenerator(BuildOptions("counter.vhd"));
        generator.Parse(new InMemoryContext());

        // Act
        var result = generator.CheckDocumentationCoverage("Public");

        // Assert
        Assert.False(result.HasViolations);
        Assert.Empty(result.UndocumentedItems);
        Assert.True(result.CheckedCount > 0);
    }

    /// <summary>
    ///     Validates that an undocumented entity, its generic, and its port each report a
    ///     violation with the expected kind label.
    /// </summary>
    [Fact]
    public void Check_UndocumentedEntity_ReportsEntityGenericAndPortViolations()
    {
        // Arrange
        var generator = new VhdlGenerator(BuildOptions("undocumented.vhd"));
        generator.Parse(new InMemoryContext());

        // Act
        var result = generator.CheckDocumentationCoverage("Public");

        // Assert
        Assert.Contains(result.UndocumentedItems, i => i is { Kind: "Entity", DisplayName: "undocumented_entity" });
        Assert.Contains(result.UndocumentedItems, i => i is { Kind: "Generic", DisplayName: "undocumented_entity.WIDTH" });
        Assert.Contains(result.UndocumentedItems, i => i is { Kind: "Port", DisplayName: "undocumented_entity.clk" });
        Assert.Contains(result.UndocumentedItems, i => i is { Kind: "Port", DisplayName: "undocumented_entity.y" });
    }

    /// <summary>
    ///     Validates that an undocumented package, its type, constant, component, and
    ///     subprogram each report a violation with the expected kind label.
    /// </summary>
    [Fact]
    public void Check_UndocumentedPackage_ReportsPackageAndMemberViolations()
    {
        // Arrange
        var generator = new VhdlGenerator(BuildOptions("undocumented.vhd"));
        generator.Parse(new InMemoryContext());

        // Act
        var result = generator.CheckDocumentationCoverage("Public");

        // Assert
        Assert.Contains(result.UndocumentedItems, i => i is { Kind: "Package", DisplayName: "undocumented_package" });
        Assert.Contains(result.UndocumentedItems, i => i is { Kind: "Type", DisplayName: "undocumented_package.undocumented_type_t" });
        Assert.Contains(result.UndocumentedItems, i => i is { Kind: "Constant", DisplayName: "undocumented_package.UNDOCUMENTED_CONSTANT" });
        Assert.Contains(result.UndocumentedItems, i => i is { Kind: "Component", DisplayName: "undocumented_package.undocumented_component" });
        Assert.Contains(result.UndocumentedItems, i => i is { Kind: "Subprogram", DisplayName: "undocumented_package.undocumented_function" });
    }

    /// <summary>
    ///     Validates the public-interface-only scope boundary: an undocumented
    ///     architecture-internal signal is never flagged, because architecture internals are
    ///     not parsed into the AST model at all — this is a deliberate v1 scope decision, not a
    ///     bug (see the design documentation and <see cref="DocumentationCoverageChecker"/> remarks).
    /// </summary>
    [Fact]
    public void Check_ArchitectureInternalSignal_IsNeverFlagged()
    {
        // Arrange — undocumented.vhd's architecture declares an intentionally undocumented
        // internal signal ("internal_undocumented_signal") that is not parsed by VhdlAstParser
        var generator = new VhdlGenerator(BuildOptions("undocumented.vhd"));
        generator.Parse(new InMemoryContext());

        // Act
        var result = generator.CheckDocumentationCoverage("Public");

        // Assert — no violation kind or display name references the internal signal at all;
        // only the architecture's own (documented) summary is checked, not its internals
        Assert.DoesNotContain(result.UndocumentedItems, i => i.DisplayName.Contains("internal_undocumented_signal", StringComparison.Ordinal));
        Assert.DoesNotContain(result.UndocumentedItems, i => i.Kind == "Signal");
    }

    /// <summary>
    ///     Validates that <c>Public</c>, <c>PublicAndProtected</c>, and <c>All</c> all produce an
    ///     identical result for VHDL, since it has no visibility/accessibility concept.
    /// </summary>
    [Fact]
    public void Check_AllThreeEnforceTiers_ProduceIdenticalResults()
    {
        // Arrange
        var generator = new VhdlGenerator(BuildOptions("undocumented.vhd"));
        generator.Parse(new InMemoryContext());

        // Act
        var publicResult = generator.CheckDocumentationCoverage("Public");
        var publicAndProtectedResult = generator.CheckDocumentationCoverage("PublicAndProtected");
        var allResult = generator.CheckDocumentationCoverage("All");

        // Assert
        Assert.Equal(publicResult.CheckedCount, publicAndProtectedResult.CheckedCount);
        Assert.Equal(publicResult.CheckedCount, allResult.CheckedCount);
        Assert.Equal(publicResult.UndocumentedCount, publicAndProtectedResult.UndocumentedCount);
        Assert.Equal(publicResult.UndocumentedCount, allResult.UndocumentedCount);
    }

    /// <summary>Validates that <see cref="VhdlGenerator.CheckDocumentationCoverage"/> throws when called before <see cref="VhdlGenerator.Parse"/>.</summary>
    [Fact]
    public void VhdlGenerator_CheckDocumentationCoverage_BeforeParse_ThrowsInvalidOperationException()
    {
        // Arrange
        var generator = new VhdlGenerator(BuildOptions("undocumented.vhd"));

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => generator.CheckDocumentationCoverage("Public"));
        Assert.Contains(nameof(VhdlGenerator.Parse), ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Validates that an invalid <c>--enforce-docs</c> value throws <see cref="ArgumentException"/>.</summary>
    [Fact]
    public void VhdlGenerator_CheckDocumentationCoverage_InvalidEnforceTier_ThrowsArgumentException()
    {
        // Arrange
        var generator = new VhdlGenerator(BuildOptions("undocumented.vhd"));
        generator.Parse(new InMemoryContext());

        // Act / Assert
        Assert.Throws<ArgumentException>(() => generator.CheckDocumentationCoverage("NotAVisibilityTier"));
    }

    /// <summary>Validates that <see cref="VhdlGeneratorOptions.EnforceDocsVisibility"/> is used when the method parameter is null or empty.</summary>
    [Fact]
    public void VhdlGenerator_CheckDocumentationCoverage_NullEnforceTier_FallsBackToOptionsValue()
    {
        // Arrange
        var options = BuildOptions("undocumented.vhd");
        options.EnforceDocsVisibility = "Public";
        var generator = new VhdlGenerator(options);
        generator.Parse(new InMemoryContext());

        // Act
        var result = generator.CheckDocumentationCoverage(null);

        // Assert
        Assert.True(result.HasViolations);
    }

    /// <summary>Validates that calling without a tier from either source throws <see cref="InvalidOperationException"/>.</summary>
    [Fact]
    public void VhdlGenerator_CheckDocumentationCoverage_NoTierConfigured_ThrowsInvalidOperationException()
    {
        // Arrange — EnforceDocsVisibility deliberately left null (disabled)
        var generator = new VhdlGenerator(BuildOptions("undocumented.vhd"));
        generator.Parse(new InMemoryContext());

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => generator.CheckDocumentationCoverage(null));
        Assert.Contains(nameof(VhdlGeneratorOptions.EnforceDocsVisibility), ex.Message, StringComparison.Ordinal);
    }
}
