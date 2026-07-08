## VhdlAstParser

![VhdlAstParser Structure](ApiMarkVhdlView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

VhdlAstParser reads a VHDL source file, pre-processes its lines to extract `--!`
doc comments, invokes the ANTLR4 vhdl2008 grammar to parse the file, and walks
the resulting parse tree to produce a `VhdlFileModel`.

### Data Model

N/A - VhdlAstParser is a stateless static class; all state is local to each `Parse`
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
  2a. Replace the default `ConsoleErrorListener` on both the lexer and the parser
      with a `CollectingErrorListener`; call `ThrowIfErrors(filePath)` after
      `parser.design_file()` returns so that any syntax error throws an
      `InvalidOperationException` instead of silently producing a corrupt parse tree.
  3. Call `parser.design_file()` to obtain the root `Design_fileContext`.
  4. Walk the tree using a `private sealed class VhdlVisitor : vhdl2008BaseVisitor<object?>`.
  5. **VisitEntity_declaration**: extract name via `context.identifier(0).GetText()`;
     extract preceding doc comment; delegate generic parsing to `ParseEntityGenerics`
     and port parsing to `ParseEntityPorts`.
  6. **VisitArchitecture_body**: extract arch name via `context.identifier(0).GetText()`;
     extract entity name via `context.name().GetText()`; extract preceding doc comment.
  7. **VisitPackage_declaration**: extract name via `context.identifier(0).GetText()`
     and preceding doc comment. Iterates every `package_declarative_item` and
     dispatches by declaration type:
     - `full_type_declaration` / `subtype_declaration` → `VhdlTypeDecl(name, definition, doc)`.
     - `constant_declaration` → `VhdlConstantDecl(name, typeName, value?, doc)`.
     - `component_declaration` → `VhdlComponentDecl(name, doc)`.
     - `subprogram_declaration` — delegates to `ParseSubprogramDecl` to resolve
       the spec as a function or procedure, extract name, kind, formal parameters,
       return type, and signature, then appends a `VhdlSubprogramDecl`. Does not
       recurse into child contexts (returns null).
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

**VhdlAstParser.ParseEntityGenerics** (private): Extracts generic declarations
from an entity header context into a list of `VhdlGenericDoc` records.

- *Parameters*: `Entity_declarationContext context`.
- *Returns*: `List<VhdlGenericDoc>` — empty when no generic clause is present.
- *Algorithm*: navigates `entity_header → generic_clause → generic_list →
  interface_list → interface_declaration`; for each `interface_constant_declaration`
  expands the identifier list and records name, type, optional default value, and
  inline trailing doc comment.

**VhdlAstParser.ParseEntityPorts** (private): Extracts port declarations from an
entity header context into a list of `VhdlPortDoc` records.

- *Parameters*: `Entity_declarationContext context`.
- *Returns*: `List<VhdlPortDoc>` — empty when no port clause is present.
- *Algorithm*: navigates `entity_header → port_clause → port_list →
  interface_list → interface_declaration`; for each declaration delegates to
  `ParsePortInterfaceDeclaration` and appends the results.

**VhdlAstParser.ParsePortInterfaceDeclaration** (private): Converts a single
`Interface_declarationContext` into zero or more `VhdlPortDoc` records.

- *Parameters*: `Interface_declarationContext iface`.
- *Returns*: `IEnumerable<VhdlPortDoc>`.
- *Algorithm*: tries `interface_signal_declaration` first (explicit port); falls
  back to `interface_constant_declaration` (ANTLR may parse bare `name : IN type`
  as a constant). Both branches expand the identifier list and emit one record per
  identifier with direction (uppercase `IN`/`OUT`/`INOUT`/`BUFFER`) and type name.

**VhdlAstParser.ParseSubprogramDecl** (private): Resolves a
`Subprogram_declarationContext` into a `VhdlSubprogramDecl` record or returns null.

- *Parameters*: `Package_declarative_itemContext item`, `Subprogram_specificationContext spec`.
- *Returns*: `VhdlSubprogramDecl?` — null when the spec cannot be resolved.
- *Algorithm*: checks `subprogram_specification` for either a
  `function_specification` or `procedure_specification`; extracts name, kind,
  formal parameters via `ExtractFormalParameters`, return type (functions only),
  source-range signature text, and preceding doc comment.

**VhdlAstParser.ExtractFormalParameters** (private): Converts a
`Formal_parameter_listContext` into a list of `VhdlParamDecl` records.

- *Parameters*: `Formal_parameter_listContext? formalParams` — may be null.
- *Returns*: `List<VhdlParamDecl>` — empty list when `formalParams` is null
  or contains no interface declarations.
- *Algorithm*: iterates each `interface_object_declaration` (via LINQ
  `Select`/`Where` to skip nulls); tries three variants in order by delegating
  to extraction helpers:
  1. `ExtractSignalParams` — `interface_signal_declaration`.
  2. `ExtractVariableParams` — `interface_variable_declaration`.
  3. `ExtractConstantParams` — `interface_constant_declaration` (also matches
     undecorated parameters).

**VhdlAstParser.ExtractSignalParams** (private): Extracts `VhdlParamDecl` records
from an `Interface_signal_declarationContext`.

- Class keyword `SIGNAL` (optional) + direction from `mode_rule`; one record per identifier.

**VhdlAstParser.ExtractVariableParams** (private): Extracts `VhdlParamDecl` records
from an `Interface_variable_declarationContext`.

- Class keyword `VARIABLE` (optional) + direction from `mode_rule`; one record per identifier.

**VhdlAstParser.ExtractConstantParams** (private): Extracts `VhdlParamDecl` records
from an `Interface_constant_declarationContext`.

- Class keyword `CONSTANT` (optional) + direction `IN` when explicitly present; one record per identifier.

**VhdlAstParser.ExtractModeText** (private static): Converts a `Mode_ruleContext`
into an upper-case direction string.

- *Parameters*: `Mode_ruleContext? modeCtx` — may be null.
- *Returns*: `string` — one of `"OUT"`, `"INOUT"`, `"BUFFER"`, `"IN"`, or `""` when
  the context is null or specifies no explicit mode.

**VhdlAstParser.CollectingErrorListener** (private sealed class): ANTLR4 error
listener that accumulates syntax errors and throws rather than writing to
`Console.Error`.

- Implements both `IAntlrErrorListener<int>` (lexer) and `IAntlrErrorListener<IToken>`
  (parser).
- **ThrowIfErrors**: throws `InvalidOperationException` with all collected messages
  when at least one error was recorded; no-op otherwise.

**VhdlAstParser.ParseDocCommentLines** (private static): Parses a list of `--!` comment
lines into a `VhdlDocComment`, recognizing `@brief`, `@param`, and `@return` tags.

- `@brief <text>` → `Summary`
- `@param <name> <description>` → `VhdlParamDoc` entry in `Params`
- `@return <text>` → `Returns`
- Non-tagged lines → `Details` (or `Summary` when no `@brief` is present)

### Error Handling

- Parse errors from the ANTLR4 lexer or parser are collected by
  `CollectingErrorListener` and thrown as `InvalidOperationException` via
  `ThrowIfErrors` after `parser.design_file()` returns. This prevents silently
  producing corrupt output from partially recovered parse trees.
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
