using ApiMark.Core;
using ApiMark.Core.TestHelpers;
using Xunit;

namespace ApiMark.Vhdl.Tests;

/// <summary>Unit tests for <see cref="VhdlGenerator"/>.</summary>
public class VhdlGeneratorTests
{
    /// <summary>Validates that null options throw ArgumentNullException.</summary>
    [Fact]
    public void VhdlGenerator_Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentNullException>(() => new VhdlGenerator(null!));
    }

    /// <summary>Validates that the generator creates the api entrypoint file from the fixture.</summary>
    [Fact]
    public void VhdlGenerator_Generate_FixtureFile_CreatesApiEntrypoint()
    {
        // Arrange
        var options = new VhdlGeneratorOptions
        {
            LibraryName = "TestLib",
            WorkingDirectory = FixturePaths.FixturesDirectory,
            Sources = ["*.vhd"],
        };
        var generator = new VhdlGenerator(options);
        var factory = new InMemoryMarkdownWriterFactory();
        var context = new InMemoryContext();

        // Act
        generator.Parse(context).Emit(factory, new EmitConfig(), context);

        // Assert
        Assert.True(factory.HasWriter("", "api"), "Expected api entrypoint to be created");
    }

    /// <summary>Validates that the generator creates an entity page from the fixture.</summary>
    [Fact]
    public void VhdlGenerator_Generate_FixtureFile_CreatesEntityPage()
    {
        // Arrange
        var options = new VhdlGeneratorOptions
        {
            LibraryName = "TestLib",
            WorkingDirectory = FixturePaths.FixturesDirectory,
            Sources = ["*.vhd"],
        };
        var generator = new VhdlGenerator(options);
        var factory = new InMemoryMarkdownWriterFactory();
        var context = new InMemoryContext();

        // Act
        generator.Parse(context).Emit(factory, new EmitConfig(), context);

        // Assert
        Assert.True(
            factory.Writers.Keys.Any(k => k.Contains("counter", StringComparison.OrdinalIgnoreCase)),
            "Expected a page containing 'counter'");
    }
}
