using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using ApiMark.Vhdl.VhdlAst;
using Xunit;

namespace ApiMark.Vhdl.Tests;

/// <summary>Unit tests for <see cref="VhdlEmitterGradualDisclosure"/>.</summary>
public class VhdlEmitterGradualDisclosureTests
{
    /// <summary>Builds minimal data for testing without parsing real VHDL files.</summary>
    private static (VhdlEmitter emitter, IReadOnlyList<VhdlFileModel> fileModels) BuildMinimalData()
    {
        var options = new VhdlGeneratorOptions { LibraryName = "TestLib" };
        var entity = new VhdlEntityDecl(
            "MyEntity",
            [],
            [],
            new VhdlDocComment("A test entity.", null, []));
        var fileModel = new VhdlFileModel("test.vhd", [entity], [], []);
        var fileModels = new List<VhdlFileModel> { fileModel };
        var emitter = new VhdlEmitter(options, fileModels);
        return (emitter, fileModels);
    }

    /// <summary>Validates that the gradual-disclosure emitter creates the api index page.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_MinimalData_CreatesApiIndexPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildMinimalData();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(factory.HasWriter("", "api"), "Expected api index page to be created");
    }

    /// <summary>Validates that the gradual-disclosure emitter creates an entity page.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_MinimalData_CreatesEntityPage()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildMinimalData();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.True(
            factory.Writers.Keys.Any(k => k.Contains("MyEntity", StringComparison.Ordinal)),
            "Expected a page containing 'MyEntity'");
    }

    /// <summary>Validates that the api index page heading contains the library name.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_MinimalData_ApiIndexContainsLibraryNameHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildMinimalData();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Contains("TestLib", StringComparison.Ordinal));
    }
}
