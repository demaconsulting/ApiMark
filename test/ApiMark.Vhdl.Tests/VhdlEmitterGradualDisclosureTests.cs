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
        var subprogramDecl = new VhdlSubprogramDecl("my_func", VhdlSubprogramKind.Function, "FUNCTION my_func RETURN INTEGER", [], "INTEGER", new VhdlDocComment("A function.", null, []));
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

    /// <summary>Validates that the architecture paragraph on an entity page includes the source filename.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_WithArchitecture_ArchitectureParagraphContainsFilename()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithArchitecture();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the architecture paragraph must contain the source filename in backticks
        var entityWriter = factory.Writers.Values.FirstOrDefault(w =>
            w.Operations.OfType<HeadingOperation>().Any(h => h.Text.Equals("MyEntity", StringComparison.Ordinal)));
        Assert.NotNull(entityWriter);
        var paragraphs = entityWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p => p.Text.Contains("`test.vhd`", StringComparison.Ordinal));
    }

    /// <summary>Validates that an entity with no generics still emits a Generics section heading.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_EntityWithNoGenerics_EmitsGenericsHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildMinimalData();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: Generics heading must appear on the entity page even when the entity has no generics
        var entityWriter = factory.Writers.Values.FirstOrDefault(w =>
            w.Operations.OfType<HeadingOperation>().Any(h => h.Text.Equals("MyEntity", StringComparison.Ordinal)));
        Assert.NotNull(entityWriter);
        var headings = entityWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Generics", StringComparison.Ordinal));
    }

    /// <summary>Validates that an entity with no generics emits a none-placeholder paragraph in the Generics section.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_EntityWithNoGenerics_EmitsNonePlaceholderInGenericsSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildMinimalData();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: none-placeholder paragraph must appear on the entity page
        var entityWriter = factory.Writers.Values.FirstOrDefault(w =>
            w.Operations.OfType<HeadingOperation>().Any(h => h.Text.Equals("MyEntity", StringComparison.Ordinal)));
        Assert.NotNull(entityWriter);
        var paragraphs = entityWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p => p.Text.Equals(VhdlEmitter.NoItemsPlaceholder, StringComparison.Ordinal));
    }

    /// <summary>Validates that the entity page includes an attribution paragraph naming the source file.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_Entity_PageContainsEntityAttributionParagraph()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildMinimalData();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: attribution paragraph must identify kind and source filename
        var entityWriter = factory.Writers.Values.FirstOrDefault(w =>
            w.Operations.OfType<HeadingOperation>().Any(h => h.Text.Equals("MyEntity", StringComparison.Ordinal)));
        Assert.NotNull(entityWriter);
        var paragraphs = entityWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p =>
            p.Text.Contains("Entity", StringComparison.Ordinal) &&
            p.Text.Contains("`test.vhd`", StringComparison.Ordinal));
    }

    /// <summary>Validates that the package page includes an attribution paragraph naming the source file.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_Package_PageContainsPackageAttributionParagraph()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithPackageMembers();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: attribution paragraph must identify kind and source filename
        var pkgWriter = factory.Writers.Values.FirstOrDefault(w =>
            w.Operations.OfType<HeadingOperation>().Any(h => h.Text.Equals("my_pkg", StringComparison.Ordinal)));
        Assert.NotNull(pkgWriter);
        var paragraphs = pkgWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p =>
            p.Text.Contains("Package", StringComparison.Ordinal) &&
            p.Text.Contains("`test.vhd`", StringComparison.Ordinal));
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

        // Verify paragraph-per-type format: type name appears in a paragraph (not a table)
        var paragraphs = pkgWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p => p.Text.Contains("my_type", StringComparison.Ordinal));
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

        // Verify paragraph-per-constant format: constant name appears in a paragraph (not a table)
        var paragraphs = pkgWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p => p.Text.Contains("MY_CONST", StringComparison.Ordinal));
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

        // Verify paragraph-per-component format: component name appears in a paragraph (not a table)
        var paragraphs = pkgWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p => p.Text.Contains("my_comp", StringComparison.Ordinal));
    }

    /// <summary>Validates that package with members emits Subprograms section on its detail page in paragraph-link format.</summary>
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

        // Verify paragraph-link format: subprogram name appears in a paragraph (not a table)
        var paragraphs = pkgWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p => p.Text.Contains("my_func", StringComparison.Ordinal));
    }

    /// <summary>Validates that a subprogram detail file is created for each subprogram in a package.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_PackageWithSubprograms_CreatesSubprogramDetailFile()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithPackageMembers();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: detail file placed in per-package subfolder {pkg}/{subprogram} must exist
        Assert.True(factory.HasWriter("my_pkg", "my_func"), "Expected subprogram detail file 'my_pkg/my_func' to be created");
    }

    /// <summary>Validates that the subprogram detail file contains a Signature heading.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_PackageWithSubprograms_SubprogramDetailFileHasSignatureHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithPackageMembers();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: Signature heading must appear in the subprogram detail file
        var subWriter = factory.GetWriter("my_pkg", "my_func");
        var headings = subWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Signature", StringComparison.Ordinal));
    }

    /// <summary>Builds data with a subprogram that has formal parameters for Parameters-section tests.</summary>
    private static (VhdlEmitter emitter, IReadOnlyList<VhdlFileModel> fileModels) BuildDataWithParameterizedSubprogram()
    {
        var options = new VhdlGeneratorOptions { LibraryName = "TestLib" };
        var paramDoc = new VhdlParamDoc("v", "The input vector.");
        var param = new VhdlParamDecl("v", "", "STD_LOGIC_VECTOR");
        var subprogramDecl = new VhdlSubprogramDecl(
            "to_natural",
            VhdlSubprogramKind.Function,
            "FUNCTION to_natural(v : STD_LOGIC_VECTOR) RETURN NATURAL",
            [param],
            "NATURAL",
            new VhdlDocComment("Converts a vector.", null, [paramDoc], "The natural value."));
        var pkg = new VhdlPackageDecl(
            "my_pkg",
            new VhdlDocComment("A test package.", null, []),
            [],
            [],
            [],
            [subprogramDecl]);
        var fileModel = new VhdlFileModel("test.vhd", [], [], [pkg]);
        var fileModels = new List<VhdlFileModel> { fileModel };
        var emitter = new VhdlEmitter(options, fileModels);
        return (emitter, fileModels);
    }

    /// <summary>Validates that the subprogram detail file contains a Parameters heading when formal parameters are present.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_SubprogramWithParameters_DetailFileHasParametersHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithParameterizedSubprogram();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: Parameters heading must appear in the subprogram detail file
        var subWriter = factory.GetWriter("my_pkg", "to_natural");
        var headings = subWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Parameters", StringComparison.Ordinal));
    }

    /// <summary>Validates that the subprogram detail file contains a Returns heading for functions.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_FunctionSubprogram_DetailFileHasReturnsHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithParameterizedSubprogram();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: Returns heading must appear in the subprogram detail file because ReturnType is set
        var subWriter = factory.GetWriter("my_pkg", "to_natural");
        var headings = subWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Returns", StringComparison.Ordinal));
    }

    /// <summary>Validates that the Parameters table headers are Name, Type, Description (no Mode column).</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_SubprogramWithParameters_ParametersTableHasCorrectHeaders()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithParameterizedSubprogram();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: Parameters table headers must be Name | Type | Description (no Mode column)
        var subWriter = factory.GetWriter("my_pkg", "to_natural");
        var table = subWriter.Operations.OfType<TableOperation>().First();
        Assert.Equal(["Name", "Type", "Description"], table.Headers);
    }

    /// <summary>Validates that a parameter with no direction shows the bare type name in the Type cell.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_SubprogramWithPlainParameter_TypeCellIsBareTypeName()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithParameterizedSubprogram();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: parameter with empty Mode emits bare type name without direction prefix
        var subWriter = factory.GetWriter("my_pkg", "to_natural");
        var table = subWriter.Operations.OfType<TableOperation>().First();
        var row = Assert.Single(table.Rows);
        Assert.Equal("v", row[0]);
        Assert.Equal("STD_LOGIC_VECTOR", row[1]);
        Assert.Equal("The input vector.", row[2]);
    }

    /// <summary>Validates that a parameter with a direction and class keyword shows direction-prefixed type, class stripped.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_SubprogramWithDirectedParameter_TypeCellPrefixedWithDirection()
    {
        // Arrange — procedure parameter with SIGNAL class and OUT direction
        var factory = new InMemoryMarkdownWriterFactory();
        var options = new VhdlGeneratorOptions { LibraryName = "TestLib" };
        var paramDoc = new VhdlParamDoc("v", "The output vector.");
        var param = new VhdlParamDecl("v", "SIGNAL OUT", "STD_LOGIC_VECTOR");
        var subprogramDecl = new VhdlSubprogramDecl(
            "clear_vec",
            VhdlSubprogramKind.Procedure,
            "PROCEDURE clear_vec(SIGNAL v : OUT STD_LOGIC_VECTOR)",
            [param],
            null,
            new VhdlDocComment("Clears a vector.", null, [paramDoc]));
        var pkg = new VhdlPackageDecl("my_pkg", new VhdlDocComment("Pkg.", null, []), [], [], [], [subprogramDecl]);
        var fileModels = new List<VhdlFileModel> { new VhdlFileModel("test.vhd", [], [], [pkg]) };
        var emitter = new VhdlEmitter(options, fileModels);

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: SIGNAL is stripped, OUT is prefixed to the type name
        var subWriter = factory.GetWriter("my_pkg", "clear_vec");
        var table = subWriter.Operations.OfType<TableOperation>().First();
        var row = Assert.Single(table.Rows);
        Assert.Equal("OUT STD_LOGIC_VECTOR", row[1]);
    }

    /// <summary>Validates that the package page link for a subprogram uses the subfolder path format.</summary>
    [Fact]
    public void VhdlEmitterGradualDisclosure_Emit_PackageWithSubprograms_PackagePageLinkUsesSubfolderPath()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildDataWithPackageMembers();

        // Act
        new VhdlEmitterGradualDisclosure(emitter, fileModels).Emit(factory, new EmitConfig(), new InMemoryContext());

        // Assert: the package page paragraph linking to the subprogram must use the subfolder path
        var pkgWriter = factory.Writers.Values.FirstOrDefault(w =>
            w.Operations.OfType<HeadingOperation>().Any(h => h.Text.Equals("my_pkg", StringComparison.Ordinal)));
        Assert.NotNull(pkgWriter);
        var paragraphs = pkgWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p => p.Text.Contains("my_pkg/my_func.md", StringComparison.Ordinal));
    }
}
