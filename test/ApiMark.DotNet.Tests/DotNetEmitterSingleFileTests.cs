// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.DotNet;
using Xunit;

namespace ApiMark.DotNet.Tests;

/// <summary>Unit tests for <see cref="DotNetEmitterSingleFile"/>.</summary>
public class DotNetEmitterSingleFileTests
{
    /// <summary>Builds DotNetGeneratorOptions pointing at the fixture assembly.</summary>
    private static DotNetGeneratorOptions BuildOptions() => new()
    {
        AssemblyPath = FixturePaths.GetFixtureDll(),
        XmlDocPath = FixturePaths.GetFixtureXmlDoc(),
        Visibility = ApiVisibility.Public,
    };

    /// <summary>Validates that the single-file emitter creates exactly one writer.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_ValidModel_CreatesExactlyOneWriter()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        Assert.Single(factory.Writers);
    }

    /// <summary>Validates that the single-file emitter creates the api writer only.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_ValidModel_CreatesApiFileOnly()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("", "api"), "Expected api writer to be created");
    }

    /// <summary>Validates that the api file contains an assembly-level heading.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_ValidModel_ApiFileContainsAssemblyHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("Fixtures", StringComparison.Ordinal));
    }

    /// <summary>Validates that the api file contains a namespace-level heading.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_ValidModel_ApiFileContainsNamespaceHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: a heading containing the fixture namespace name exists
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("ApiMark.DotNet.Fixtures", StringComparison.Ordinal));
    }

    /// <summary>Validates that the api file contains a type-level heading for SampleClass.</summary>
    [Fact]
    public void DotNetEmitterSingleFile_Emit_ValidModel_ApiFileContainsTypeHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var emitter = (DotNetEmitter)new DotNetGenerator(BuildOptions()).Parse(new InMemoryContext());

        // Act
        new DotNetEmitterSingleFile(emitter, emitter.Model).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: a heading for SampleClass exists
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("SampleClass", StringComparison.Ordinal));
    }
}
