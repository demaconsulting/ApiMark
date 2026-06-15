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

        // Assert: Types heading still present (paragraph format, not table)
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Types", StringComparison.Ordinal));

        // Verify paragraph-per-type format: type name appears in a paragraph (not a table)
        var paragraphs = apiWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p => p.Text.Contains("my_type", StringComparison.Ordinal));
    }

    /// <summary>Builds data with a package containing a function subprogram for subprogram rendering tests.</summary>
    private static (VhdlEmitter emitter, IReadOnlyList<VhdlFileModel> fileModels) BuildPackageWithSubprogramsData()
    {
        var options = new VhdlGeneratorOptions { LibraryName = "TestLib" };
        var subprogramDecl = new VhdlSubprogramDecl(
            "my_func",
            VhdlSubprogramKind.Function,
            "FUNCTION my_func RETURN INTEGER",
            [],
            "INTEGER",
            new VhdlDocComment("A function.", null, []));
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

    /// <summary>Validates that subprogram sections do NOT contain a standalone italic kind paragraph in single-file output.</summary>
    /// <remarks>
    ///     The kind is already visible in the Signature line (FUNCTION/PROCEDURE keyword),
    ///     so the redundant italic attribution paragraph was removed.
    /// </remarks>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_PackageWithSubprograms_NoKindAttributionParagraph()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildPackageWithSubprogramsData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: no standalone italic kind paragraph (*Function* or *Procedure*) should appear
        var apiWriter = factory.GetWriter("", "api");
        var paragraphs = apiWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.DoesNotContain(paragraphs, p =>
            p.Text.Equals("*Function*", StringComparison.Ordinal) ||
            p.Text.Equals("*Procedure*", StringComparison.Ordinal));
    }

    /// <summary>Validates that subprogram sections contain a Signature heading in single-file output.</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_PackageWithSubprograms_SubprogramSectionContainsSignatureHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildPackageWithSubprogramsData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: Signature heading must appear in the api output for the subprogram
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Signature", StringComparison.Ordinal));
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

    /// <summary>Validates that the architecture paragraph in single-file output includes the source filename.</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_EntityWithArchitecture_ArchitectureParagraphContainsFilename()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildEntityWithArchData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: an architecture paragraph must contain both the bold architecture name and the source filename,
        // distinguishing it from the entity attribution paragraph which also contains the filename
        var apiWriter = factory.GetWriter("", "api");
        var paragraphs = apiWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p =>
            p.Text.Contains("**behavioral**", StringComparison.Ordinal) &&
            p.Text.Contains("`test.vhd`", StringComparison.Ordinal));
    }

    /// <summary>Validates that an entity with no generics still emits a Generics section heading in single-file output.</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_EntityWithNoGenerics_EmitsGenericsHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildMinimalData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: Generics heading must appear even when the entity has no generics
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Generics", StringComparison.Ordinal));
    }

    /// <summary>Validates that an entity with no generics emits a none-placeholder paragraph in single-file output.</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_EntityWithNoGenerics_EmitsNonePlaceholderInGenericsSection()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildMinimalData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: none-placeholder paragraph must appear in the api output
        var apiWriter = factory.GetWriter("", "api");
        var paragraphs = apiWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p => p.Text.Equals(VhdlEmitter.NoItemsPlaceholder, StringComparison.Ordinal));
    }

    /// <summary>Validates that an entity section includes an attribution paragraph naming the source file in single-file output.</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_Entity_SectionContainsEntityAttributionParagraph()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildMinimalData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: attribution paragraph must identify kind and source filename
        var apiWriter = factory.GetWriter("", "api");
        var paragraphs = apiWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p =>
            p.Text.Contains("Entity", StringComparison.Ordinal) &&
            p.Text.Contains("`test.vhd`", StringComparison.Ordinal));
    }

    /// <summary>Validates that a package section includes an attribution paragraph naming the source file in single-file output.</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_Package_SectionContainsPackageAttributionParagraph()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildPackageWithTypesData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: attribution paragraph must identify kind and source filename
        var apiWriter = factory.GetWriter("", "api");
        var paragraphs = apiWriter.Operations.OfType<ParagraphOperation>().ToList();
        Assert.Contains(paragraphs, p =>
            p.Text.Contains("Package", StringComparison.Ordinal) &&
            p.Text.Contains("`test.vhd`", StringComparison.Ordinal));
    }

    /// <summary>Builds data with a subprogram that has formal parameters for Parameters-section tests.</summary>
    private static (VhdlEmitter emitter, IReadOnlyList<VhdlFileModel> fileModels) BuildPackageWithParameterizedSubprogramData()
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

    /// <summary>Validates that a subprogram with formal parameters emits a Parameters heading in single-file output.</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_SubprogramWithParameters_EmitsParametersHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildPackageWithParameterizedSubprogramData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: Parameters heading must appear in the api output
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Parameters", StringComparison.Ordinal));
    }

    /// <summary>Validates that a function subprogram emits a Returns heading in single-file output.</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_FunctionSubprogram_EmitsReturnsHeading()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildPackageWithParameterizedSubprogramData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: Returns heading must appear because ReturnType is set
        var apiWriter = factory.GetWriter("", "api");
        var headings = apiWriter.Operations.OfType<HeadingOperation>().ToList();
        Assert.Contains(headings, h => h.Text.Equals("Returns", StringComparison.Ordinal));
    }

    /// <summary>Validates that the Parameters table headers are Name, Type, Description (no Mode column).</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_SubprogramWithParameters_ParametersTableHasCorrectHeaders()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildPackageWithParameterizedSubprogramData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: Parameters table headers must be Name | Type | Description (no Mode column)
        var apiWriter = factory.GetWriter("", "api");
        var table = apiWriter.Operations.OfType<TableOperation>()
            .First(t => t.Headers.Length == 3 && t.Headers[0] == "Name" && t.Headers[1] == "Type");
        Assert.Equal(["Name", "Type", "Description"], table.Headers);
    }

    /// <summary>Validates that a parameter with no direction shows the bare type name in the Type cell.</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_SubprogramWithPlainParameter_TypeCellIsBareTypeName()
    {
        // Arrange
        var factory = new InMemoryMarkdownWriterFactory();
        var (emitter, fileModels) = BuildPackageWithParameterizedSubprogramData();

        // Act
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: parameter with empty Mode emits bare type name without direction prefix
        var apiWriter = factory.GetWriter("", "api");
        var table = apiWriter.Operations.OfType<TableOperation>()
            .First(t => t.Headers.Length == 3 && t.Headers[0] == "Name" && t.Headers[1] == "Type");
        var row = Assert.Single(table.Rows);
        Assert.Equal("v", row[0]);
        Assert.Equal("STD_LOGIC_VECTOR", row[1]);
        Assert.Equal("The input vector.", row[2]);
    }

    /// <summary>Validates that a parameter with a direction and class keyword shows direction-prefixed type, class stripped.</summary>
    [Fact]
    public void VhdlEmitterSingleFile_Emit_SubprogramWithDirectedParameter_TypeCellPrefixedWithDirection()
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
        new VhdlEmitterSingleFile(emitter, fileModels).Emit(factory, new EmitConfig { Format = OutputFormat.SingleFile }, new InMemoryContext());

        // Assert: SIGNAL is stripped, OUT is prefixed to the type name
        var apiWriter = factory.GetWriter("", "api");
        var table = apiWriter.Operations.OfType<TableOperation>()
            .First(t => t.Headers.Length == 3 && t.Headers[0] == "Name" && t.Headers[1] == "Type");
        var row = Assert.Single(table.Rows);
        Assert.Equal("OUT STD_LOGIC_VECTOR", row[1]);
    }
}
