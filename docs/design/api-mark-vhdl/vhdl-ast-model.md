# VhdlAstModel

<!-- All sections below are MANDATORY. -->

## Responsibility

VhdlAstModel defines the immutable record types that represent the parsed result
of a VHDL source file. These records are constructed exclusively by `VhdlAstParser`
and consumed by the VHDL emitters.

## Records

- **VhdlParamDoc**: `(string Name, string Description)` — one entry per `@param` tag.
- **VhdlDocComment**: `(string? Summary, string? Details, IReadOnlyList<VhdlParamDoc> Params, string? Returns)` — structured doc comment.
- **VhdlPortDoc**: `(string Name, string Direction, string TypeName, VhdlDocComment? Doc)` — a single port in an entity.
- **VhdlGenericDoc**: `(string Name, string TypeName, string? DefaultValue, VhdlDocComment? Doc)` — a single generic in an entity.
- **VhdlEntityDecl**: `(string Name, IReadOnlyList<VhdlGenericDoc> Generics, IReadOnlyList<VhdlPortDoc> Ports, VhdlDocComment? Doc)` — an entity declaration.
- **VhdlArchitectureDecl**: `(string Name, string EntityName, VhdlDocComment? Doc)` — an architecture body declaration.
- **VhdlPackageDecl**: `(string Name, VhdlDocComment? Doc)` — a package declaration.
- **VhdlFileModel**: `(string FilePath, IReadOnlyList<VhdlEntityDecl> Entities, IReadOnlyList<VhdlArchitectureDecl> Architectures, IReadOnlyList<VhdlPackageDecl> Packages)` — all declarations from one file.

## Design Decisions

- All records use C# positional record syntax for conciseness.
- `IReadOnlyList<T>` is used for all collections to prevent mutation after construction.
- All text fields are pre-normalized by `VhdlAstParser` before being stored in the records.
- `null` means "no documentation found" for optional doc fields.
