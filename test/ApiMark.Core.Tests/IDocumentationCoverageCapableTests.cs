using ApiMark.Core;
using Xunit;

namespace ApiMark.Core.Tests;

/// <summary>
///     Verifies the <see cref="IDocumentationCoverageCapable"/> interface contract.
///     These tests confirm that the interface can be implemented and invoked through
///     an interface reference, and that <see cref="DocumentationCoverageResult"/> and
///     <see cref="UndocumentedApiItem"/> expose the expected shape.
/// </summary>
public sealed class IDocumentationCoverageCapableTests
{
    /// <summary>
    ///     Verifies that a minimal stub implementation of
    ///     <see cref="IDocumentationCoverageCapable"/> can be invoked through the
    ///     interface reference and returns the expected result.
    /// </summary>
    [Fact]
    public void ApiMarkCore_DocumentationCoverageContract_SupportedLanguage_CanBeInvoked()
    {
        // Arrange: construct a stub via the interface reference
        IDocumentationCoverageCapable capable = new StubDocumentationCoverageCapable();

        // Act: invoke through the interface — this validates the full dispatch path
        var result = capable.CheckDocumentationCoverage("Public");

        // Assert: the stub's canned result is returned unchanged
        Assert.Equal(1, result.CheckedCount);
        Assert.True(result.HasViolations);
    }

    /// <summary>
    ///     Verifies that <see cref="DocumentationCoverageResult"/> correctly derives
    ///     <see cref="DocumentationCoverageResult.UndocumentedCount"/> and
    ///     <see cref="DocumentationCoverageResult.HasViolations"/> from the supplied item list.
    /// </summary>
    [Fact]
    public void DocumentationCoverageResult_WithUndocumentedItems_DerivesCountsCorrectly()
    {
        // Arrange: build a result with two undocumented items
        var items = new[]
        {
            new UndocumentedApiItem("Type", "Sample.Widget"),
            new UndocumentedApiItem("Method", "Sample.Widget.Refresh")
        };

        // Act: construct the result
        var result = new DocumentationCoverageResult(items, checkedCount: 10);

        // Assert: derived counts and flags reflect the supplied items
        Assert.Equal(10, result.CheckedCount);
        Assert.Equal(2, result.UndocumentedCount);
        Assert.True(result.HasViolations);
        Assert.Same(items, result.UndocumentedItems);
    }

    /// <summary>
    ///     Verifies that a <see cref="DocumentationCoverageResult"/> with no undocumented
    ///     items reports <c>HasViolations</c> as <see langword="false"/>.
    /// </summary>
    [Fact]
    public void DocumentationCoverageResult_WithNoUndocumentedItems_HasViolationsIsFalse()
    {
        // Arrange / Act: build a result with an empty undocumented-items list
        var result = new DocumentationCoverageResult([], checkedCount: 5);

        // Assert: no violations were found
        Assert.Equal(0, result.UndocumentedCount);
        Assert.False(result.HasViolations);
    }

    /// <summary>
    ///     Stub implementation of <see cref="IDocumentationCoverageCapable"/> that
    ///     returns a fixed result, used to verify the interface is invocable.
    /// </summary>
    private sealed class StubDocumentationCoverageCapable : IDocumentationCoverageCapable
    {
        /// <summary>
        ///     Returns a fixed <see cref="DocumentationCoverageResult"/> regardless of
        ///     <paramref name="enforceTier"/>, confirming the method signature matches
        ///     the interface contract.
        /// </summary>
        /// <param name="enforceTier">Not used.</param>
        /// <returns>A fixed result with one undocumented item.</returns>
        public DocumentationCoverageResult CheckDocumentationCoverage(string? enforceTier) =>
            new([new UndocumentedApiItem("Type", "Stub.Type")], checkedCount: 1);
    }
}
