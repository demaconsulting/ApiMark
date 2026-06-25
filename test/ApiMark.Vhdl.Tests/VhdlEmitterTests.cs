using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.Vhdl.VhdlAst;
using Xunit;

namespace ApiMark.Vhdl.Tests;

/// <summary>Unit tests for <see cref="VhdlEmitter"/>.</summary>
public class VhdlEmitterTests
{
    /// <summary>Validates that passing null factory to Emit throws ArgumentNullException.</summary>
    [Fact]
    public void VhdlEmitter_Emit_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange: build an emitter with no file models
        var options = new VhdlGeneratorOptions { LibraryName = "TestLib" };
        var emitter = new VhdlEmitter(options, []);

        // Act / Assert: null factory must be rejected immediately
        Assert.Throws<ArgumentNullException>(() =>
            emitter.Emit(null!, new EmitConfig(), new InMemoryContext()));
    }

    /// <summary>Validates that GradualDisclosure format produces more than one output writer (api.md plus at least one entity page).</summary>
    [Fact]
    public void VhdlEmitter_Emit_GradualDisclosureFormat_ProducesMultipleOutputFiles()
    {
        // Arrange: build an emitter with one entity so GD format writes api.md and an entity page
        var options = new VhdlGeneratorOptions { LibraryName = "TestLib" };
        var entity = new VhdlEntityDecl("MyEntity", [], [], new VhdlDocComment("A test entity.", null, []));
        var fileModel = new VhdlFileModel("test.vhd", [entity], [], []);
        var emitter = new VhdlEmitter(options, [fileModel]);
        var factory = new InMemoryMarkdownWriterFactory();

        // Act: emit with the default (GradualDisclosure) format
        emitter.Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: api.md plus at least one entity page means more than one writer
        Assert.True(factory.Writers.Count > 1, "Expected more than one output writer for GradualDisclosure format");
    }

    /// <summary>Validates that SingleFile format produces exactly one writer keyed "api".</summary>
    [Fact]
    public void VhdlEmitter_Emit_SingleFileFormat_ProducesSingleOutputFile()
    {
        // Arrange: build an emitter with one entity
        var options = new VhdlGeneratorOptions { LibraryName = "TestLib" };
        var entity = new VhdlEntityDecl("MyEntity", [], [], new VhdlDocComment("A test entity.", null, []));
        var fileModel = new VhdlFileModel("test.vhd", [entity], [], []);
        var emitter = new VhdlEmitter(options, [fileModel]);
        var factory = new InMemoryMarkdownWriterFactory();

        // Act: emit with SingleFile format
        emitter.Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: exactly one writer keyed "api"
        Assert.Single(factory.Writers);
        Assert.True(factory.HasWriter("", "api"), "Expected the single writer to be keyed 'api'");
    }

    /// <summary>Validates that when no file models exist, Emit produces no writers.</summary>
    [Fact]
    public void VhdlEmitter_Emit_EmptyFileModels_ProducesNoOutput()
    {
        // Arrange: build an emitter with an empty file models list
        var options = new VhdlGeneratorOptions { LibraryName = "TestLib" };
        var emitter = new VhdlEmitter(options, []);
        var factory = new InMemoryMarkdownWriterFactory();

        // Act: emit — empty models should return early with no output
        emitter.Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: no writers created
        Assert.Empty(factory.Writers);
    }
}
