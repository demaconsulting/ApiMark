## CppEmitterSingleFile

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

CppEmitterSingleFile writes all C++ API documentation — classes, free functions,
enums, and type aliases — into a single `api.md` file. All content is organized
using heading levels offset by `EmitConfig.HeadingDepth`: H{depth} for the library
title, H{depth+1} for each namespace, H{depth+2} for each type, free function, enum,
or type alias, and H{depth+3} for individual members and class-scoped type aliases.
Type links are omitted in single-file mode to prevent anchor collisions when all
members share a single file. The path convention appendix is also omitted because it
only applies to the multi-file layout.

### Data Model

N/A - CppEmitterSingleFile holds no data model of its own. It reads from the
`SortedDictionary<string, CppEmitter.NamespaceDeclarations>` supplied by
`CppEmitter` and from `CppGeneratorOptions` via the parent emitter.

### Key Methods

**CppEmitterSingleFile.Emit** (internal): Entry point; writes all content into a
single `api.md` file.

- *Parameters*: `IMarkdownWriterFactory factory`, `EmitConfig config`,
  `IContext context`.
- *Returns*: `void`
- *Algorithm*: Calls `EmitSingleFile(factory, config)` which creates one writer
  via `factory.CreateMarkdown("", "api")`, writes the H{depth} library-name heading
  in the form `{LibraryName} API Reference`, optional description, and iterates over
  namespaces calling `WriteSingleFileClassSection`, `WriteSingleFileFreeFunctionSection`,
  `WriteSingleFileEnumSection`, and `WriteSingleFileTypeAliasSection`.

**WriteSingleFileClassSection** (private): Emits an H{depth+2} section for a
class.

- Writes the class name heading, optional parent-context note for nested types,
  signature block (when a source location is available), summary, details, note,
  example, a compact member bullet list, H{depth+3} sections for each visible
  member via `WriteSingleFileMemberSection`, H{depth+3} sub-entries for each
  class-scoped type alias, and peer H{depth+2} sections for nested classes.

**WriteSingleFileFreeFunctionSection** (private static): Emits an H{depth+2}
section for a free function, including parameter types in the heading,
a fenced-code signature block, summary, details, and a parameters table.

**WriteSingleFileEnumSection** (private static): Emits an H{depth+2} section for
a C++ enum, including summary and an enum values table.

**WriteSingleFileTypeAliasSection** (private static): Emits an H{depth+2} section
for a namespace-level C++ type alias.

- *Algorithm*: Writes the alias name as an H{depth+2} heading, a fenced `cpp`
  code block containing the qualified-name comment and `using {name} = {underlying};`
  declaration, and a summary paragraph (or the no-description placeholder when no
  doc comment is present). Mirrors the content produced by `WriteTypeAliasPage` in
  gradual-disclosure mode.

**WriteSingleFileMemberSection** (private static): Emits an H{depth+3} section
for a single class member (constructor, method, or field), including a fenced-code
signature block, summary, parameters table (when applicable), Returns line (for
non-void non-constructor methods), and example block (when present).

**WriteSingleFileParametersTable** (private static): Writes a Parameters table
with columns (Parameter, Type, Description) for a function when it has at least
one parameter.

**BuildFunctionSignature** (private static): Returns a one-line C++ function
signature string suitable for a fenced code block.

**GetMemberDisplayAndSummary** (private static): Returns the display name and
one-line summary for a class member; constructors use the class name as the
display name.

### Error Handling

N/A - CppEmitterSingleFile propagates exceptions from the factory and writer
without wrapping. No additional error handling is performed.

### External Interfaces

**IMarkdownWriterFactory (consumed)**: Received from `CppEmitter.Emit` and used
to create the single `api.md` writer via `CreateMarkdown("", "api")`.

- *Type*: In-process .NET interface.
- *Role*: Consumer — exactly one writer is created for the entire output.
- *Contract*: The factory must produce a valid `IMarkdownWriter` for the single
  call; the caller `using`-disposes the writer after writing.

### Dependencies

- **CppEmitter** — parent emitter providing options, visibility helpers, comment
  extractors, signature builders, and `GetIncludePath`.
- **CppAstModel** — consumes `CppClass`, `CppFunction`, `CppField`, `CppEnum`,
  and `CppTypeAlias` record types.
- **IMarkdownWriterFactory** (ApiMarkCore) — supplies the single Markdown writer.

### Callers

- **CppEmitter.Emit** — instantiates and calls `CppEmitterSingleFile.Emit` when
  `config.Format` is `SingleFile`.
