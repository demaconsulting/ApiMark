using System.Linq;
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

    /// <summary>Builds data with an entity and architecture pair for architecture-related tests.</summary>
    private static (VhdlEmitter emitter, IReadOnlyList<VhdlFileModel> fileModels) BuildDataWithArchitecture()
    {
        var options = new VhdlGeneratorOptions { LibraryName = "TestLib" };
        var entity = new VhdlEntityDecl(
            "MyEntity",
            [],
            [],
            new VhdlDocComment("A test entity.", null, []));
        var arch = new VhdlArchitectureDecl(
            "behavioral",
            "MyEntity",
            new VhdlDocComment("Behavioral implementation.", null, []));
        var fileModel = new VhdlFileModel("test.vhd", [entity], [arch], []);
        var fileModels = new List<VhdlFileModel> { fileModel };
        var emitter = new VhdlEmitter(options, fileModels);
        return (emitter, fileModels);
    }

    /// <summary>Builds data with a package that has members.</summary>
    private static (VhdlEmitter emitter, IReadOnlyList<VhdlFileModel> fileModels) BuildDataWithPackageMembers()
    {
        var options = new VhdlGeneratorOptions { LibraryName = "TestLib" };
        var typeDecl = new VhdlTypeDecl("my_type", "STD_LOGIC_VECTOR(7 DOWNTO 0)", new VhdlDocComment("An 8-bit type.", null, []));
        var constDecl = new VhdlConstantDecl("MY_CONST", "INTEGER", "42", new VhdlDocComment("A constant.", null, []));
        var compDecl = new VhdlComponentDecl("my_comp", new VhdlDocComment("A component.", null, []));
        var subprogramDecl = new VhdlSubprogramDecl("my_func", VhdlSubprogramKind.Function, "FUNCTION my_func RETURN INTEGER", new VhdlDocComment("A function.", null, []));
        var pkg = new VhdlPackageDecl(
            "my_pkg",
            new VhdlDocComment("A test package.", null, []),
            [typeDecl],
            [constDecl],
            [compDecl],
            [subprogramDecl]);
        var fileModel = new VhdlFileModel("test.vhd", [], [], [pkg]);
        var fileModels = new List<VhdlFileModel> { fileModel };
        var emitter = new VhdlEmitter(options, fileModels);
        return (emitter, fileModels);
    }

    /// <summary>Validates that api.md does NOT contain a standalone Architectures section heading.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_WithArchitecture_ApiIndexHasNoArchitecturesSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithArchitecture();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.DoesNotContain(headings, h => h.Text.Equals("Architectures", StringComparison.Ordinal));
    }

    /// <summary>Validates that no standalone arch page files are created.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_WithArchitecture_NoStandaloneArchPageCreated()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithArchitecture();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        Assert.DoesNotContain(factory.Writers.Keys, k => k.Contains("_arch", StringComparison.Ordinal));
    }

    /// <summary>Validates that entity page still has inline Architectures section.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_WithArchitecture_EntityPageHasInlineArchitecturesSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithArchitecture();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var entityWriter = factory.Writers.Values.FirstOrDefault(w =>
            w.Operations.OfType<HeadingOperation>().Any(h => h.Text.Equals("MyEntity", StringComparison.Ordinal)));
        Assert.NotNull(entityWriter);
        var headings = entityWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Architectures", StringComparison.Ordinal));
    }

    /// <summary>Validates that package with members emits Types section on its detail page.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_PackageWithTypes_EmitsTypesSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithPackageMembers();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var pkgWriter = factory.Writers.Values.FirstOrDefault(w =>
            w.Operations.OfType<HeadingOperation>().Any(h => h.Text.Equals("my_pkg", StringComparison.Ordinal)));
        Assert.NotNull(pkgWriter);
        var headings = pkgWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Types", StringComparison.Ordinal));
    }

    /// <summary>Validates that package with members emits Constants section on its detail page.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_PackageWithConstants_EmitsConstantsSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithPackageMembers();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var pkgWriter = factory.Writers.Values.FirstOrDefault(w =>
            w.Operations.OfType<HeadingOperation>().Any(h => h.Text.Equals("my_pkg", StringComparison.Ordinal)));
        Assert.NotNull(pkgWriter);
        var headings = pkgWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Constants", StringComparison.Ordinal));
    }

    /// <summary>Validates that package with members emits Components section on its detail page.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_PackageWithComponents_EmitsComponentsSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithPackageMembers();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var pkgWriter = factory.Writers.Values.FirstOrDefault(w =>
            w.Operations.OfType<HeadingOperation>().Any(h => h.Text.Equals("my_pkg", StringComparison.Ordinal)));
        Assert.NotNull(pkgWriter);
        var headings = pkgWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Components", StringComparison.Ordinal));
    }

    /// <summary>Validates that package with members emits Subprograms section on its detail page.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_PackageWithSubprograms_EmitsSubprogramsSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithPackageMembers();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert
        var pkgWriter = factory.Writers.Values.FirstOrDefault(w =>
            w.Operations.OfType<HeadingOperation>().Any(h => h.Text.Equals("my_pkg", StringComparison.Ordinal)));
        Assert.NotNull(pkgWriter);
        var headings = pkgWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Subprograms", StringComparison.Ordinal));
    }
}
