## VhdlAstParser

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

VhdlAstParser reads a VHDL source file, pre-processes its lines to extract `--!`
doc comments, invokes the ANTLR4 vhdl2008 grammar to parse the file, and walks
the resulting parse tree to produce a `VhdlFileModel`.

### Data Model

N/A — VhdlAstParser is a stateless static class; all state is local to each `Parse`
call and returned as a `VhdlFileModel`.

### Key Methods

**VhdlAstParser.Parse** (internal static): Parses a single VHDL source file and
returns its declaration model.

- *Parameters*: `string filePath` — absolute or working-directory-relative path to a
  `.vhd` or `.vhdl` source file.
- *Returns*: `VhdlFileModel` — all entities, architectures, and packages found in the
  file.
- *Preconditions*: `filePath` is not null and refers to a readable file.
- *Postconditions*: the returned `VhdlFileModel` contains all top-level declarations
  found in the file with associated doc comments; never returns null.
- *Algorithm*:
  1. Read the file content as `string sourceText`; split into `string[] lines`.
  2. Create the ANTLR4 pipeline: `AntlrInputStream` → `vhdl2008Lexer` →
     `CommonTokenStream` → `vhdl2008Parser`.
  3. Call `parser.design_file()` to obtain the root `Design_fileContext`.
  4. Walk the tree using a `private sealed class VhdlVisitor : vhdl2008BaseVisitor<object?>`.
  5. **VisitEntity_declaration**: extract name via `context.identifier(0).GetText()`;
     extract preceding doc comment; parse generics and ports from `entity_header()`.
  6. **VisitArchitecture_body**: extract arch name via `context.identifier(0).GetText()`;
     extract entity name via `context.name().GetText()`; extract preceding doc comment.
  7. **VisitPackage_declaration**: extract name via `context.identifier(0).GetText()`
     and preceding doc comment. Iterates every `package_declarative_item` and
     dispatches by declaration type:
     - `full_type_declaration` / `subtype_declaration` → `VhdlTypeDecl(name, definition, doc)`.
     - `constant_declaration` → `VhdlConstantDecl(name, typeName, value?, doc)`.
     - `component_declaration` → `VhdlComponentDecl(name, doc)`.
     - `subprogram_declaration` — resolves the spec as a function or procedure
       designator, extracts name, kind (`VhdlSubprogramKind.Function` or
       `.Procedure`), formal parameters via `ExtractFormalParameters`, return type
       (function only), source-range signature text, and preceding doc comment, then
       appends a `VhdlSubprogramDecl`. Does not recurse into child contexts (returns
       null).
  8. **Preceding doc comment extraction**: walk backward from `declarationLine - 1`,
     collecting consecutive lines that begin with `--!` after trimming; reverse and
     parse with `ParseDocCommentLines`.
  9. **Inline trailing comment extraction**: scan the raw line at the port/generic
     line number for the last `--!` occurrence; extract text after the marker.
  10. **Identifier list expansion**: when `identifier_list` contains multiple
      identifiers, create one `VhdlPortDoc` or `VhdlGenericDoc` per identifier with
      the same type, direction, and default value.
  11. **Type text extraction**: use
      `sourceText[ctx.Start.StartIndex..(ctx.Stop.StopIndex + 1)]` to recover the
      full type text including whitespace (which ANTLR's skip rules strip from
      `ctx.GetText()`).

**VhdlAstParser.ExtractFormalParameters** (private): Converts a
`Formal_parameter_listContext` into a list of `VhdlParamDecl` records.

- *Parameters*: `Formal_parameter_listContext? formalParams` — may be null.
- *Returns*: `IReadOnlyList<VhdlParamDecl>` — empty list when `formalParams` is null
  or contains no interface declarations.
- *Algorithm*: iterates each `interface_object_declaration`; tries three variants in
  order:
  1. `interface_signal_declaration` — class keyword `SIGNAL` (optional) + direction
     from `mode_rule`; one `VhdlParamDecl` per identifier.
  2. `interface_variable_declaration` — class keyword `VARIABLE` (optional) +
     direction from `mode_rule`; one `VhdlParamDecl` per identifier.
  3. `interface_constant_declaration` — class keyword `CONSTANT` (optional) +
     direction `IN` (optional); one `VhdlParamDecl` per identifier. This variant
     also matches undecorated parameters (`v : STD_LOGIC_VECTOR`).
  The `Mode` field is built by joining the non-empty class keyword and direction
  strings with a single space.

**VhdlAstParser.ExtractModeText** (private static): Converts a `Mode_ruleContext`
into an upper-case direction string.

- *Parameters*: `Mode_ruleContext? modeCtx` — may be null.
- *Returns*: `string` — one of `"OUT"`, `"INOUT"`, `"BUFFER"`, `"IN"`, or `""` when
  the context is null or specifies no explicit mode.
a `VhdlDocComment`.

- `@brief <text>` → `Summary`
- `@param <name> <description>` → `VhdlParamDoc` entry in `Params`
- `@return <text>` → `Returns`
- Non-tagged lines → `Details` (or `Summary` when no `@brief` is present)

### Error Handling

- Parse errors from the ANTLR4 lexer or parser surface through the default ANTLR4
  error listener; callers should replace this listener when integration into the
  `IContext` logging channel is required.
- File I/O exceptions (e.g., `FileNotFoundException`, `IOException`) are not caught
  by `VhdlAstParser` and propagate to `VhdlGenerator`, which logs them via
  `context.WriteError` and skips the file.
- Malformed or missing doc-comment blocks produce `null` optional fields in the
  resulting records rather than throwing.

### Dependencies

- **VhdlAstModel** (internal) — produces record instances (`VhdlFileModel`,
  `VhdlEntityDecl`, `VhdlArchitectureDecl`, `VhdlPackageDecl`, `VhdlPortDoc`,
  `VhdlGenericDoc`, `VhdlDocComment`, `VhdlParamDoc`) defined there.
- **ANTLR4 Runtime** (NuGet OTS) — provides `AntlrInputStream`, `CommonTokenStream`,
  and the visitor base class.
- **vhdl2008 grammar** (generated) — `vhdl2008Lexer`, `vhdl2008Parser`,
  `vhdl2008BaseVisitor<T>`, and associated context types.

### Callers

- **VhdlGenerator** — calls `VhdlAstParser.Parse(filePath)` once per matched source
  file.
