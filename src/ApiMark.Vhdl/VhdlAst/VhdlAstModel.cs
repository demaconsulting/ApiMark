namespace ApiMark.Vhdl.VhdlAst;

/// <summary>Holds a parameter name and its documentation text from a --! doc comment.</summary>
/// <param name="Name">Parameter name as it appears in the @param tag.</param>
/// <param name="Description">Trimmed description text from the @param tag.</param>
public record VhdlParamDoc(string Name, string Description);

/// <summary>Holds structured documentation extracted from a VHDL --! doc comment block.</summary>
/// <param name="Summary">Brief description from @brief or first plain paragraph.</param>
/// <param name="Details">Extended description body text.</param>
/// <param name="Params">One entry per @param tag.</param>
/// <param name="Returns">Return description from @return tag, or null.</param>
public record VhdlDocComment(string? Summary, string? Details, IReadOnlyList<VhdlParamDoc> Params, string? Returns = null);

/// <summary>Represents a port in a VHDL entity declaration.</summary>
/// <param name="Name">Port name.</param>
/// <param name="Direction">Port direction: IN, OUT, INOUT, BUFFER, or in (default).</param>
/// <param name="TypeName">Port type as declared in source.</param>
/// <param name="Doc">Documentation extracted from inline --! comment, or null.</param>
public record VhdlPortDoc(string Name, string Direction, string TypeName, VhdlDocComment? Doc);

/// <summary>Represents a generic parameter in a VHDL entity declaration.</summary>
/// <param name="Name">Generic name.</param>
/// <param name="TypeName">Generic type as declared in source.</param>
/// <param name="DefaultValue">Default value expression text, or null.</param>
/// <param name="Doc">Documentation extracted from inline --! comment, or null.</param>
public record VhdlGenericDoc(string Name, string TypeName, string? DefaultValue, VhdlDocComment? Doc);

/// <summary>Represents a VHDL entity declaration with its generics and ports.</summary>
/// <param name="Name">Entity name.</param>
/// <param name="Generics">List of generic parameters.</param>
/// <param name="Ports">List of ports.</param>
/// <param name="Doc">Documentation from preceding --! block comment, or null.</param>
public record VhdlEntityDecl(string Name, IReadOnlyList<VhdlGenericDoc> Generics, IReadOnlyList<VhdlPortDoc> Ports, VhdlDocComment? Doc);

/// <summary>Represents a VHDL architecture body declaration.</summary>
/// <param name="Name">Architecture name.</param>
/// <param name="EntityName">Name of the entity this architecture implements.</param>
/// <param name="Doc">Documentation from preceding --! block comment, or null.</param>
public record VhdlArchitectureDecl(string Name, string EntityName, VhdlDocComment? Doc);

/// <summary>Represents a VHDL package declaration.</summary>
/// <param name="Name">Package name.</param>
/// <param name="Doc">Documentation from preceding --! block comment, or null.</param>
public record VhdlPackageDecl(string Name, VhdlDocComment? Doc);

/// <summary>Represents all VHDL declarations parsed from a single source file.</summary>
/// <param name="FilePath">Absolute path to the source file.</param>
/// <param name="Entities">All entity declarations found in this file.</param>
/// <param name="Architectures">All architecture body declarations found in this file.</param>
/// <param name="Packages">All package declarations found in this file.</param>
public record VhdlFileModel(
    string FilePath,
    IReadOnlyList<VhdlEntityDecl> Entities,
    IReadOnlyList<VhdlArchitectureDecl> Architectures,
    IReadOnlyList<VhdlPackageDecl> Packages);
