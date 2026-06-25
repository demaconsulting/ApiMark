// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Mono.Cecil;
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

    /// <summary>Validates that passing a null config to <see cref="DotNetEmitter.Emit"/> throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void DotNetEmitter_Emit_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => emitter.Emit(factory, null!, new InMemoryContext()));
    }

    /// <summary>Validates that passing a null context to <see cref="DotNetEmitter.Emit"/> throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void DotNetEmitter_Emit_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => emitter.Emit(factory, new EmitConfig(), null!));
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

    /// <summary>Validates that <see cref="DotNetEmitter.GetNamespaceFolderPath"/> returns the full name for an unknown namespace.</summary>
    [Fact]
    public void DotNetEmitter_GetNamespaceFolderPath_UnknownNamespace_ReturnsFullName()
    {
        // Arrange / Act
        var result = DotNetEmitter.GetNamespaceFolderPath("Unknown.Namespace", ["ApiMark.DotNet"]);

        // Assert: namespace that matches no root is returned as its full dotted name
        Assert.Equal("Unknown.Namespace", result);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildTypeSignature"/> includes the <c>abstract</c>
    ///     modifier for an abstract class that is not sealed.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildTypeSignature_AbstractClass_ContainsAbstractModifier()
    {
        // Arrange: load the abstract fixture class from the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "AbstractFixtureClass");

        // Act
        var signature = DotNetEmitter.BuildTypeSignature(type, "ApiMark.DotNet.Fixtures");

        // Assert: the abstract modifier must be present and no conflicting modifiers appear
        Assert.Contains("abstract ", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("static ", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("sealed ", signature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildTypeSignature"/> includes the <c>sealed</c>
    ///     modifier for a sealed class that is not abstract.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildTypeSignature_SealedClass_ContainsSealedModifier()
    {
        // Arrange: load the sealed fixture class from the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "SealedClass");

        // Act
        var signature = DotNetEmitter.BuildTypeSignature(type, "ApiMark.DotNet.Fixtures");

        // Assert: the sealed modifier must be present and no conflicting modifiers appear
        Assert.Contains("sealed ", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("abstract ", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("static ", signature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.BuildTypeSignature"/> includes the <c>static</c>
    ///     modifier for a static class.
    /// </summary>
    [Fact]
    public void DotNetEmitter_BuildTypeSignature_StaticClass_ContainsStaticModifier()
    {
        // Arrange: load the static fixture class from the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "StaticFixtureClass");

        // Act
        var signature = DotNetEmitter.BuildTypeSignature(type, "ApiMark.DotNet.Fixtures");

        // Assert: the static modifier must be present and no conflicting modifiers appear
        Assert.Contains("static ", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("abstract ", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("sealed ", signature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.IsNamespaceDocCarrier"/> returns
    ///     <see langword="true"/> for the <c>NamespaceDoc</c> carrier class in the fixture assembly.
    /// </summary>
    [Fact]
    public void DotNetEmitter_IsNamespaceDocCarrier_NamespaceDocClass_ReturnsTrue()
    {
        // Arrange: load all types including internal ones from the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "NamespaceDoc");

        // Act
        var result = DotNetEmitter.IsNamespaceDocCarrier(type);

        // Assert
        Assert.True(result, "NamespaceDoc internal static class must be recognized as a carrier.");
    }

    /// <summary>
    ///     Validates that <see cref="DotNetEmitter.IsNamespaceDocCarrier"/> returns
    ///     <see langword="false"/> for a regular (non-carrier) class.
    /// </summary>
    [Fact]
    public void DotNetEmitter_IsNamespaceDocCarrier_RegularClass_ReturnsFalse()
    {
        // Arrange: SampleClass is a regular public class, not a NamespaceDoc carrier
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "SampleClass");

        // Act
        var result = DotNetEmitter.IsNamespaceDocCarrier(type);

        // Assert
        Assert.False(result, "A regular public class must not be recognized as a NamespaceDoc carrier.");
    }

    /// <summary>Validates that <see cref="DotNetEmitter.BuildPropertyAccessors"/> emits <c>init;</c> for init-only setters.</summary>
    [Fact]
    public void DotNetEmitter_BuildPropertyAccessors_InitOnlySetter_EmitsInit()
    {
        // Arrange
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.Single(t => t.Name == "InitPropertyClass");
        var prop = type.Properties.Single(p => p.Name == "InitOnlyProperty");

        // Act
        var result = DotNetEmitter.BuildPropertyAccessors(prop);

        // Assert
        Assert.Contains("init", result, StringComparison.Ordinal);
        Assert.DoesNotContain("set", result, StringComparison.Ordinal);
    }

    /// <summary>Validates that <see cref="DotNetEmitter.BuildPropertyAccessors"/> does not prefix accessors when they share the property's declared accessibility.</summary>
    [Fact]
    public void DotNetEmitter_BuildPropertyAccessors_ProtectedProperty_DoesNotPrefixAccessors()
    {
        // Arrange: load ProtectedProperty from AbstractFixtureClass in the fixture assembly
        using var assembly = AssemblyDefinition.ReadAssembly(FixturePaths.GetFixtureDll());
        var type = assembly.MainModule.Types.First(t => t.Name == "AbstractFixtureClass");
        var prop = type.Properties.Single(p => p.Name == "ProtectedProperty");

        // Act
        var result = DotNetEmitter.BuildPropertyAccessors(prop);

        // Assert: both accessors share the property's protected accessibility — no prefix should be emitted
        Assert.Equal("get; set;", result);
    }
}
