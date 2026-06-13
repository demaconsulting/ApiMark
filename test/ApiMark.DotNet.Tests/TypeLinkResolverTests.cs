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
}
