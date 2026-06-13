using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.Vhdl.VhdlAst;
using Xunit;

namespace ApiMark.Vhdl.Tests;

/// <summary>Unit tests for <see cref="VhdlEmitterSingleFile"/>.</summary>
public class VhdlEmitterSingleFileTests
{
    /// <summary>Builds minimal data for testing.</summary>
    private static (VhdlEmitter emitter, IReadOnlyList<VhdlFileModel> fileModels) BuildMinimalData()
    {
        var options = new VhdlGeneratorOptions { LibraryName = "TestLib" };
        var entity = new VhdlEntityDecl("MyEntity", [], [], new VhdlDocComment("A test entity.", null, []));
        var fileModel = new VhdlFileModel("test.vhd", [entity], [], []);
        var fileModels = new List<VhdlFileModel> { fileModel };
        var emitter = new VhdlEmitter(options, fileModels);
        return (emitter, fileModels);
    }

    /// <summary>Validates that the single-file emitter creates exactly one writer.</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_MinimalData_CreatesExactlyOneWriter()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildMinimalData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        Assert.Single(factory.Writers);
    }

    /// <summary>Validates that the single-file emitter creates only the api file.</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_MinimalData_CreatesApiFileOnly()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildMinimalData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("", "api"), "Expected api writer to be created");
    }
}
