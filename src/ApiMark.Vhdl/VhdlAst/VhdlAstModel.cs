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
/// <param name="Direction">Port direction: one of IN, OUT, INOUT, or BUFFER (uppercase). Defaults to IN per VHDL-2008 when no explicit direction is written.</param>
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

/// <summary>Represents a type or subtype declaration in a VHDL package.</summary>
/// <param name="Name">Type name.</param>
/// <param name="Definition">Type definition text as declared in source.</param>
/// <param name="Doc">Documentation from preceding --! block comment, or null.</param>
public record VhdlTypeDecl(string Name, string Definition, VhdlDocComment? Doc);

/// <summary>Represents a constant declaration in a VHDL package.</summary>
/// <param name="Name">Constant name.</param>
/// <param name="TypeName">Constant type as declared in source.</param>
/// <param name="Value">Default value expression text, or null.</param>
/// <param name="Doc">Documentation from inline --! comment, or null.</param>
public record VhdlConstantDecl(string Name, string TypeName, string? Value, VhdlDocComment? Doc);

/// <summary>Represents a component declaration in a VHDL package.</summary>
/// <param name="Name">Component name.</param>
/// <param name="Doc">Documentation from preceding --! block comment, or null.</param>
public record VhdlComponentDecl(string Name, VhdlDocComment? Doc);

/// <summary>Specifies whether a subprogram declaration is a function or procedure.</summary>
public enum VhdlSubprogramKind
{
    /// <summary>Indicates a function subprogram.</summary>
    Function,

    /// <summary>Indicates a procedure subprogram.</summary>
    Procedure,
}

/// <summary>Represents a parameter in a VHDL subprogram declaration.</summary>
/// <param name="Name">Parameter name.</param>
/// <param name="Mode">Parameter mode token(s): one of the direction keywords (IN, OUT, INOUT, BUFFER), one of the object-class keywords (SIGNAL, VARIABLE, CONSTANT, FILE), a combination of both (e.g., SIGNAL IN), or an empty string when no explicit mode is specified.</param>
/// <param name="TypeName">Parameter type as declared in source.</param>
public record VhdlParamDecl(string Name, string Mode, string TypeName);

/// <summary>Represents a subprogram (function or procedure) declaration in a VHDL package.</summary>
/// <param name="Name">Subprogram name.</param>
/// <param name="Kind">Whether this is a function or procedure.</param>
/// <param name="Signature">Full signature text as declared in source.</param>
/// <param name="Parameters">List of formal parameters.</param>
/// <param name="ReturnType">Return type for functions, or null for procedures.</param>
/// <param name="Doc">Documentation from preceding --! block comment, or null.</param>
public record VhdlSubprogramDecl(string Name, VhdlSubprogramKind Kind, string Signature, IReadOnlyList<VhdlParamDecl> Parameters, string? ReturnType, VhdlDocComment? Doc);

/// <summary>Represents a VHDL package declaration.</summary>
/// <param name="Name">Package name.</param>
/// <param name="Doc">Documentation from preceding --! block comment, or null.</param>
/// <param name="Types">Type and subtype declarations in the package.</param>
/// <param name="Constants">Constant declarations in the package.</param>
/// <param name="Components">Component declarations in the package.</param>
/// <param name="Subprograms">Subprogram (function/procedure) declarations in the package.</param>
public record VhdlPackageDecl(
    string Name,
    VhdlDocComment? Doc,
    IReadOnlyList<VhdlTypeDecl> Types,
    IReadOnlyList<VhdlConstantDecl> Constants,
    IReadOnlyList<VhdlComponentDecl> Components,
    IReadOnlyList<VhdlSubprogramDecl> Subprograms);

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
