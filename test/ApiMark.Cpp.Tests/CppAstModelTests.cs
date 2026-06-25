// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Cpp.CppAst;
using Xunit;

namespace ApiMark.Cpp.Tests;

/// <summary>Unit tests for the record types defined in <c>CppAstModel.cs</c>.</summary>
public class CppAstModelTests
{
    /// <summary>Validates that <see cref="CppSourceLocation"/> stores File and Line correctly.</summary>
    [Fact]
    public void CppSourceLocation_Construction_SetsFileAndLine()
    {
        // Arrange / Act
        var location = new CppSourceLocation("myfile.h", 42);

        // Assert
        Assert.Equal("myfile.h", location.File);
        Assert.Equal(42, location.Line);
    }

    /// <summary>Validates that <see cref="CppParamDoc"/> stores Name and Description correctly.</summary>
    [Fact]
    public void CppParamDoc_Construction_SetsNameAndDescription()
    {
        // Arrange / Act
        var paramDoc = new CppParamDoc("count", "Number of items.");

        // Assert
        Assert.Equal("count", paramDoc.Name);
        Assert.Equal("Number of items.", paramDoc.Description);
    }

    /// <summary>Validates that <see cref="CppDocComment"/> stores Summary and Details correctly.</summary>
    [Fact]
    public void CppDocComment_Construction_SetsSummaryAndDetails()
    {
        // Arrange / Act
        var doc = new CppDocComment("A brief summary.", "Detailed explanation.", [], "Returns a value.");

        // Assert
        Assert.Equal("A brief summary.", doc.Summary);
        Assert.Equal("Detailed explanation.", doc.Details);
    }

    /// <summary>Validates that two identical <see cref="CppDocComment"/> instances are equal (record equality).</summary>
    [Fact]
    public void CppDocComment_Equality_TwoIdenticalInstances_AreEqual()
    {
        // Arrange
        var doc1 = new CppDocComment("Summary.", null, [], null);
        var doc2 = new CppDocComment("Summary.", null, [], null);

        // Act / Assert
        Assert.Equal(doc1, doc2);
    }

    /// <summary>Validates that Note and Example default to null when not provided.</summary>
    [Fact]
    public void CppDocComment_NoteAndExample_WhenNotProvided_AreNull()
    {
        // Arrange / Act
        var doc = new CppDocComment("Summary.", null, [], null);

        // Assert
        Assert.Null(doc.Note);
        Assert.Null(doc.Example);
    }

    /// <summary>Validates that <see cref="CppBaseType"/> stores Name correctly.</summary>
    [Fact]
    public void CppBaseType_Construction_SetsName()
    {
        // Arrange / Act
        var baseType = new CppBaseType("Shape");

        // Assert
        Assert.Equal("Shape", baseType.Name);
    }

    /// <summary>Validates that <see cref="CppTemplateParam"/> stores Name correctly.</summary>
    [Fact]
    public void CppTemplateParam_Construction_SetsName()
    {
        // Arrange / Act
        var templateParam = new CppTemplateParam("T");

        // Assert
        Assert.Equal("T", templateParam.Name);
    }

    /// <summary>Validates that <see cref="CppEnumValue"/> stores Name and Doc correctly.</summary>
    [Fact]
    public void CppEnumValue_Construction_SetsNameAndDoc()
    {
        // Arrange
        var doc = new CppDocComment("Active state.", null, [], null);

        // Act
        var enumValue = new CppEnumValue("Active", doc);

        // Assert
        Assert.Equal("Active", enumValue.Name);
        Assert.Same(doc, enumValue.Doc);
    }

    /// <summary>Validates that <see cref="CppParameter"/> stores Name and TypeName correctly.</summary>
    [Fact]
    public void CppParameter_Construction_SetsNameAndTypeName()
    {
        // Arrange / Act
        var param = new CppParameter("radius", "double");

        // Assert
        Assert.Equal("radius", param.Name);
        Assert.Equal("double", param.TypeName);
    }

    /// <summary>Validates that the DefaultValue of <see cref="CppParameter"/> is null when not provided.</summary>
    [Fact]
    public void CppParameter_DefaultValue_WhenNotProvided_IsNull()
    {
        // Arrange / Act
        var param = new CppParameter("radius", "double");

        // Assert
        Assert.Null(param.DefaultValue);
    }

    /// <summary>Validates that <see cref="CppField"/> stores core properties correctly.</summary>
    [Fact]
    public void CppField_Construction_SetsCoreProperties()
    {
        // Arrange
        var location = new CppSourceLocation("widget.h", 10);

        // Act
        var field = new CppField("m_value", "int", CppAccessibility.Private, false, false, location, null);

        // Assert
        Assert.Equal("m_value", field.Name);
        Assert.Equal("int", field.TypeName);
        Assert.Equal(CppAccessibility.Private, field.Accessibility);
        Assert.False(field.IsStatic);
    }

    /// <summary>Validates that <see cref="CppFunction"/> stores core properties correctly.</summary>
    [Fact]
    public void CppFunction_Construction_SetsCoreProperties()
    {
        // Arrange / Act
        var fn = new CppFunction(
            "GetCount", "int", [], CppAccessibility.Public,
            false, false, false, false, false, false, null, null);

        // Assert
        Assert.Equal("GetCount", fn.Name);
        Assert.Equal("int", fn.ReturnTypeName);
        Assert.Equal(CppAccessibility.Public, fn.Accessibility);
        Assert.False(fn.IsConstructor);
        Assert.False(fn.IsDeleted);
    }

    /// <summary>Validates that <see cref="CppClass"/> stores core properties correctly.</summary>
    [Fact]
    public void CppClass_Construction_SetsCoreProperties()
    {
        // Arrange / Act
        var cls = new CppClass("Widget", [], [], [], [], [], [], false, false, null, null);

        // Assert
        Assert.Equal("Widget", cls.Name);
        Assert.Empty(cls.BaseTypes);
        Assert.False(cls.IsFinal);
    }

    /// <summary>Validates that <see cref="CppEnum"/> stores Name and Values correctly.</summary>
    [Fact]
    public void CppEnum_Construction_SetsNameAndValues()
    {
        // Arrange
        var values = new[] { new CppEnumValue("Red", null), new CppEnumValue("Green", null) };

        // Act
        var cppEnum = new CppEnum("Color", values, false, null, null);

        // Assert
        Assert.Equal("Color", cppEnum.Name);
        Assert.Equal(2, cppEnum.Values.Count);
    }

    /// <summary>Validates that <see cref="CppTypeAlias"/> stores Name and UnderlyingTypeName correctly.</summary>
    [Fact]
    public void CppTypeAlias_Construction_SetsNameAndUnderlyingType()
    {
        // Arrange / Act
        var alias = new CppTypeAlias("handle_t", "void*", false, null, null);

        // Assert
        Assert.Equal("handle_t", alias.Name);
        Assert.Equal("void*", alias.UnderlyingTypeName);
    }

    /// <summary>Validates that <see cref="CppNamespaceDecl"/> stores QualifiedName correctly.</summary>
    [Fact]
    public void CppNamespaceDecl_Construction_SetsQualifiedName()
    {
        // Arrange / Act
        var ns = new CppNamespaceDecl("mylib::rendering", [], [], [], [], null);

        // Assert
        Assert.Equal("mylib::rendering", ns.QualifiedName);
    }

    /// <summary>Validates that <see cref="CppCompilationResult"/> stores Namespaces and Errors correctly.</summary>
    [Fact]
    public void CppCompilationResult_Construction_SetsNamespacesAndErrors()
    {
        // Arrange
        var ns = new CppNamespaceDecl("mylib", [], [], [], [], null);
        var errors = new[] { "warning: something" };

        // Act
        var result = new CppCompilationResult([ns], errors);

        // Assert
        Assert.Single(result.Namespaces);
        Assert.Single(result.Errors);
    }

    /// <summary>Validates that the <see cref="CppAccessibility"/> enum contains Public, Protected, and Private values.</summary>
    [Fact]
    public void CppAccessibility_Values_ArePublicProtectedPrivate()
    {
        // Arrange / Act
        var values = Enum.GetValues<CppAccessibility>();

        // Assert
        Assert.Contains(CppAccessibility.Public, values);
        Assert.Contains(CppAccessibility.Protected, values);
        Assert.Contains(CppAccessibility.Private, values);
    }
}
