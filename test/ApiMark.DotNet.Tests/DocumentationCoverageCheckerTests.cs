using ApiMark.DotNet;
using Mono.Cecil;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Unit tests for <see cref="DocumentationCoverageChecker"/>.</summary>
public class DocumentationCoverageCheckerTests
{
    /// <summary>
    ///     Writes a minimal XML doc file containing <paramref name="membersXml"/> and returns the
    ///     path so the caller can clean it up after use.
    /// </summary>
    /// <param name="membersXml">Raw XML to embed inside the &lt;members&gt; element.</param>
    /// <returns>Path to the temporary XML documentation file.</returns>
    private static string WriteXmlDoc(string membersXml)
    {
        var path = Path.GetTempFileName();
        var xml = $"""
            <?xml version="1.0"?>
            <doc>
              <assembly><name>TestAssembly</name></assembly>
              <members>
                {membersXml}
              </members>
            </doc>
            """;
        File.WriteAllText(path, xml);
        return path;
    }

    /// <summary>Loads the fixture assembly used by every test in this class.</summary>
    /// <returns>A freshly-read <see cref="AssemblyDefinition"/> for the fixture DLL.</returns>
    private static AssemblyDefinition LoadFixtureAssembly() =>
        AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());

    /// <summary>
    ///     Builds exclude patterns that remove every top-level type in <paramref name="assembly"/>
    ///     except those named in <paramref name="keepFullNames"/>, scoping a scan down to a small,
    ///     deterministic subset of the fixture assembly without modifying the shared fixtures project.
    /// </summary>
    /// <param name="assembly">The fixture assembly to scope.</param>
    /// <param name="keepFullNames">Fully-qualified type names to keep visible to the scan.</param>
    /// <returns>Exact-match exclude patterns for every other top-level type.</returns>
    private static List<string> ExcludeAllExcept(AssemblyDefinition assembly, params string[] keepFullNames) =>
        assembly.MainModule.Types
            .Where(t => !t.IsNested)
            .Select(t => t.FullName.Replace('/', '.'))
            .Where(fullName => !keepFullNames.Contains(fullName))
            .ToList();

    /// <summary>Validates that a fully-documented, narrowly-scoped subset of the assembly reports zero violations.</summary>
    [Fact]
    public void Check_FullyDocumentedScope_ReportsZeroViolations()
    {
        // Arrange — hand-written XML doc documents the type, its property, and its (compiler
        // generated implicit) parameterless constructor
        using var assembly = LoadFixtureAssembly();
        var docPath = WriteXmlDoc("""
            <member name="T:ApiMark.DotNet.Fixtures.ExcludedSample.ExcludedSampleClass">
              <summary>A class in an isolated namespace.</summary>
            </member>
            <member name="P:ApiMark.DotNet.Fixtures.ExcludedSample.ExcludedSampleClass.Value">
              <summary>Gets or sets a value.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.ExcludedSample.ExcludedSampleClass.#ctor">
              <summary>Initializes a new instance.</summary>
            </member>
            """);
        try
        {
            var xmlDocs = new XmlDocReader(docPath);
            var excludePatterns = ExcludeAllExcept(assembly, "ApiMark.DotNet.Fixtures.ExcludedSample.ExcludedSampleClass");

            // Act
            var result = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.Public, includeObsolete: false, excludePatterns);

            // Assert — type + Value property + implicit constructor
            Assert.Equal(3, result.CheckedCount);
            Assert.Equal(0, result.UndocumentedCount);
            Assert.False(result.HasViolations);
            Assert.Empty(result.UndocumentedItems);
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    /// <summary>Validates that a method missing a summary in the real compiled fixture XML doc is reported.</summary>
    [Fact]
    public void Check_RealFixtureDoc_ReportsUndocumentedMethod()
    {
        // Arrange — SampleClass.Refresh is an intentionally undocumented public method in the
        // real, compiler-generated fixture XML doc (see SampleClass.cs); the compiler-generated
        // implicit parameterless constructor is likewise never emitted with a summary
        using var assembly = LoadFixtureAssembly();
        var xmlDocs = new XmlDocReader(FixturePaths.GetFixtureXmlDoc());
        var excludePatterns = ExcludeAllExcept(assembly, "ApiMark.DotNet.Fixtures.SampleClass");

        // Act
        var result = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.Public, includeObsolete: false, excludePatterns);

        // Assert — type + Name, Title, DefaultName, NameChanged, GetGreeting, Reset, Refresh, ctor
        Assert.Equal(9, result.CheckedCount);
        Assert.Equal(2, result.UndocumentedCount);
        Assert.Contains(result.UndocumentedItems, i => i is { Kind: "Method", DisplayName: "ApiMark.DotNet.Fixtures.SampleClass.Refresh()" });
        Assert.Contains(result.UndocumentedItems, i => i is { Kind: "Method", DisplayName: "ApiMark.DotNet.Fixtures.SampleClass.SampleClass()" });
    }

    /// <summary>Validates that a type missing a summary is reported as an <see cref=""Type""/> violation.</summary>
    [Fact]
    public void Check_TypeMissingSummary_ReportsTypeViolation()
    {
        // Arrange — hand-written XML doc documents both members and the implicit constructor
        // but omits the type summary
        using var assembly = LoadFixtureAssembly();
        var docPath = WriteXmlDoc("""
            <member name="P:ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass.Value">
              <summary>Gets or sets a value.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass.Compute(System.Int32)">
              <summary>Computes a result from the given input.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass.#ctor">
              <summary>Initializes a new instance.</summary>
            </member>
            """);
        try
        {
            var xmlDocs = new XmlDocReader(docPath);
            var excludePatterns = ExcludeAllExcept(assembly, "ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass");

            // Act
            var result = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.Public, includeObsolete: false, excludePatterns);

            // Assert — type + Value + Compute + ctor
            Assert.Equal(4, result.CheckedCount);
            var violation = Assert.Single(result.UndocumentedItems);
            Assert.Equal("Type", violation.Kind);
            Assert.Equal("ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass", violation.DisplayName);
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    /// <summary>Validates that a method missing a summary is reported as an <see cref=""Method""/> violation.</summary>
    [Fact]
    public void Check_MethodMissingSummary_ReportsMethodViolation()
    {
        // Arrange — hand-written XML doc documents the type, property, and implicit constructor
        // but omits the method
        using var assembly = LoadFixtureAssembly();
        var docPath = WriteXmlDoc("""
            <member name="T:ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass">
              <summary>A class in a child namespace used to verify hierarchical namespace output.</summary>
            </member>
            <member name="P:ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass.Value">
              <summary>Gets or sets a value.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass.#ctor">
              <summary>Initializes a new instance.</summary>
            </member>
            """);
        try
        {
            var xmlDocs = new XmlDocReader(docPath);
            var excludePatterns = ExcludeAllExcept(assembly, "ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass");

            // Act
            var result = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.Public, includeObsolete: false, excludePatterns);

            // Assert
            Assert.Equal(4, result.CheckedCount);
            var violation = Assert.Single(result.UndocumentedItems);
            Assert.Equal("Method", violation.Kind);
            Assert.Equal("ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass.Compute(int)", violation.DisplayName);
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    /// <summary>Validates that a property missing a summary is reported as an <see cref=""Property""/> violation.</summary>
    [Fact]
    public void Check_PropertyMissingSummary_ReportsPropertyViolation()
    {
        // Arrange — hand-written XML doc documents the type, method, and implicit constructor
        // but omits the property
        using var assembly = LoadFixtureAssembly();
        var docPath = WriteXmlDoc("""
            <member name="T:ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass">
              <summary>A class in a child namespace used to verify hierarchical namespace output.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass.Compute(System.Int32)">
              <summary>Computes a result from the given input.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass.#ctor">
              <summary>Initializes a new instance.</summary>
            </member>
            """);
        try
        {
            var xmlDocs = new XmlDocReader(docPath);
            var excludePatterns = ExcludeAllExcept(assembly, "ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass");

            // Act
            var result = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.Public, includeObsolete: false, excludePatterns);

            // Assert
            Assert.Equal(4, result.CheckedCount);
            var violation = Assert.Single(result.UndocumentedItems);
            Assert.Equal("Property", violation.Kind);
            Assert.Equal("ApiMark.DotNet.Fixtures.Inner.InnerNamespaceClass.Value", violation.DisplayName);
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    /// <summary>Validates that a field missing a summary is reported as an <see cref=""Field""/> violation.</summary>
    [Fact]
    public void Check_FieldMissingSummary_ReportsFieldViolation()
    {
        // Arrange — hand-written XML doc documents everything on SampleClass (including its
        // implicit constructor) except the DefaultName constant field
        using var assembly = LoadFixtureAssembly();
        var docPath = WriteXmlDoc("""
            <member name="T:ApiMark.DotNet.Fixtures.SampleClass">
              <summary>A sample class for testing the API generator.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.SampleClass.#ctor">
              <summary>Initializes a new instance.</summary>
            </member>
            <member name="P:ApiMark.DotNet.Fixtures.SampleClass.Name">
              <summary>Gets or sets the name.</summary>
            </member>
            <member name="P:ApiMark.DotNet.Fixtures.SampleClass.Title">
              <summary>Gets or sets the title.</summary>
            </member>
            <member name="E:ApiMark.DotNet.Fixtures.SampleClass.NameChanged">
              <summary>Occurs when the name changes.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.SampleClass.GetGreeting(System.String)">
              <summary>Gets a greeting for the specified name.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.SampleClass.Reset">
              <summary>Resets this instance to its default state.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.SampleClass.Refresh">
              <summary>Refreshes this instance.</summary>
            </member>
            """);
        try
        {
            var xmlDocs = new XmlDocReader(docPath);
            var excludePatterns = ExcludeAllExcept(assembly, "ApiMark.DotNet.Fixtures.SampleClass");

            // Act
            var result = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.Public, includeObsolete: false, excludePatterns);

            // Assert
            Assert.Equal(9, result.CheckedCount);
            var violation = Assert.Single(result.UndocumentedItems);
            Assert.Equal("Field", violation.Kind);
            Assert.Equal("ApiMark.DotNet.Fixtures.SampleClass.DefaultName", violation.DisplayName);
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    /// <summary>Validates that an event missing a summary is reported as an <see cref=""Event""/> violation.</summary>
    [Fact]
    public void Check_EventMissingSummary_ReportsEventViolation()
    {
        // Arrange — hand-written XML doc documents everything on SampleClass (including its
        // implicit constructor) except the NameChanged event
        using var assembly = LoadFixtureAssembly();
        var docPath = WriteXmlDoc("""
            <member name="T:ApiMark.DotNet.Fixtures.SampleClass">
              <summary>A sample class for testing the API generator.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.SampleClass.#ctor">
              <summary>Initializes a new instance.</summary>
            </member>
            <member name="P:ApiMark.DotNet.Fixtures.SampleClass.Name">
              <summary>Gets or sets the name.</summary>
            </member>
            <member name="P:ApiMark.DotNet.Fixtures.SampleClass.Title">
              <summary>Gets or sets the title.</summary>
            </member>
            <member name="F:ApiMark.DotNet.Fixtures.SampleClass.DefaultName">
              <summary>Gets the default name constant.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.SampleClass.GetGreeting(System.String)">
              <summary>Gets a greeting for the specified name.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.SampleClass.Reset">
              <summary>Resets this instance to its default state.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.SampleClass.Refresh">
              <summary>Refreshes this instance.</summary>
            </member>
            """);
        try
        {
            var xmlDocs = new XmlDocReader(docPath);
            var excludePatterns = ExcludeAllExcept(assembly, "ApiMark.DotNet.Fixtures.SampleClass");

            // Act
            var result = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.Public, includeObsolete: false, excludePatterns);

            // Assert
            Assert.Equal(9, result.CheckedCount);
            var violation = Assert.Single(result.UndocumentedItems);
            Assert.Equal("Event", violation.Kind);
            Assert.Equal("ApiMark.DotNet.Fixtures.SampleClass.NameChanged", violation.DisplayName);
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    /// <summary>Validates that nested types are recursed into and checked using the same enforcement tier.</summary>
    [Fact]
    public void Check_NestedTypeFullyDocumented_ChecksBothLevelsWithZeroViolations()
    {
        // Arrange — real compiled fixture XML doc fully documents OuterClass and its nested Inner
        // type, both of which declare explicit (fully documented) constructors
        using var assembly = LoadFixtureAssembly();
        var xmlDocs = new XmlDocReader(FixturePaths.GetFixtureXmlDoc());
        var excludePatterns = ExcludeAllExcept(assembly, "ApiMark.DotNet.Fixtures.OuterClass");

        // Act
        var result = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.Public, includeObsolete: false, excludePatterns);

        // Assert — OuterClass (type + Value + ctor) and Inner (type + InnerValue + ctor)
        Assert.Equal(6, result.CheckedCount);
        Assert.Equal(0, result.UndocumentedCount);
    }

    /// <summary>Validates that a missing summary on a nested type's member is still reported during recursion.</summary>
    [Fact]
    public void Check_NestedTypeMemberMissingSummary_ReportsViolationOnNestedType()
    {
        // Arrange — hand-written XML doc documents everything except the nested Inner constructor
        using var assembly = LoadFixtureAssembly();
        var docPath = WriteXmlDoc("""
            <member name="T:ApiMark.DotNet.Fixtures.OuterClass">
              <summary>An outer class containing a public nested type for testing nested type page generation.</summary>
            </member>
            <member name="P:ApiMark.DotNet.Fixtures.OuterClass.Value">
              <summary>Gets the outer value.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.OuterClass.#ctor(System.Int32)">
              <summary>Initializes a new instance with the specified value.</summary>
            </member>
            <member name="T:ApiMark.DotNet.Fixtures.OuterClass.Inner">
              <summary>A public nested class inside OuterClass.</summary>
            </member>
            <member name="P:ApiMark.DotNet.Fixtures.OuterClass.Inner.InnerValue">
              <summary>Gets the inner value.</summary>
            </member>
            """);
        try
        {
            var xmlDocs = new XmlDocReader(docPath);
            var excludePatterns = ExcludeAllExcept(assembly, "ApiMark.DotNet.Fixtures.OuterClass");

            // Act
            var result = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.Public, includeObsolete: false, excludePatterns);

            // Assert
            Assert.Equal(6, result.CheckedCount);
            var violation = Assert.Single(result.UndocumentedItems);
            Assert.Equal("Method", violation.Kind);
            Assert.Equal("ApiMark.DotNet.Fixtures.OuterClass.Inner.Inner(int)", violation.DisplayName);
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    /// <summary>
    ///     Validates that an exclude pattern matching a nested type's fully-qualified name is
    ///     re-applied during nested-type recursion, so the excluded nested type (and its members)
    ///     are skipped entirely rather than being counted or reported.
    /// </summary>
    [Fact]
    public void Check_ExcludePatternMatchesNestedType_SkipsNestedTypeDuringRecursion()
    {
        // Arrange — real compiled fixture XML doc fully documents OuterClass and its nested
        // Inner type; explicitly exclude the nested Inner type in addition to every other
        // top-level type, leaving only OuterClass itself in scope
        using var assembly = LoadFixtureAssembly();
        var xmlDocs = new XmlDocReader(FixturePaths.GetFixtureXmlDoc());
        var excludePatterns = ExcludeAllExcept(assembly, "ApiMark.DotNet.Fixtures.OuterClass")
            .Append("ApiMark.DotNet.Fixtures.OuterClass.Inner")
            .ToList();

        // Act
        var result = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.Public, includeObsolete: false, excludePatterns);

        // Assert — only OuterClass itself (type + Value + ctor) is checked; the nested Inner
        // type and its members are excluded and must not appear in the checked count or results
        Assert.Equal(3, result.CheckedCount);
        Assert.Equal(0, result.UndocumentedCount);
        Assert.DoesNotContain(result.UndocumentedItems, item => item.DisplayName.Contains("Inner"));
    }

    /// <summary>Validates that the enforcement visibility tier is independent of any emission visibility tier.</summary>
    [Fact]
    public void Check_PublicAndProtectedTier_IncludesProtectedMembersNotSeenAtPublicTier()
    {
        // Arrange — hand-written XML doc documents the type, implicit constructor, and public
        // property only, leaving the protected property and method undocumented
        using var assembly = LoadFixtureAssembly();
        var docPath = WriteXmlDoc("""
            <member name="T:ApiMark.DotNet.Fixtures.ProtectedMembersClass">
              <summary>A class for testing protected member visibility filtering.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.ProtectedMembersClass.#ctor">
              <summary>Initializes a new instance.</summary>
            </member>
            <member name="P:ApiMark.DotNet.Fixtures.ProtectedMembersClass.PublicProp">
              <summary>Gets or sets the public property.</summary>
            </member>
            """);
        try
        {
            var xmlDocs = new XmlDocReader(docPath);
            var excludePatterns = ExcludeAllExcept(assembly, "ApiMark.DotNet.Fixtures.ProtectedMembersClass");

            // Act
            var publicResult = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.Public, includeObsolete: false, excludePatterns);
            var publicAndProtectedResult = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.PublicAndProtected, includeObsolete: false, excludePatterns);

            // Assert — Public tier only sees the type, ctor, and PublicProp, all documented
            Assert.Equal(3, publicResult.CheckedCount);
            Assert.Equal(0, publicResult.UndocumentedCount);

            // PublicAndProtected tier additionally sees ProtectedProp and ProtectedMethod, both undocumented
            Assert.Equal(5, publicAndProtectedResult.CheckedCount);
            Assert.Equal(2, publicAndProtectedResult.UndocumentedCount);
            Assert.Contains(publicAndProtectedResult.UndocumentedItems, i => i is { Kind: "Property", DisplayName: "ApiMark.DotNet.Fixtures.ProtectedMembersClass.ProtectedProp" });
            Assert.Contains(publicAndProtectedResult.UndocumentedItems, i => i is { Kind: "Method", DisplayName: "ApiMark.DotNet.Fixtures.ProtectedMembersClass.ProtectedMethod(int)" });
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    /// <summary>Validates that exclude patterns suppress a type entirely from the scan, regardless of documentation state.</summary>
    [Fact]
    public void Check_ExcludePatternMatchesType_TypeIsNotChecked()
    {
        // Arrange — exclude every top-level type in the assembly
        using var assembly = LoadFixtureAssembly();
        var xmlDocs = new XmlDocReader(FixturePaths.GetFixtureXmlDoc());
        var excludePatterns = ExcludeAllExcept(assembly);

        // Act
        var result = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.All, includeObsolete: true, excludePatterns);

        // Assert
        Assert.Equal(0, result.CheckedCount);
        Assert.Equal(0, result.UndocumentedCount);
    }

    /// <summary>Validates that obsolete types are skipped from the scan by default.</summary>
    [Fact]
    public void Check_IncludeObsoleteFalse_SkipsObsoleteType()
    {
        // Arrange
        using var assembly = LoadFixtureAssembly();
        var xmlDocs = new XmlDocReader(FixturePaths.GetFixtureXmlDoc());
        var excludePatterns = ExcludeAllExcept(assembly, "ApiMark.DotNet.Fixtures.ObsoleteClass");

        // Act
        var result = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.Public, includeObsolete: false, excludePatterns);

        // Assert
        Assert.Equal(0, result.CheckedCount);
        Assert.Equal(0, result.UndocumentedCount);
    }

    /// <summary>Validates that obsolete types are scanned (and can be reported as violations) when opted in.</summary>
    [Fact]
    public void Check_IncludeObsoleteTrue_ScansObsoleteTypeAndReportsMissingSummary()
    {
        // Arrange — hand-written XML doc documents the obsolete type and its implicit
        // constructor but omits the OldMethod summary
        using var assembly = LoadFixtureAssembly();
        var docPath = WriteXmlDoc("""
            <member name="T:ApiMark.DotNet.Fixtures.ObsoleteClass">
              <summary>An obsolete class for testing obsolete member filtering.</summary>
            </member>
            <member name="M:ApiMark.DotNet.Fixtures.ObsoleteClass.#ctor">
              <summary>Initializes a new instance.</summary>
            </member>
            """);
        try
        {
            var xmlDocs = new XmlDocReader(docPath);
            var excludePatterns = ExcludeAllExcept(assembly, "ApiMark.DotNet.Fixtures.ObsoleteClass");

            // Act
            var result = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.Public, includeObsolete: true, excludePatterns);

            // Assert — type + ctor + OldMethod
            Assert.Equal(3, result.CheckedCount);
            var violation = Assert.Single(result.UndocumentedItems);
            Assert.Equal("Method", violation.Kind);
            Assert.Equal("ApiMark.DotNet.Fixtures.ObsoleteClass.OldMethod()", violation.DisplayName);
        }
        finally
        {
            File.Delete(docPath);
        }
    }

    /// <summary>Validates that a NamespaceDoc carrier type is never checked, even under the broadest visibility tier.</summary>
    [Fact]
    public void Check_NamespaceDocCarrier_IsNeverChecked()
    {
        // Arrange — the RemarksOnly namespace contains an internal NamespaceDoc carrier type
        // alongside an ordinary public class; scan at the broadest (All) tier so visibility
        // alone would not otherwise exclude the internal carrier type
        using var assembly = LoadFixtureAssembly();
        var xmlDocs = new XmlDocReader(FixturePaths.GetFixtureXmlDoc());
        var excludePatterns = ExcludeAllExcept(
            assembly,
            "ApiMark.DotNet.Fixtures.Inner.RemarksOnly.NamespaceDoc",
            "ApiMark.DotNet.Fixtures.Inner.RemarksOnly.RemarksOnlyNamespaceClass");

        // Act
        var result = DocumentationCoverageChecker.Check(assembly, xmlDocs, ApiVisibility.All, includeObsolete: true, excludePatterns);

        // Assert — only RemarksOnlyNamespaceClass (type + Value property + implicit ctor) is
        // checked; the NamespaceDoc carrier is always excluded regardless of visibility tier.
        // The compiler-generated implicit constructor is never emitted with a summary, so it
        // is the sole (expected) violation.
        Assert.Equal(3, result.CheckedCount);
        var violation = Assert.Single(result.UndocumentedItems);
        Assert.Equal("Method", violation.Kind);
        Assert.Equal("ApiMark.DotNet.Fixtures.Inner.RemarksOnly.RemarksOnlyNamespaceClass.RemarksOnlyNamespaceClass()", violation.DisplayName);
    }
}
