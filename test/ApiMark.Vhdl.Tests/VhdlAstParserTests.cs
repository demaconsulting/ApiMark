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
        // Arrange: resolve path to counter.vhd fixture file
        var path = FixturePaths.GetFixtureFilePath("counter.vhd");

        // Act: invoke parser on the fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: result must contain at least one entity
        Assert.NotEmpty(model.Entities);
    }

    /// <summary>Validates that the entity has at least one generic.</summary>
    [Fact]
    public void VhdlAstParser_Parse_FixtureFile_EntityHasGenerics()
    {
        // Arrange: resolve path to counter.vhd fixture file
        var path = FixturePaths.GetFixtureFilePath("counter.vhd");

        // Act: invoke parser on the fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: single entity must have at least one generic
        var entity = Assert.Single(model.Entities);
        Assert.NotEmpty(entity.Generics);
    }

    /// <summary>Validates that the entity has at least one port.</summary>
    [Fact]
    public void VhdlAstParser_Parse_FixtureFile_EntityHasPorts()
    {
        // Arrange: resolve path to counter.vhd fixture file
        var path = FixturePaths.GetFixtureFilePath("counter.vhd");

        // Act: invoke parser on the fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: single entity must have at least one port
        var entity = Assert.Single(model.Entities);
        Assert.NotEmpty(entity.Ports);
    }

    /// <summary>Validates that the entity doc comment summary is not null or empty.</summary>
    [Fact]
    public void VhdlAstParser_Parse_FixtureFile_EntityDocCommentParsed()
    {
        // Arrange: resolve path to counter.vhd fixture file
        var path = FixturePaths.GetFixtureFilePath("counter.vhd");

        // Act: invoke parser on the fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: entity doc comment Summary field must be populated
        var entity = Assert.Single(model.Entities);
        Assert.NotNull(entity.Doc);
        Assert.False(string.IsNullOrEmpty(entity.Doc.Summary));
    }

    /// <summary>Validates that at least one port has an inline doc comment.</summary>
    [Fact]
    public void VhdlAstParser_Parse_FixtureFile_PortsHaveInlineDocComments()
    {
        // Arrange: resolve path to counter.vhd fixture file
        var path = FixturePaths.GetFixtureFilePath("counter.vhd");

        // Act: invoke parser on the fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: at least one port must have an inline --! trailing comment
        var entity = Assert.Single(model.Entities);
        Assert.Contains(entity.Ports, p => p.Doc != null);
    }

    /// <summary>Validates that mux.vhd parses two architectures.</summary>
    [Fact]
    public void VhdlAstParser_Parse_MuxFixture_ParsesTwoArchitectures()
    {
        // Arrange: resolve path to mux.vhd fixture file
        var path = FixturePaths.MuxVhd;

        // Act: invoke parser on the mux fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: mux.vhd contains two architecture bodies
        Assert.Equal(2, model.Architectures.Count);
    }

    /// <summary>Validates that mux.vhd has one entity named mux.</summary>
    [Fact]
    public void VhdlAstParser_Parse_MuxFixture_HasMuxEntity()
    {
        // Arrange: resolve path to mux.vhd fixture file
        var path = FixturePaths.MuxVhd;

        // Act: invoke parser on the mux fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: single entity name must be "mux"
        var entity = Assert.Single(model.Entities);
        Assert.Equal("mux", entity.Name);
    }

    /// <summary>Validates that common_types.vhd parses a package.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_ReturnsPackage()
    {
        // Arrange: resolve path to common_types.vhd fixture file
        var path = FixturePaths.CommonTypesVhd;

        // Act: invoke parser on the common types fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: at least one package must be present
        Assert.NotEmpty(model.Packages);
    }

    /// <summary>Validates that common_types.vhd package has 2 types.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_PackageHasTwoTypes()
    {
        // Arrange: resolve path to common_types.vhd fixture file
        var path = FixturePaths.CommonTypesVhd;

        // Act: invoke parser on the common types fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: single package must have exactly two type declarations
        var pkg = Assert.Single(model.Packages);
        Assert.Equal(2, pkg.Types.Count);
    }

    /// <summary>Validates that common_types.vhd package has 2 constants.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_PackageHasTwoConstants()
    {
        // Arrange: resolve path to common_types.vhd fixture file
        var path = FixturePaths.CommonTypesVhd;

        // Act: invoke parser on the common types fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: single package must have exactly two constant declarations
        var pkg = Assert.Single(model.Packages);
        Assert.Equal(2, pkg.Constants.Count);
    }

    /// <summary>Validates that package constants have their preceding doc comments parsed.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_ConstantsHaveDocComments()
    {
        // Arrange: resolve path to common_types.vhd fixture file
        var path = FixturePaths.CommonTypesVhd;

        // Act: invoke parser on the common types fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: DATA_WIDTH constant must have a non-empty Summary in its doc comment
        var pkg = Assert.Single(model.Packages);
        var dataWidth = pkg.Constants.FirstOrDefault(c => c.Name == "DATA_WIDTH");
        Assert.NotNull(dataWidth);
        Assert.NotNull(dataWidth.Doc);
        Assert.False(string.IsNullOrEmpty(dataWidth.Doc.Summary));
    }

    /// <summary>Validates that common_types.vhd package has 1 component.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_PackageHasOneComponent()
    {
        // Arrange: resolve path to common_types.vhd fixture file
        var path = FixturePaths.CommonTypesVhd;

        // Act: invoke parser on the common types fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: single package must have exactly one component declaration
        var pkg = Assert.Single(model.Packages);
        Assert.Single(pkg.Components);
    }

    /// <summary>Validates that common_types.vhd package has 2 subprograms.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_PackageHasTwoSubprograms()
    {
        // Arrange: resolve path to common_types.vhd fixture file
        var path = FixturePaths.CommonTypesVhd;

        // Act: invoke parser on the common types fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: single package must have exactly two subprogram declarations
        var pkg = Assert.Single(model.Packages);
        Assert.Equal(2, pkg.Subprograms.Count);
    }

    /// <summary>Validates that to_natural is a Function.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_ToNaturalIsFunction()
    {
        // Arrange: resolve path to common_types.vhd fixture file
        var path = FixturePaths.CommonTypesVhd;

        // Act: invoke parser on the common types fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: to_natural subprogram kind must be Function
        var pkg = Assert.Single(model.Packages);
        var subprogram = pkg.Subprograms.FirstOrDefault(s => s.Name == "to_natural");
        Assert.NotNull(subprogram);
        Assert.Equal(VhdlSubprogramKind.Function, subprogram.Kind);
    }

    /// <summary>Validates that clear_vector is a Procedure.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_ClearVectorIsProcedure()
    {
        // Arrange: resolve path to common_types.vhd fixture file
        var path = FixturePaths.CommonTypesVhd;

        // Act: invoke parser on the common types fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: clear_vector subprogram kind must be Procedure
        var pkg = Assert.Single(model.Packages);
        var subprogram = pkg.Subprograms.FirstOrDefault(s => s.Name == "clear_vector");
        Assert.NotNull(subprogram);
        Assert.Equal(VhdlSubprogramKind.Procedure, subprogram.Kind);
    }

    /// <summary>Validates that to_natural has exactly one formal parameter named v of type STD_LOGIC_VECTOR.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_ToNaturalHasOneParameter()
    {
        // Arrange: resolve path to common_types.vhd fixture file
        var path = FixturePaths.CommonTypesVhd;

        // Act: invoke parser on the common types fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: to_natural must have one parameter named "v" of type STD_LOGIC_VECTOR with empty mode
        var pkg = Assert.Single(model.Packages);
        var subprogram = pkg.Subprograms.FirstOrDefault(s => s.Name == "to_natural");
        Assert.NotNull(subprogram);
        var param = Assert.Single(subprogram.Parameters);
        Assert.Equal("v", param.Name);
        Assert.Equal("STD_LOGIC_VECTOR", param.TypeName);

        // Mode is empty because no class keyword or direction is written for a plain function parameter
        Assert.Equal(string.Empty, param.Mode);
    }

    /// <summary>Validates that to_natural has a return type of NATURAL.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_ToNaturalHasReturnTypeNatural()
    {
        // Arrange: resolve path to common_types.vhd fixture file
        var path = FixturePaths.CommonTypesVhd;

        // Act: invoke parser on the common types fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: to_natural return type must be "NATURAL"
        var pkg = Assert.Single(model.Packages);
        var subprogram = pkg.Subprograms.FirstOrDefault(s => s.Name == "to_natural");
        Assert.NotNull(subprogram);
        Assert.Equal("NATURAL", subprogram.ReturnType);
    }

    /// <summary>Validates that clear_vector has exactly one formal parameter named v of type STD_LOGIC_VECTOR.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_ClearVectorHasOneParameter()
    {
        // Arrange: resolve path to common_types.vhd fixture file
        var path = FixturePaths.CommonTypesVhd;

        // Act: invoke parser on the common types fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: clear_vector must have one parameter with combined SIGNAL OUT mode
        var pkg = Assert.Single(model.Packages);
        var subprogram = pkg.Subprograms.FirstOrDefault(s => s.Name == "clear_vector");
        Assert.NotNull(subprogram);
        var param = Assert.Single(subprogram.Parameters);
        Assert.Equal("v", param.Name);
        Assert.Equal("STD_LOGIC_VECTOR", param.TypeName);

        // Mode must contain both SIGNAL (class keyword) and OUT (direction) from `SIGNAL v : OUT STD_LOGIC_VECTOR`
        Assert.True(
            param.Mode.Contains("SIGNAL", StringComparison.Ordinal) &&
            param.Mode.Contains("OUT", StringComparison.Ordinal),
            $"Expected mode to contain both 'SIGNAL' and 'OUT', got '{param.Mode}'");
    }

    /// <summary>Validates that clear_vector has a null return type because it is a procedure.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_ClearVectorHasNullReturnType()
    {
        // Arrange: resolve path to common_types.vhd fixture file
        var path = FixturePaths.CommonTypesVhd;

        // Act: invoke parser on the common types fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: procedure must have a null ReturnType
        var pkg = Assert.Single(model.Packages);
        var subprogram = pkg.Subprograms.FirstOrDefault(s => s.Name == "clear_vector");
        Assert.NotNull(subprogram);
        Assert.Null(subprogram.ReturnType);
    }

    /// <summary>Validates that to_natural doc comment has a @param entry for parameter v.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_ToNaturalDocHasParamEntry()
    {
        // Arrange: resolve path to common_types.vhd fixture file
        var path = FixturePaths.CommonTypesVhd;

        // Act: invoke parser on the common types fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: to_natural doc must contain a @param entry for "v"
        var pkg = Assert.Single(model.Packages);
        var subprogram = pkg.Subprograms.FirstOrDefault(s => s.Name == "to_natural");
        Assert.NotNull(subprogram);
        Assert.NotNull(subprogram.Doc);
        Assert.Contains(subprogram.Doc.Params, p => p.Name == "v");
    }

    /// <summary>Validates that to_natural doc comment has a @return entry.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CommonTypesFixture_ToNaturalDocHasReturnEntry()
    {
        // Arrange: resolve path to common_types.vhd fixture file
        var path = FixturePaths.CommonTypesVhd;

        // Act: invoke parser on the common types fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: to_natural doc must have a non-empty Returns field from @return tag
        var pkg = Assert.Single(model.Packages);
        var subprogram = pkg.Subprograms.FirstOrDefault(s => s.Name == "to_natural");
        Assert.NotNull(subprogram);
        Assert.NotNull(subprogram.Doc);
        Assert.False(string.IsNullOrEmpty(subprogram.Doc.Returns));
    }

    /// <summary>Validates that a file with invalid VHDL syntax throws InvalidOperationException.</summary>
    [Fact]
    public void VhdlAstParser_Parse_InvalidVhdl_ThrowsInvalidOperationException()
    {
        // Arrange: write deliberately invalid VHDL content to a temp file
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".vhd");
        try
        {
            File.WriteAllText(tempFile, "this is not valid vhdl syntax!!!");

            // Act / Assert: parsing invalid VHDL must throw InvalidOperationException
            Assert.Throws<InvalidOperationException>(() => VhdlAstParser.Parse(tempFile));
        }
        finally
        {
            // Clean up temp file regardless of outcome
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>Validates that the entity name from the counter fixture is "counter".</summary>
    [Fact]
    public void VhdlAstParser_Parse_CounterFixture_EntityNameIsCounter()
    {
        // Arrange: resolve path to counter.vhd fixture file
        var path = FixturePaths.CounterVhd;

        // Act: invoke parser on the counter fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: the single entity must be named "counter"
        var entity = Assert.Single(model.Entities);
        Assert.Equal("counter", entity.Name);
    }

    /// <summary>Validates that generics in the counter fixture have inline --! doc comments.</summary>
    [Fact]
    public void VhdlAstParser_Parse_CounterFixture_GenericsHaveInlineDocComments()
    {
        // Arrange: resolve path to counter.vhd fixture file
        var path = FixturePaths.CounterVhd;

        // Act: invoke parser on the counter fixture
        var model = VhdlAstParser.Parse(path);

        // Assert: at least one generic must have a non-empty inline doc comment
        var entity = Assert.Single(model.Entities);
        Assert.True(
            entity.Generics.Any(g => g.Doc != null && !string.IsNullOrEmpty(g.Doc.Summary)),
            "Expected at least one generic to have an inline --! doc comment");
    }
}
