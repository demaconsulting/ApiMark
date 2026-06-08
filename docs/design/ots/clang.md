## Clang

Clang is a C/C++ compiler front-end from the LLVM project. In ApiMark, it is used exclusively
as a static parser — it is invoked via `clang -Xclang -ast-dump=json -fparse-all-comments
-fsyntax-only` to produce a JSON representation of the C++ abstract syntax tree (AST) for
a set of public header files. The JSON output is then parsed by `ClangAstParser` to build
the in-memory AST used by CppGenerator.

### Purpose

Clang was chosen as the parsing backend because it produces a complete, standards-compliant C++
AST without requiring a bundled parser library. The `-ast-dump=json` flag produces a
machine-readable JSON representation of the full translation unit, including all declarations,
types, template parameters, access specifiers, doc comments, and source-file provenance. The
`-fparse-all-comments` flag ensures that Doxygen `@brief`, `@param`, and `@return` comments
are included in the AST. Using system clang eliminates the bundled libclang dependency and allows
users to control the exact compiler version used for parsing.

### Features Used

- **AST JSON dump** — `clang -Xclang -ast-dump=json -fsyntax-only -fparse-all-comments` accepts a
  combined header file and produces a JSON AST on stdout. The top-level object contains an
  `"inner"` array of top-level declaration nodes, each with `"kind"`, `"name"`, `"loc"`, and
  nested `"inner"` arrays for children.
- **Source file provenance** — the `"loc"` and `"range"` fields on each node include a `"file"`
  property recording the absolute path of the source file in which the declaration appears. This
  is the primary mechanism by which `ClangAstParser` determines which declarations belong to a
  configured public include root.
- **Declaration metadata** — each node exposes its `"kind"` (`CXXRecordDecl`, `FunctionDecl`,
  `EnumDecl`, `FieldDecl`, etc.), `"name"`, `"type"`, `"access"`, `"isImplicit"`, and
  `"isDeprecated"` fields, which supply the metadata needed for documentation generation.
- **Template parameter nodes** — `ClassTemplateDecl` nodes wrap `CXXRecordDecl` children and
  carry `TemplateTypeParmDecl` and `NonTypeTemplateParmDecl` children that describe template
  parameters.
- **Doc comment nodes** — `FullComment`, `ParagraphComment`, `BlockCommandComment`,
  `ParamCommandComment`, and `TextComment` sub-nodes within each declaration carry the
  structured Doxygen documentation comment content.
- **Clang discovery** — the parser locates the clang executable automatically: by searching PATH
  on all platforms, by invoking clang through `xcrun` on macOS if the PATH search fails, and by
  querying vswhere for LLVM installations on Windows if both earlier strategies fail. An explicit
  path can be provided via `CppGeneratorOptions.ClangPath` to override discovery.

### Integration Pattern

Clang is consumed by `ClangAstParser` in `src/ApiMark.Cpp/CppAst/ClangAstParser.cs`. The
integration follows these steps:

1. `ClangAstParser.Parse` discovers the clang executable using the strategy described under
   _Features Used_ above, or uses `CppGeneratorOptions.ClangPath` if set.
2. The parser builds a combined `#include` header file that includes every file in the configured
   public include roots (after applying IncludePatterns and ExcludePatterns).
3. The parser constructs a clang command line: `-Xclang -ast-dump=json -fparse-all-comments
   -fsyntax-only` followed by include path flags (`-I`, `-isystem`), preprocessor defines (`-D`),
   and the combined header file.
4. Clang is launched via `Process.Start`; stdout is read and deserialized as a `JsonDocument`.
5. `ClangAstParser` walks the JSON AST, collecting only nodes whose `"loc.file"` falls under a
   configured public include root. The `"loc.file"` field is inherited by child nodes when not
   explicitly present (sticky-file semantics from the clang AST format).
6. The collected declarations are assembled into a `CppCompilationResult` and returned to
   `CppGenerator` for output generation.
