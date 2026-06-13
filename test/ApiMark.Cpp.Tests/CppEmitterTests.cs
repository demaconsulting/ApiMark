// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.Cpp;
using ApiMark.Cpp.CppAst;
using Xunit;

namespace ApiMark.Cpp.Tests;

/// <summary>Unit tests for <see cref="CppEmitter"/>.</summary>
public class CppEmitterTests
{
    /// <summary>Builds a minimal <see cref="CppEmitter"/> with one namespace and one class for testing.</summary>
    private static (CppEmitter emitter, SortedDictionary<string, CppEmitter.NamespaceDeclarations> nsDecls, CppTypeLinkResolver resolver) BuildMinimalEmitter()
    {
        var options = new CppGeneratorOptions
        {
            LibraryName = "TestLib",
            PublicIncludeRoots = [FixturePaths.GetFixtureIncludeDir()],
        };
        var nsDecls = new SortedDictionary<string, CppEmitter.NamespaceDeclarations>(StringComparer.Ordinal);
        var ns = new CppEmitter.NamespaceDeclarations("testlib", new CppDocComment("A test library.", null, [], null));
        ns.Classes.Add(new CppClass("Widget", [], [], [], [], [], [], false, false, null,
            new CppDocComment("A widget.", null, [], null)));
        nsDecls["testlib"] = ns;
        var resolver = new CppTypeLinkResolver(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "testlib::Widget", "testlib/Widget" },
        });
        var emitter = new CppEmitter(options, nsDecls, resolver);
        return (emitter, nsDecls, resolver);
    }

    /// <summary>Validates that passing null to <see cref="CppEmitter.Emit"/> throws <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void CppEmitter_Emit_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var (emitter, _, _) = BuildMinimalEmitter();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => emitter.Emit(null!, new EmitConfig(), new InMemoryContext()));
    }

    /// <summary>Validates that <see cref="OutputFormat.GradualDisclosure"/> produces more than one writer.</summary>
    [Fact]
    public void CppEmitter_Emit_GradualDisclosureFormat_ProducesMultipleFiles()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, _, _) = BuildMinimalEmitter();

        // Act
        emitter.Emit(factory, new EmitConfig { Format = OutputFormat.GradualDisclosure }, new InMemoryContext());

        // Assert
        Assert.True(factory.Writers.Count > 1, "GradualDisclosure format must produce more than one writer");
    }

    /// <summary>Validates that <see cref="OutputFormat.SingleFile"/> produces exactly one writer keyed as "api".</summary>
    [Fact]
    public void CppEmitter_Emit_SingleFileFormat_ProducesSingleApiFile()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, _, _) = BuildMinimalEmitter();

        // Act
        emitter.Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("", "api"), "SingleFile format must produce an api writer");
        Assert.Single(factory.Writers);
    }

    /// <summary>Validates that <see cref="CppEmitter.SanitizeFileName"/> leaves regular names unchanged.</summary>
    [Fact]
    public void CppEmitter_SanitizeFileName_RegularName_IsUnchanged()
    {
        // Arrange / Act
        var result = CppEmitter.SanitizeFileName("MyClass");

        // Assert
        Assert.Equal("MyClass", result);
    }

    /// <summary>Validates that <see cref="CppEmitter.BuildClassDeclaration"/> returns the class name for a non-final class with no bases.</summary>
    [Fact]
    public void CppEmitter_BuildClassDeclaration_NonFinalNoBase_ReturnsJustClassName()
    {
        // Arrange
        var cls = new CppClass("Circle", [], [], [], [], [], [], false, false, null, null);

        // Act
        var result = CppEmitter.BuildClassDeclaration(cls);

        // Assert
        Assert.Equal("class Circle", result);
    }

    /// <summary>Validates that <see cref="CppEmitter.BuildClassDeclaration"/> appends "final" for a final class.</summary>
    [Fact]
    public void CppEmitter_BuildClassDeclaration_FinalClass_AppendsFinalKeyword()
    {
        // Arrange
        var cls = new CppClass("FinalWidget", [], [], [], [], [], [], false, true, null, null);

        // Act
        var result = CppEmitter.BuildClassDeclaration(cls);

        // Assert
        Assert.Contains("final", result, StringComparison.Ordinal);
    }
}
