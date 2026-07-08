## VhdlEmitterSingleFile

![VhdlEmitterSingleFile Structure](ApiMarkVhdlView.svg)

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

- H{depth} {library name} API Reference.
- H{depth+1} Entities section — written only when at least one entity is present;
  one H{depth+2} per entity, with H{depth+3} Generics (always written; uses
  `NoItemsPlaceholder` when empty), optional H{depth+3} Ports (omitted when the
  entity has no ports), and optional H{depth+3} Architectures sub-sections (omitted
  when no architectures implement the entity).
- H{depth+1} Packages section — written only when at least one package is present;
  one H{depth+2} per package, with:
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
  4. Write Entities section: H{depth+1} heading, then for each entity delegate to
     `EmitEntitySection`.
  5. Write Packages section: H{depth+1} heading, then for each package delegate to
     `EmitPackageSection`, which calls `EmitSubprogramSection` for each subprogram.

**VhdlEmitterSingleFile.EmitEntitySection** (private static): Writes the per-entity
block within the single-file output.

- H{depth+2} entity name, `*Entity declared in \`{fileName}\`*` attribution
  paragraph, summary, details, Generics section (H{depth+3} — table when generics
  are present, `NoItemsPlaceholder` paragraph when empty), optional Ports table
  (H{depth+3}), optional Architectures sub-section (H{depth+3}, one bold paragraph
  per architecture formatted as `**{name}** (\`{fileName}\`): {summary}` with
  optional details).

**VhdlEmitterSingleFile.EmitPackageSection** (private static): Writes the
per-package block within the single-file output.

- H{depth+2} package name, `*Package declared in \`{fileName}\`*` attribution
  paragraph, summary, details, optional Types section (H{depth+3}),
  optional Constants section (H{depth+3}), optional Components section (H{depth+3}),
  then calls `EmitSubprogramSection` for each subprogram.

**VhdlEmitterSingleFile.EmitSubprogramSection** (private static): Writes the
per-subprogram block within the single-file output.

- H{depth+3} subprogram name, summary, details, optional Parameters table
  (H{depth+4}), optional Returns section (H{depth+4}, functions only), Signature
  fenced code block (H{depth+4}).

### Error Handling

- Exceptions from `IMarkdownWriterFactory.CreateMarkdown` or from the Markdown writer
  propagate to the caller (`VhdlEmitter.Emit`) without wrapping.
- Missing or null doc-comment fields produce the `VhdlEmitter.NoDescriptionPlaceholder`
  string in output cells rather than throwing.

### Dependencies

- **VhdlEmitter** (internal) — instantiates this class and supplies `Options` and
  shared helpers (`GetSummary`, `DescriptionColumnHeader`, `NoDescriptionPlaceholder`,
  `NoItemsPlaceholder`).
- **IMarkdownWriterFactory** (ApiMarkCore) — used to create the single Markdown writer.
- **VhdlAstModel** (internal) — consumes `VhdlFileModel`, `VhdlEntityDecl`,
  `VhdlArchitectureDecl`, `VhdlPackageDecl`, `VhdlSubprogramDecl`, and
  `VhdlParamDecl` record types.

### Callers

- **VhdlEmitter** — instantiates and calls `Emit` when `config.Format` is `SingleFile`.
