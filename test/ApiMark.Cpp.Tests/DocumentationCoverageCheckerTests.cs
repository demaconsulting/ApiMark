using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.Cpp;
using Xunit;

namespace ApiMark.Cpp.Tests;

/// <summary>Unit tests for <see cref="DocumentationCoverageChecker"/> and <see cref="CppGenerator.CheckDocumentationCoverage"/>.</summary>
/// <remarks>
///     Uses the shared fixture headers under <c>ApiMark.Cpp.Fixtures/include/fixtures</c> —
///     notably <c>SampleClass.h</c>, whose public <c>Refresh()</c> method is intentionally
///     undocumented (mirroring <c>ApiMark.DotNet.Tests.DocumentationCoverageCheckerTests</c>'s
///     use of the equivalent .NET fixture), and <c>ProtectedMembersClass.h</c>/
///     <c>DeprecatedClass.h</c>, which are fully documented and used for tier/deprecated-filter
///     assertions.
/// </remarks>
public class DocumentationCoverageCheckerTests
{
    /// <summary>
    ///     Builds a <see cref="CppGeneratorOptions"/> pointing at the fixture include directory,
    ///     scoped to a single header via an explicit <c>ApiHeaderPatterns</c> entry so each test
    ///     only invokes clang against a small, deterministic file set.
    /// </summary>
    /// <param name="headerFileName">The single header file name (under <c>include/fixtures/</c>) to scope the parse to.</param>
    /// <param name="includeDeprecated">Whether deprecated declarations are included in the emission-level filter.</param>
    /// <returns>A configured <see cref="CppGeneratorOptions"/> for the named fixture header.</returns>
    private static CppGeneratorOptions BuildOptions(string headerFileName, bool includeDeprecated = false)
    {
        return new CppGeneratorOptions
        {
            LibraryName = "Fixtures",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
            ApiHeaderPatterns = [Path.Join(FixturePaths.GetFixtureNamespaceDir(), headerFileName).Replace('\\', '/')],
            IncludeDeprecated = includeDeprecated,
        };
    }

    /// <summary>Validates that a fully-documented header reports zero violations.</summary>
    [Fact]
    public void Check_FullyDocumentedHeader_ReportsZeroViolations()
    {
        // Arrange — ProtectedMembersClass.h is fully documented at every visibility tier
        var generator = new CppGenerator(BuildOptions("ProtectedMembersClass.h"));
        generator.Parse(new InMemoryContext());

        // Act
        var result = generator.CheckDocumentationCoverage("All");

        // Assert
        Assert.False(result.HasViolations);
        Assert.Empty(result.UndocumentedItems);
    }

    /// <summary>Validates that the real fixture header's intentionally undocumented method is reported.</summary>
    [Fact]
    public void Check_RealFixtureHeader_ReportsUndocumentedFunction()
    {
        // Arrange — SampleClass.Refresh() is intentionally undocumented in SampleClass.h
        var generator = new CppGenerator(BuildOptions("SampleClass.h"));
        generator.Parse(new InMemoryContext());

        // Act
        var result = generator.CheckDocumentationCoverage("Public");

        // Assert
        Assert.True(result.HasViolations);
        Assert.Contains(result.UndocumentedItems, i => i is { Kind: "Function", DisplayName: "fixtures::SampleClass::Refresh()" });
    }

    /// <summary>
    ///     Validates that each declaration kind (class, function, field, enum, enum value, and
    ///     type alias) is reported with its correct <c>Kind</c> label, using a fixture header
    ///     purpose-built with one undocumented declaration of every kind.
    /// </summary>
    [Fact]
    public void Check_VariousDeclarationKinds_ReportsExpectedKindLabels()
    {
        // Arrange — UndocumentedKindsFixture.h has one undocumented declaration of every kind
        var generator = new CppGenerator(BuildOptions("UndocumentedKindsFixture.h"));
        generator.Parse(new InMemoryContext());

        // Act
        var result = generator.CheckDocumentationCoverage("Public");

        // Assert — every kind label the checker can produce for class-scoped and
        // namespace-scoped declarations is actually observed
        var kinds = result.UndocumentedItems.Select(i => i.Kind).ToHashSet();
        Assert.Contains("Class", kinds);
        Assert.Contains("Function", kinds);
        Assert.Contains("Field", kinds);
        Assert.Contains("Enum", kinds);
        Assert.Contains("EnumValue", kinds);
        Assert.Contains("TypeAlias", kinds);
    }

    /// <summary>
    ///     Validates that undocumented overloaded constructors are each reported with a distinct,
    ///     unambiguous <c>DisplayName</c> that includes the parameter signature, so overloads of
    ///     the same name do not collapse into a single indistinguishable violation.
    /// </summary>
    [Fact]
    public void Check_OverloadedUndocumentedConstructor_ReportsDistinctParameterSignature()
    {
        // Arrange — UndocumentedKindsClass has a documented default constructor and an
        // undocumented single-string-argument constructor overload
        var generator = new CppGenerator(BuildOptions("UndocumentedKindsFixture.h"));
        generator.Parse(new InMemoryContext());

        // Act
        var result = generator.CheckDocumentationCoverage("Public");

        // Assert — the undocumented constructor overload is reported with its parameter
        // signature appended, distinguishing it from the (documented, unreported)
        // no-argument overload
        Assert.Contains(
            result.UndocumentedItems,
            i => i.Kind == "Function"
                && i.DisplayName.StartsWith("fixtures::UndocumentedKindsClass::UndocumentedKindsClass(", StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.UndocumentedItems,
            i => i.DisplayName == "fixtures::UndocumentedKindsClass::UndocumentedKindsClass");
    }

    /// <summary>
    ///     Validates that undocumented overloaded methods are each reported with a distinct,
    ///     unambiguous <c>DisplayName</c> that includes the parameter signature.
    /// </summary>
    [Fact]
    public void Check_OverloadedUndocumentedMethod_ReportsDistinctParameterSignature()
    {
        // Arrange — UndocumentedKindsClass has a documented DoWork() and an undocumented
        // DoWork(int) overload
        var generator = new CppGenerator(BuildOptions("UndocumentedKindsFixture.h"));
        generator.Parse(new InMemoryContext());

        // Act
        var result = generator.CheckDocumentationCoverage("Public");

        // Assert — the undocumented DoWork(int) overload carries its parameter type in the
        // reported DisplayName, distinguishing it from the documented DoWork() overload,
        // which is never reported
        Assert.Contains(
            result.UndocumentedItems,
            i => i.Kind == "Function" && i.DisplayName == "fixtures::UndocumentedKindsClass::DoWork(int)");
        Assert.DoesNotContain(
            result.UndocumentedItems,
            i => i.DisplayName == "fixtures::UndocumentedKindsClass::DoWork()");
    }

    /// <summary>
    ///     Validates that undocumented overloaded free functions are each reported with a
    ///     distinct, unambiguous <c>DisplayName</c> that includes the parameter signature.
    /// </summary>
    [Fact]
    public void Check_OverloadedUndocumentedFreeFunction_ReportsDistinctParameterSignature()
    {
        // Arrange — both UndocumentedFreeFunction() and UndocumentedFreeFunction(int) are
        // undocumented overloads of the same free function name
        var generator = new CppGenerator(BuildOptions("UndocumentedKindsFixture.h"));
        generator.Parse(new InMemoryContext());

        // Act
        var result = generator.CheckDocumentationCoverage("Public");

        // Assert — both overloads are reported, each with a distinct parameter-signature suffix
        Assert.Contains(
            result.UndocumentedItems,
            i => i.Kind == "Function" && i.DisplayName == "fixtures::UndocumentedFreeFunction()");
        Assert.Contains(
            result.UndocumentedItems,
            i => i.Kind == "Function" && i.DisplayName == "fixtures::UndocumentedFreeFunction(int)");
    }

    /// <summary>Validates that <see cref="ApiVisibility.PublicAndProtected"/> surfaces protected members not visible at <see cref="ApiVisibility.Public"/>.</summary>
    [Fact]
    public void Check_PublicAndProtectedTier_SurfacesProtectedMembersNotSeenAtPublic()
    {
        // Arrange — ProtectedMembersClass.h is fully documented, so re-purpose SampleClass.h,
        // whose protected OnNameChanged() member IS documented — assert instead that the
        // checked count increases when moving from Public to PublicAndProtected, proving the
        // protected member enters the scan (rather than asserting a specific violation).
        var generator = new CppGenerator(BuildOptions("SampleClass.h"));
        generator.Parse(new InMemoryContext());

        // Act
        var publicResult = generator.CheckDocumentationCoverage("Public");
        var publicAndProtectedResult = generator.CheckDocumentationCoverage("PublicAndProtected");

        // Assert
        Assert.True(publicAndProtectedResult.CheckedCount > publicResult.CheckedCount);
    }

    /// <summary>Validates that deprecated declarations are skipped by default and included when requested.</summary>
    [Fact]
    public void Check_DeprecatedClass_FilteredByIncludeDeprecatedOption()
    {
        // Arrange — DeprecatedClass.h's class and method are both [[deprecated]] but fully documented
        var excluding = new CppGenerator(BuildOptions("DeprecatedClass.h", includeDeprecated: false));
        excluding.Parse(new InMemoryContext());
        var including = new CppGenerator(BuildOptions("DeprecatedClass.h", includeDeprecated: true));
        including.Parse(new InMemoryContext());

        // Act
        var excludingResult = excluding.CheckDocumentationCoverage("Public");
        var includingResult = including.CheckDocumentationCoverage("Public");

        // Assert — deprecated declarations excluded entirely from the scan when IncludeDeprecated is false
        Assert.Equal(0, excludingResult.CheckedCount);
        Assert.True(includingResult.CheckedCount > 0);
    }

    /// <summary>Validates that <see cref="CppGenerator.CheckDocumentationCoverage"/> throws when called before <see cref="CppGenerator.Parse"/>.</summary>
    [Fact]
    public void CppGenerator_CheckDocumentationCoverage_BeforeParse_ThrowsInvalidOperationException()
    {
        // Arrange
        var generator = new CppGenerator(BuildOptions("SampleClass.h"));

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => generator.CheckDocumentationCoverage("Public"));
        Assert.Contains(nameof(CppGenerator.Parse), ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Validates that an invalid <c>--enforce-docs</c> value throws <see cref="ArgumentException"/>.</summary>
    [Fact]
    public void CppGenerator_CheckDocumentationCoverage_InvalidEnforceTier_ThrowsArgumentException()
    {
        // Arrange
        var generator = new CppGenerator(BuildOptions("SampleClass.h"));
        generator.Parse(new InMemoryContext());

        // Act / Assert
        Assert.Throws<ArgumentException>(() => generator.CheckDocumentationCoverage("NotAVisibilityTier"));
    }

    /// <summary>Validates that <see cref="CppGeneratorOptions.EnforceDocsVisibility"/> is used when the method parameter is null or empty.</summary>
    [Fact]
    public void CppGenerator_CheckDocumentationCoverage_NullEnforceTier_FallsBackToOptionsValue()
    {
        // Arrange
        var options = BuildOptions("SampleClass.h");
        options.EnforceDocsVisibility = ApiVisibility.Public;
        var generator = new CppGenerator(options);
        generator.Parse(new InMemoryContext());

        // Act
        var result = generator.CheckDocumentationCoverage(null);

        // Assert
        Assert.True(result.HasViolations);
    }

    /// <summary>
    ///     Validates that an empty-string <c>enforceTier</c> (as opposed to <see langword="null"/>)
    ///     falls back to <see cref="CppGeneratorOptions.EnforceDocsVisibility"/> identically,
    ///     confirming the <c>string.IsNullOrEmpty</c> boundary treats both the same way.
    /// </summary>
    [Fact]
    public void CppGenerator_CheckDocumentationCoverage_EmptyStringEnforceTier_FallsBackToOptionsValue()
    {
        // Arrange
        var options = BuildOptions("SampleClass.h");
        options.EnforceDocsVisibility = ApiVisibility.Public;
        var generator = new CppGenerator(options);
        generator.Parse(new InMemoryContext());

        // Act
        var result = generator.CheckDocumentationCoverage(string.Empty);

        // Assert
        Assert.True(result.HasViolations);
    }

    /// <summary>Validates that calling without a tier from either source throws <see cref="InvalidOperationException"/>.</summary>
    [Fact]
    public void CppGenerator_CheckDocumentationCoverage_NoTierConfigured_ThrowsInvalidOperationException()
    {
        // Arrange — EnforceDocsVisibility deliberately left null (disabled)
        var generator = new CppGenerator(BuildOptions("SampleClass.h"));
        generator.Parse(new InMemoryContext());

        // Act / Assert
        var ex = Assert.Throws<InvalidOperationException>(() => generator.CheckDocumentationCoverage(null));
        Assert.Contains(nameof(CppGeneratorOptions.EnforceDocsVisibility), ex.Message, StringComparison.Ordinal);
    }
}
