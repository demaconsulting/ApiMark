## ANTLR4

ANTLR4 is a parser-generator tool. In ApiMark it was used **once**, manually,
to generate C# lexer and parser source files from the `vhdl2008.g4` grammar.
Those generated files are committed to the repository and treated as ordinary
source from that point on. ANTLR4 itself is not invoked during a normal build
or CI run.

The `Antlr4.Runtime.Standard` NuGet package is the associated runtime library
that the generated parser code depends on. It is the only ANTLR4-related build
or runtime dependency.

### Purpose

ANTLR4 was chosen because the `antlr/grammars-v4` project maintains a
production-quality `vhdl2008.g4` grammar covering the full VHDL-2008 standard.
Writing an equivalent parser by hand would be prohibitively complex and brittle.
By generating the parser once and committing the result, ApiMark gains a
complete, standards-conformant VHDL parser with no ongoing tool dependency.

Regeneration would only be needed if ApiMark were extended to support a newer
VHDL language standard (e.g., VHDL-2019) and a suitable updated grammar became
available.

### Features Used

- **Code generation (one-time)** — ANTLR4 tool was invoked once to generate
  `vhdl2008Lexer.cs`, `vhdl2008Parser.cs`, `vhdl2008Visitor.cs`,
  `vhdl2008BaseVisitor.cs`, and associated `.interp` and `.tokens` data files
  from `vhdl2008.g4`. Regeneration instructions (including the specific jar
  version) are in `src/ApiMark.Vhdl/VhdlAst/Antlr/README.md`.
- **Runtime support** — `Antlr4.Runtime.Standard` NuGet package provides the
  base classes and token-stream infrastructure that the generated code depends
  on at runtime.

### Integration Pattern

The generated files live in `src/ApiMark.Vhdl/VhdlAst/Antlr/` and are marked
`generated_code = true` in `.editorconfig` so that formatting and lint tools
skip them. `VhdlAstParser` imports the generated `vhdl2008Lexer` and
`vhdl2008Parser` classes directly; no wrapper or adapter layer is introduced.

Regeneration instructions are maintained in
`src/ApiMark.Vhdl/VhdlAst/Antlr/README.md`. That file describes the required
Java runtime, the specific ANTLR4 jar version, and the exact command to run.

### Known Compiler Suppressions

The `vhdl2008.g4` grammar contains a rule named `base`, which conflicts with
the C# `base` keyword in the generated output. `ApiMark.Vhdl.csproj` suppresses
warnings CS1041, CS1584, CS1658, and CS3021 project-wide to accommodate this;
these suppressions do not affect any hand-written source files.
