// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Unit tests for <see cref="DotNetEmitter"/>.</summary>
public class DotNetEmitterTests
{
    /// <summary>Builds DotNetGeneratorOptions pointing at the fixture assembly.</summary>
    private static DotNetGeneratorOptions BuildOptions() => new()
    {
        AssemblyPath = FixturePaths.GetFixtureDll(),
        XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
        Visibility = ApiVisibility.Public,
    };

    /// <summary>Validates that passing null to <see cref="DotNetEmitter.Emit"/> throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void DotNetEmitter_Emit_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => emitter.Emit(null!, new EmitConfig(), new InMemoryContext()));
    }

    /// <summary>Validates that <see cref="OutputFormat.GradualDisclosure"/> produces more than one writer.</summary>
    [Fact]
    public void DotNetEmitter_Emit_GradualDisclosureFormat_ProducesMultipleFiles()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        emitter.Emit(factory, new EmitConfig { Format = OutputFormat.GradualDisclosure }, new InMemoryContext());

        // Assert
        Assert.True(factory.Writers.Count > 1, "GradualDisclosure format must produce more than one writer");
    }

    /// <summary>Validates that <see cref="OutputFormat.SingleFile"/> produces exactly one writer keyed as "api".</summary>
    [Fact]
    public void DotNetEmitter_Emit_SingleFileFormat_ProducesSingleApiFile()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        emitter.Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("", "api"), "SingleFile format must produce an api writer");
        Assert.Single(factory.Writers);
    }

    /// <summary>Validates that <see cref="DotNetEmitter.GetNamespaceFolderPath"/> returns the full dotted name for a root namespace.</summary>
    [Fact]
    public void DotNetEmitter_GetNamespaceFolderPath_RootNamespace_ReturnsDottedName()
    {
        // Arrange / Act
        var result = DotNetEmitter.GetNamespaceFolderPath("A.B", ["A.B"]);

        // Assert
        Assert.Equal("A.B", result);
    }

    /// <summary>Validates that <see cref="DotNetEmitter.GetNamespaceFolderPath"/> returns slash-separated path for a child namespace.</summary>
    [Fact]
    public void DotNetEmitter_GetNamespaceFolderPath_ChildNamespace_ReturnsSlashSeparated()
    {
        // Arrange / Act
        var result = DotNetEmitter.GetNamespaceFolderPath("A.B.C", ["A.B"]);

        // Assert
        Assert.Equal("A.B/C", result);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.ToXmlDocTypeName"/> converts a Cecil-encoded
    ///     generic instantiation to the XML doc ID encoding.
    /// </summary>
    [Theory]
    [InlineData("System.String", "System.String")]
    [InlineData("System.String[]", "System.String[]")]
    [InlineData("System.Collections.Generic.IEnumerable`1<System.String>",
                "System.Collections.Generic.IEnumerable{System.String}")]
    [InlineData("System.Collections.Generic.IReadOnlyDictionary`2<System.String,System.Object>",
                "System.Collections.Generic.IReadOnlyDictionary{System.String,System.Object}")]
    [InlineData("System.Action`1<System.String>", "System.Action{System.String}")]
    [InlineData("Outer/Inner", "Outer.Inner")]
    public void DotNetEmitter_ToXmlDocTypeName_ConvertsGenericNotation(string cecilFullName, string expected)
    {
        // Act
        var result = DotNetEmitter.ToXmlDocTypeName(cecilFullName);

        // Assert
        Assert.Equal(expected, result);
    }
}
