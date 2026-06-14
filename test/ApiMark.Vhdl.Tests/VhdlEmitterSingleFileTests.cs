using System.Linq;
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

    /// <summary>Builds data with two entities for multi-entity tests.</summary>
    private static (VhdlEmitter emitter, IReadOnlyList<VhdlFileModel> fileModels) BuildTwoEntityData()
    {
        var options = new VhdlGeneratorOptions { LibraryName = "TestLib" };
        var entity1 = new VhdlEntityDecl("EntityOne", [], [], new VhdlDocComment("First entity.", null, []));
        var entity2 = new VhdlEntityDecl("EntityTwo", [], [], new VhdlDocComment("Second entity.", null, []));
        var fileModel = new VhdlFileModel("test.vhd", [entity1, entity2], [], []);
        var fileModels = new List<VhdlFileModel> { fileModel };
        var emitter = new VhdlEmitter(options, fileModels);
        return (emitter, fileModels);
    }

    /// <summary>Builds data with a package that has types for single-file tests.</summary>
    private static (VhdlEmitter emitter, IReadOnlyList<VhdlFileModel> fileModels) BuildPackageWithTypesData()
    {
        var options = new VhdlGeneratorOptions { LibraryName = "TestLib" };
        var typeDecl = new VhdlTypeDecl("my_type", "STD_LOGIC_VECTOR(7 DOWNTO 0)", new VhdlDocComment("An 8-bit type.", null, []));
        var pkg = new VhdlPackageDecl(
            "my_pkg",
            new VhdlDocComment("A test package.", null, []),
            [typeDecl],
            [],
            [],
            []);
        var fileModel = new VhdlFileModel("test.vhd", [], [], [pkg]);
        var fileModels = new List<VhdlFileModel> { fileModel };
        var emitter = new VhdlEmitter(options, fileModels);
        return (emitter, fileModels);
    }

    /// <summary>Builds data with entity and architecture for architecture section tests.</summary>
    private static (VhdlEmitter emitter, IReadOnlyList<VhdlFileModel> fileModels) BuildEntityWithArchData()
    {
        var options = new VhdlGeneratorOptions { LibraryName = "TestLib" };
        var entity = new VhdlEntityDecl("MyEntity", [], [], new VhdlDocComment("A test entity.", null, []));
        var arch = new VhdlArchitectureDecl("behavioral", "MyEntity", new VhdlDocComment("Behavioral arch.", null, []));
        var fileModel = new VhdlFileModel("test.vhd", [entity], [arch], []);
        var fileModels = new List<VhdlFileModel> { fileModel };
        var emitter = new VhdlEmitter(options, fileModels);
        return (emitter, fileModels);
    }

    /// <summary>Validates that two entities appear in single-file output.</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_TwoEntities_BothAppearInOutput()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildTwoEntityData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("EntityOne", StringComparison.Ordinal));
        Assert.Contains(headings, h => h.Text.Equals("EntityTwo", StringComparison.Ordinal));
    }

    /// <summary>Validates that package with members renders Types section in single-file output.</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_PackageWithTypes_EmitsTypesSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildPackageWithTypesData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Types", StringComparison.Ordinal));
    }

    /// <summary>Validates that architecture sections appear inside entity sections in single-file output.</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_EntityWithArchitecture_ArchitectureSectionAppearsInOutput()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildEntityWithArchData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Architectures", StringComparison.Ordinal));
    }
}
