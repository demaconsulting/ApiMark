using ApiMark.Vhdl.VhdlAst;
using Xunit;

namespace ApiMark.Vhdl.Tests;

/// <summary>Unit tests for <see cref="VhdlAstParser"/>.</summary>
public class VhdlAstParserTests
{
    /// <summary>Validates that the parser returns a non-null entity from the fixture file.</summary>
    [Fact]
    public void VhdlAstParser_Parse_FixtureFile_ReturnsEntity()
    {
        // Arrange
        var path = FixturePaths.GetFixtureFilePath("counter.vhd");

        // Act
        var model = VhdlAstParser.Parse(path);

        // Assert
        Assert.NotEmpty(model.Entities);
    }

    /// <summary>Validates that the entity has at least one generic.</summary>
    [Fact]
    public void VhdlAstParser_Parse_FixtureFile_EntityHasGenerics()
    {
        // Arrange
        var path = FixturePaths.GetFixtureFilePath("counter.vhd");

        // Act
        var model = VhdlAstParser.Parse(path);

        // Assert
        var entity = Assert.Single(model.Entities);
        Assert.NotEmpty(entity.Generics);
    }

    /// <summary>Validates that the entity has at least one port.</summary>
    [Fact]
    public void VhdlAstParser_Parse_FixtureFile_EntityHasPorts()
    {
        // Arrange
        var path = FixturePaths.GetFixtureFilePath("counter.vhd");

        // Act
        var model = VhdlAstParser.Parse(path);

        // Assert
        var entity = Assert.Single(model.Entities);
        Assert.NotEmpty(entity.Ports);
    }

    /// <summary>Validates that the entity doc comment summary is not null or empty.</summary>
    [Fact]
    public void VhdlAstParser_Parse_FixtureFile_EntityDocCommentParsed()
    {
        // Arrange
        var path = FixturePaths.GetFixtureFilePath("counter.vhd");

        // Act
        var model = VhdlAstParser.Parse(path);

        // Assert
        var entity = Assert.Single(model.Entities);
        Assert.NotNull(entity.Doc);
        Assert.False(string.IsNullOrEmpty(entity.Doc.Summary));
    }

    /// <summary>Validates that at least one port has an inline doc comment.</summary>
    [Fact]
    public void VhdlAstParser_Parse_FixtureFile_PortsHaveInlineDocComments()
    {
        // Arrange
        var path = FixturePaths.GetFixtureFilePath("counter.vhd");

        // Act
        var model = VhdlAstParser.Parse(path);

        // Assert
        var entity = Assert.Single(model.Entities);
        Assert.Contains(entity.Ports, p => p.Doc != null);
    }
}
