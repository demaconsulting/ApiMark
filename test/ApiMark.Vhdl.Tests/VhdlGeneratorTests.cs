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

    /// <summary>Validates that empty LibraryName throws ArgumentException.</summary>
    [Fact]
    public void VhdlGenerator_Constructor_EmptyLibraryName_ThrowsArgumentException()
    {
        // Arrange: options with an empty LibraryName
        var options = new VhdlGeneratorOptions { LibraryName = string.Empty };

        // Act / Assert: empty LibraryName must be rejected at construction time
        Assert.Throws<ArgumentException>(() => new VhdlGenerator(options));
    }

    /// <summary>Validates that a whitespace-only LibraryName throws ArgumentException.</summary>
    [Fact]
    public void VhdlGenerator_Constructor_WhitespaceLibraryName_ThrowsArgumentException()
    {
        // Arrange: options with a whitespace-only LibraryName
        var options = new VhdlGeneratorOptions { LibraryName = "   " };

        // Act / Assert: whitespace LibraryName must be rejected at construction time
        Assert.Throws<ArgumentException>(() => new VhdlGenerator(options));
    }

    /// <summary>Validates that a source pattern matching no files emits an error and returns an emitter that produces no output.</summary>
    [Fact]
    public void VhdlGenerator_Parse_NoFilesMatched_EmitsErrorAndReturnsEmptyEmitter()
    {
        // Arrange: use a pattern that will not match any files
        var options = new VhdlGeneratorOptions
        {
            LibraryName = "TestLib",
            WorkingDirectory = FixturePaths.FixturesDirectory,
            Sources = ["*.nonexistent"],
        };
        var generator = new VhdlGenerator(options);
        var factory = new InMemoryMarkdownWriterFactory();
        var context = new InMemoryContext();

        // Act: parse with a non-matching pattern
        var emitter = generator.Parse(context);
        emitter.Emit(factory, new EmitConfig(), context);

        // Assert: an error message was written and no output was produced
        Assert.NotEmpty(context.Errors);
        Assert.Empty(factory.Writers);
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

    /// <summary>Validates that generator against all fixtures confirms entities, package, and no standalone arch pages.</summary>
    [Fact]
    public void VhdlGenerator_Generate_AllFixtures_ProducesExpectedOutputStructure()
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
        Assert.True(
            factory.Writers.Keys.Any(k => k.Contains("mux", StringComparison.OrdinalIgnoreCase)),
            "Expected a page containing 'mux'");
        Assert.True(
            factory.Writers.Keys.Any(k => k.Contains("common_types", StringComparison.OrdinalIgnoreCase)),
            "Expected a page containing 'common_types'");
        Assert.DoesNotContain(factory.Writers.Keys, k => k.Contains("_arch", StringComparison.Ordinal));
    }
}
