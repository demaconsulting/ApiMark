using System.Linq;
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

    /// <summary>Validates that mux.vhd parses two architectures.</summary>
    [Fact]
    public void VhdlAstParser_Parse_MuxFixture_ParsesTwoArchitectures()
    {
        // Arrange
        var path = FixturePaths.MuxVhd;

        // Act
        var model = VhdlAstParser.Parse(path);

        // Assert
        Assert.Equal(2, model.Architectures.Count);
    }

    /// <summary>Validates that mux.vhd has one entity named mux.</summary>
    [Fact]
    public void VhdlAstParser_Parse_MuxFixture_HasMuxEntity()
    {
        // Arrange
        var path = FixturePaths.MuxVhd;

        // Act
        var model = VhdlAstParser.Parse(path);

        // Assert
        var entity = Assert.Single(model.Entities);
        Assert.Equal("mux", entity.Name);
    }

    /// <summary>Validates that common_types.vhd parses a package.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_ReturnsPackage()
    {
        // Arrange
        var path = FixturePaths.CommonTypesVhd;

        // Act
        var model = VhdlAstParser.Parse(path);

        // Assert
        Assert.NotEmpty(model.Packages);
    }

    /// <summary>Validates that common_types.vhd package has 2 types.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_PackageHasTwoTypes()
    {
        // Arrange
        var path = FixturePaths.CommonTypesVhd;

        // Act
        var model = VhdlAstParser.Parse(path);

        // Assert
        var pkg = Assert.Single(model.Packages);
        Assert.Equal(2, pkg.Types.Count);
    }

    /// <summary>Validates that common_types.vhd package has 2 constants.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_PackageHasTwoConstants()
    {
        // Arrange
        var path = FixturePaths.CommonTypesVhd;

        // Act
        var model = VhdlAstParser.Parse(path);

        // Assert
        var pkg = Assert.Single(model.Packages);
        Assert.Equal(2, pkg.Constants.Count);
    }

    /// <summary>Validates that common_types.vhd package has 1 component.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_PackageHasOneComponent()
    {
        // Arrange
        var path = FixturePaths.CommonTypesVhd;

        // Act
        var model = VhdlAstParser.Parse(path);

        // Assert
        var pkg = Assert.Single(model.Packages);
        Assert.Single(pkg.Components);
    }

    /// <summary>Validates that common_types.vhd package has 2 subprograms.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_PackageHasTwoSubprograms()
    {
        // Arrange
        var path = FixturePaths.CommonTypesVhd;

        // Act
        var model = VhdlAstParser.Parse(path);

        // Assert
        var pkg = Assert.Single(model.Packages);
        Assert.Equal(2, pkg.Subprograms.Count);
    }

    /// <summary>Validates that to_natural is a Function.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_ToNaturalIsFunction()
    {
        // Arrange
        var path = FixturePaths.CommonTypesVhd;

        // Act
        var model = VhdlAstParser.Parse(path);

        // Assert
        var pkg = Assert.Single(model.Packages);
        var subprogram = pkg.Subprograms.FirstOrDefault(s => s.Name == "to_natural");
        Assert.NotNull(subprogram);
        Assert.Equal(VhdlSubprogramKind.Function, subprogram.Kind);
    }

    /// <summary>Validates that clear_vector is a Procedure.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_ClearVectorIsProcedure()
    {
        // Arrange
        var path = FixturePaths.CommonTypesVhd;

        // Act
        var model = VhdlAstParser.Parse(path);

        // Assert
        var pkg = Assert.Single(model.Packages);
        var subprogram = pkg.Subprograms.FirstOrDefault(s => s.Name == "clear_vector");
        Assert.NotNull(subprogram);
        Assert.Equal(VhdlSubprogramKind.Procedure, subprogram.Kind);
    }
}
