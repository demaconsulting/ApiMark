// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using ApiMark.DotNet;
using Mono.Cecil;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Unit tests for <see cref="TypeLinkResolver"/>.</summary>
public class TypeLinkResolverTests : IDisposable
{
    private readonly AssemblyDefinition _assembly;

    /// <summary>Initializes the test fixture by loading the fixture assembly.</summary>
    public TypeLinkResolverTests()
    {
        _assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
    }

    /// <summary>Disposes the loaded assembly after each test.</summary>
    public void Dispose() => _assembly.Dispose();

    /// <summary>Validates that a null type reference returns an empty string.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_NullTypeRef_ReturnsEmptyString()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();

        // Act
        var result = resolver.Linkify(null!, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    /// <summary>Validates that <c>System.Int32</c> is resolved to the C# alias "int".</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_Int32_ReturnsCSharpAlias()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var typeRef = _assembly.MainModule.TypeSystem.Int32;

        // Act
        var result = resolver.Linkify(typeRef, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal("int", result);
    }

    /// <summary>Validates that <c>System.String</c> is resolved to the C# alias "string".</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_StringType_ReturnsCSharpAlias()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var typeRef = _assembly.MainModule.TypeSystem.String;

        // Act
        var result = resolver.Linkify(typeRef, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Equal("string", result);
    }

    /// <summary>Validates that an intra-assembly type generates a Markdown link when generateLinks is true.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_GenerateLinksTrue_IntraAssemblyType_ReturnsMarkdownLink()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: true);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var typeDef = _assembly.MainModule.Types.First(t => t.Name == "SampleClass");

        // Act
        var result = resolver.Linkify(typeDef, "ApiMark.DotNet.Fixtures", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.Contains("[", result, StringComparison.Ordinal);
    }

    /// <summary>Validates that an intra-assembly type returns plain text when generateLinks is false.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_GenerateLinksFalse_IntraAssemblyType_ReturnsPlainText()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: false);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var typeDef = _assembly.MainModule.Types.First(t => t.Name == "SampleClass");

        // Act
        var result = resolver.Linkify(typeDef, "ApiMark.DotNet.Fixtures", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert
        Assert.DoesNotContain("[", result, StringComparison.Ordinal);
    }

    /// <summary>Validates that a nullable generic type parameter (<c>T?</c>) appends a <c>?</c> suffix.</summary>
    [Fact]
    public void TypeLinkResolver_Linkify_NullableGenericParameter_AppendsQuestionMark()
    {
        // Arrange: obtain the T generic parameter from SampleGenericClass<T>
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var genericClass = _assembly.MainModule.Types.First(t => t.Name == "SampleGenericClass`1");
        var typeParam = genericClass.GenericParameters[0]; // T

        // Act: linkify with isNullableAnnotated = true to simulate T?
        var result = resolver.Linkify(typeParam, "", "ApiMark.DotNet.Fixtures", externalTypes, isNullableAnnotated: true);

        // Assert: the result must be "T?" — the plain parameter name plus the nullability marker
        Assert.Equal("T?", result);
    }

    /// <summary>
    ///     Validates that a non-System external type reference is rendered as plain text
    ///     and tracked in the caller-supplied external type set.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_ExternalNonSystemType_ReturnsPlainNameAndTracksExternalType()
    {
        // Arrange: create a resolver and a synthetic external TypeReference whose namespace
        // does not start with "System" and whose scope is an AssemblyNameReference (external)
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var assemblyRef = new AssemblyNameReference("Acme.Widgets", new Version(1, 0));
        var externalTypeRef = new TypeReference("Acme.Widgets", "Widget", _assembly.MainModule, assemblyRef);

        // Act
        var result = resolver.Linkify(externalTypeRef, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: plain name returned and the type is recorded in the external types set
        Assert.Multiple(
            () => Assert.Equal("Widget", result),
            () => Assert.Single(externalTypes),
            () => Assert.Contains(
                new ExternalTypeInfo("Widget", "Acme.Widgets"),
                externalTypes));
    }

    /// <summary>
    ///     Validates that an array type reference renders with the <c>[]</c> array rank suffix
    ///     appended to the element type name.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_ArrayType_AppendsArraySuffix()
    {
        // Arrange: wrap System.String in an ArrayType to represent string[]
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var stringArrayType = new ArrayType(_assembly.MainModule.TypeSystem.String);

        // Act
        var result = resolver.Linkify(stringArrayType, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: the result is "string[]" — element alias plus array suffix
        Assert.EndsWith("[]", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a generic instance type reference renders with angle-bracket notation
    ///     showing the resolved type arguments.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_GenericType_RendersTypeArguments()
    {
        // Arrange: get the return type of ArrayAndNullableClass.GetList() which is List<string>
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var arrayClass = _assembly.MainModule.Types.First(t => t.Name == "ArrayAndNullableClass");
        var getListMethod = arrayClass.Methods.First(m => m.Name == "GetList");
        var genericReturnType = getListMethod.ReturnType; // List<string>

        // Act
        var result = resolver.Linkify(genericReturnType, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: the result contains angle-bracket notation for the type arguments
        Assert.Contains("\\<", result, StringComparison.Ordinal);
        Assert.Contains("\\>", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a <c>Nullable&lt;int&gt;</c> generic instance is resolved to the
    ///     inner C# alias with a <c>?</c> suffix (i.e., <c>"int?"</c>).
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_NullableValueType_ReturnsInnerAliasWithQuestionMark()
    {
        // Arrange: construct Nullable<int> as a GenericInstanceType
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var nullableType = new GenericInstanceType(_assembly.MainModule.ImportReference(typeof(Nullable<>)));
        nullableType.GenericArguments.Add(_assembly.MainModule.TypeSystem.Int32);

        // Act
        var result = resolver.Linkify(nullableType, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: Nullable<int> must render as "int?" — not "Nullable<int>"
        Assert.Equal("int?", result);
    }

    /// <summary>
    ///     Validates that a type from a System sub-namespace (e.g. <c>System.IO.Stream</c>) is
    ///     returned as plain text and is not added to the external types accumulator.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_SystemNamespaceExternalType_ReturnsPlainTextAndDoesNotTrack()
    {
        // Arrange: construct a synthetic TypeReference for System.IO.Stream from an external assembly scope
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var assemblyRef = new AssemblyNameReference("System.Runtime", new Version(8, 0));
        var typeRef = new TypeReference("System.IO", "Stream", _assembly.MainModule, assemblyRef);

        // Act
        var result = resolver.Linkify(typeRef, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: plain name returned; System types are not tracked as external dependencies
        Assert.Equal("Stream", result);
        Assert.Empty(externalTypes);
    }

    /// <summary>
    ///     Validates that a multi-dimensional array type reference renders with the correct
    ///     rank suffix appended to the element type name (e.g., <c>[,]</c> for rank-2).
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_MultiDimensionalArrayType_AppendsRankSuffix()
    {
        // Arrange: wrap System.Int32 in a rank-2 ArrayType to represent int[,]
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var rank2ArrayType = new ArrayType(_assembly.MainModule.TypeSystem.Int32, 2);

        // Act
        var result = resolver.Linkify(rank2ArrayType, "", "ApiMark.DotNet.Fixtures", externalTypes);

        // Assert: rank-2 int array must produce "int[,]"
        Assert.Equal("int[,]", result);
    }

    /// <summary>
    ///     Validates that a nullable intra-assembly type reference produces a Markdown link
    ///     with a <c>?</c> suffix when <paramref name="isNullableAnnotated"/> is <see langword="true"/>.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_NullableIntraAssemblyType_ReturnsLinkWithQuestionMark()
    {
        // Arrange
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"], generateLinks: true);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var typeDef = _assembly.MainModule.Types.First(t => t.Name == "SampleClass");

        // Act
        var result = resolver.Linkify(typeDef, "ApiMark.DotNet.Fixtures", "ApiMark.DotNet.Fixtures", externalTypes, isNullableAnnotated: true);

        // Assert: the result must contain a Markdown link AND end with "?"
        Assert.Contains("[", result, StringComparison.Ordinal);
        Assert.EndsWith("?", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an array type reference with <paramref name="isNullableAnnotated"/> set to
    ///     <see langword="true"/> produces a result ending with <c>[]?</c> — the array rank suffix
    ///     followed by the nullable marker.
    /// </summary>
    [Fact]
    public void TypeLinkResolver_Linkify_NullableArrayType_AppendsArraySuffixThenQuestionMark()
    {
        // Arrange: wrap System.String in an ArrayType to represent string[]
        var resolver = new TypeLinkResolver(["ApiMark.DotNet.Fixtures"]);
        var externalTypes = new HashSet<ExternalTypeInfo>();
        var stringArrayType = new ArrayType(_assembly.MainModule.TypeSystem.String);

        // Act
        var result = resolver.Linkify(stringArrayType, "", "ApiMark.DotNet.Fixtures", externalTypes, isNullableAnnotated: true);

        // Assert: nullable string array must render as "string[]?"
        Assert.EndsWith("[]?", result, StringComparison.Ordinal);
    }
}
