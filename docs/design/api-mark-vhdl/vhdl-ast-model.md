## VhdlAstModel

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

VhdlAstModel defines the immutable record types that represent the parsed result
of a VHDL source file. These records are constructed exclusively by `VhdlAstParser`
and consumed by the VHDL emitters.

### Data Model

**VhdlParamDoc**: `(string Name, string Description)` — one entry per `@param` tag.

**VhdlDocComment**: `(string? Summary, string? Details, IReadOnlyList<VhdlParamDoc> Params,
string? Returns)` — structured doc comment extracted from `--!` comment blocks.

**VhdlPortDoc**: `(string Name, string Direction, string TypeName, VhdlDocComment? Doc)` —
a single port in an entity.

**VhdlGenericDoc**: `(string Name, string TypeName, string? DefaultValue,
VhdlDocComment? Doc)` — a single generic in an entity.

**VhdlEntityDecl**: `(string Name, IReadOnlyList<VhdlGenericDoc> Generics,
IReadOnlyList<VhdlPortDoc> Ports, VhdlDocComment? Doc)` — an entity declaration.

**VhdlArchitectureDecl**: `(string Name, string EntityName, VhdlDocComment? Doc)` —
an architecture body declaration.

**VhdlPackageDecl**: `(string Name, VhdlDocComment? Doc, IReadOnlyList<VhdlTypeDecl> Types,
IReadOnlyList<VhdlConstantDecl> Constants, IReadOnlyList<VhdlComponentDecl> Components,
IReadOnlyList<VhdlSubprogramDecl> Subprograms)` — a package declaration with all its
contained members.

**VhdlTypeDecl**: `(string Name, string Definition, VhdlDocComment? Doc)` — a type
declaration within a package.

**VhdlConstantDecl**: `(string Name, string TypeName, string? Value, VhdlDocComment? Doc)` —
a constant declaration within a package.

**VhdlComponentDecl**: `(string Name, VhdlDocComment? Doc)` — a component declaration
within a package.

**VhdlSubprogramKind**: enum with values `Procedure` and `Function` — distinguishes
procedures from functions in a package.

**VhdlParamDecl**: `(string Name, string Mode, string TypeName)` — a parameter in a
subprogram; `Mode` is the VHDL port mode (`IN`, `OUT`, `INOUT`, `BUFFER`) or the
object-class keyword for subprogram parameters.

**VhdlSubprogramDecl**: `(string Name, VhdlSubprogramKind Kind, string Signature,
IReadOnlyList<VhdlParamDecl> Parameters, string? ReturnType, VhdlDocComment? Doc)` —
a subprogram (procedure or function) declaration within a package.

**VhdlFileModel**: `(string FilePath, IReadOnlyList<VhdlEntityDecl> Entities,
IReadOnlyList<VhdlArchitectureDecl> Architectures,
IReadOnlyList<VhdlPackageDecl> Packages)` — all declarations extracted from one
VHDL source file.

Design invariants:

- All records use C# positional record syntax for conciseness.
- `IReadOnlyList<T>` is used for all collections to prevent mutation after construction.
- All text fields are pre-normalized by `VhdlAstParser` before being stored in the records.
- `null` means "no documentation found" for all optional doc fields.

### Key Methods

N/A — VhdlAstModel contains only immutable record definitions; it exposes no methods
beyond the C# positional-record auto-generated members (constructor, deconstruct,
equality).

### Error Handling

N/A — VhdlAstModel is a pure data model with no runtime logic; error handling is
the responsibility of `VhdlAstParser` during record construction.

### Dependencies

N/A — VhdlAstModel has no dependencies on other units, OTS packages, or shared
packages; it uses only BCL types (`IReadOnlyList<T>`, `string`).

### Callers

- **VhdlAstParser** — constructs all record instances from the ANTLR4 parse tree.
- **VhdlEmitter**, **VhdlEmitterGradualDisclosure**, **VhdlEmitterSingleFile** —
  consume the records to generate Markdown output.
