# VhdlAstParser

<!-- All sections below are MANDATORY. -->

## Responsibility

VhdlAstParser reads a VHDL source file, pre-processes its lines to extract --!
doc comments, invokes the ANTLR4 vhdl2008 grammar to parse the file, and walks
the resulting parse tree to produce a `VhdlFileModel`.

## Interface

```csharp
internal static class VhdlAstParser
{
    internal static VhdlFileModel Parse(string filePath);
}
```

## Algorithm

1. Read the file content as `string sourceText`; split into `string[] lines`.
2. Create ANTLR4 pipeline: `AntlrInputStream` → `vhdl2008Lexer` → `CommonTokenStream` → `vhdl2008Parser`.
3. Call `parser.design_file()` to obtain the root `Design_fileContext`.
4. Walk the tree using a `private sealed class VhdlVisitor : vhdl2008BaseVisitor<object?>`.
5. **VisitEntity_declaration**: extract name via `context.identifier(0).GetText()`;
   extract preceding doc comment; parse generics and ports from `entity_header()`.
6. **VisitArchitecture_body**: extract arch name via `context.identifier(0).GetText()`;
   extract entity name via `context.name().GetText()`; extract preceding doc comment.
7. **VisitPackage_declaration**: extract name and preceding doc comment.
8. **Preceding doc comment extraction**: walk backward from `declarationLine - 1`,
   collecting consecutive lines that begin with `--!` after trimming; reverse and
   parse with `ParseDocCommentLines`.
9. **Inline trailing comment extraction**: scan the raw line at the port/generic
   line number for the last `--!` occurrence; extract text after the marker.
10. **Identifier list expansion**: when `identifier_list` contains multiple
    identifiers, create one `VhdlPortDoc` or `VhdlGenericDoc` per identifier
    with the same type, direction, and default value.
11. **Type text extraction**: use `sourceText[ctx.Start.StartIndex..(ctx.Stop.StopIndex + 1)]`
    to recover the full type text including whitespace (which ANTLR's skip rules strip
    from `ctx.GetText()`).

## Doc Comment Tag Parsing

- `@brief <text>` → Summary
- `@param <name> <description>` → VhdlParamDoc entry
- `@return <text>` → Returns
- Non-tagged lines → Details (or Summary when no @brief is present)

## Design Decisions

- Doc comments are extracted from raw source lines rather than from the ANTLR
  token stream because the grammar discards comments via `-> skip` rules.
- The visitor uses overriding of only the three declaration visit methods; all
  other nodes are traversed by the default base class behavior which calls
  `VisitChildren` automatically.
- Port direction defaults to `"in"` per VHDL-2008 §6.5.2 when `mode_rule()` is null.
