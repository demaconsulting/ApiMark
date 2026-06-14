## VhdlEmitterSingleFile

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

VhdlEmitterSingleFile writes all VHDL API documentation into a single `api.md`
file using heading levels offset by `EmitConfig.HeadingDepth`.

### Data Model

**VhdlEmitterSingleFile** (internal sealed class):

- `_emitter`: `VhdlEmitter` — parent emitter supplying options and shared helpers.
- `_fileModels`: `IReadOnlyList<VhdlFileModel>` — all parsed file models to emit.

**Output file layout** (single `api.md`):

- H{depth} library name.
- H{depth+1} Entities section — one H{depth+2} per entity, with H{depth+3}
  Generics, Ports, and Architectures sub-sections.
- H{depth+1} Packages section — one H{depth+2} per package, with:
  - H{depth+3} Types — one paragraph per type declaration.
  - H{depth+3} Constants — one paragraph per constant declaration.
  - H{depth+3} Components — one paragraph per component declaration.
  - H{depth+3} per subprogram, each containing H{depth+4} Parameters (table),
    H{depth+4} Returns (functions only), and H{depth+4} Signature sub-sections.

### Key Methods

**VhdlEmitterSingleFile.Emit** (internal): Produces the single consolidated Markdown
file.

- *Parameters*: `IMarkdownWriterFactory factory`, `EmitConfig config`,
  `IContext context`.
- *Returns*: `void`.
- *Preconditions*: `factory` is not null (enforced by the calling `VhdlEmitter`).
- *Postconditions*: a single `api.md` file containing all documented declarations
  has been written.
- *Algorithm*:
  1. Write `<!-- markdownlint-disable MD024 -->` as the first output to suppress
     duplicate-heading lint warnings caused by entities and packages sharing names.
  2. Create the output file via `factory.CreateMarkdown("", "api")` — called
     exactly once.
  3. Write H{depth} library name heading and optional description.
  4. Write Entities section: H{depth+1} heading, then for each entity an H{depth+2}
     heading followed by generics and ports tables and an Architectures sub-section.
  5. Write Packages section: H{depth+1} heading, then for each package an H{depth+2}
     heading followed by summary, Types (H{depth+3}, one paragraph per type),
     Constants (H{depth+3}, one paragraph per constant), Components (H{depth+3},
     one paragraph per component), and one H{depth+3} heading per subprogram
     containing Parameters table, Returns section (functions only), and Signature
     sub-sections at H{depth+4}.

### Error Handling

- Exceptions from `IMarkdownWriterFactory.CreateMarkdown` or from the Markdown writer
  propagate to the caller (`VhdlEmitter.Emit`) without wrapping.
- Missing or null doc-comment fields produce the `VhdlEmitter.NoDescriptionPlaceholder`
  string in output cells rather than throwing.

### Dependencies

- **VhdlEmitter** (internal) — instantiates this class and supplies `Options` and
  shared helpers (`GetSummary`, `DescriptionColumnHeader`, `NoDescriptionPlaceholder`).
- **IMarkdownWriterFactory** (ApiMarkCore) — used to create the single Markdown writer.
- **VhdlAstModel** (internal) — consumes `VhdlFileModel`, `VhdlEntityDecl`,
  `VhdlArchitectureDecl`, and `VhdlPackageDecl` record types.

### Callers

- **VhdlEmitter** — instantiates and calls `Emit` when `config.Format` is `SingleFile`.
