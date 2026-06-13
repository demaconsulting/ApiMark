## VhdlEmitterGradualDisclosure

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

VhdlEmitterGradualDisclosure writes one Markdown file per VHDL declaration
(entity, architecture, package) plus an `api.md` index page, enabling gradual
disclosure navigation from the index to individual declaration pages.

### Data Model

**VhdlEmitterGradualDisclosure** (internal sealed class):

- `_emitter`: `VhdlEmitter` — parent emitter supplying options and shared helpers.
- `_fileModels`: `IReadOnlyList<VhdlFileModel>` — all parsed file models to emit.

**Output file layout**:

- `api.md` — library index listing all entities and packages with descriptions and
  relative Markdown links.
- `{entityName}.md` — entity detail page with generics table, ports table, and
  architecture list.
- `{archName}_{entityName}_arch.md` — architecture detail page (combined name avoids
  collision with entity pages of the same name).
- `{packageName}.md` — package detail page.

### Key Methods

**VhdlEmitterGradualDisclosure.Emit** (internal): Produces all Markdown output files.

- *Parameters*: `IMarkdownWriterFactory factory`, `EmitConfig config`,
  `IContext context`.
- *Returns*: `void`.
- *Preconditions*: `factory` is not null (enforced by the calling `VhdlEmitter`).
- *Postconditions*: all output files listed in the data model have been written.
- *Algorithm*:
  1. Collect all entities, architectures, and packages from all `VhdlFileModel`
     instances.
  2. Write `api.md`: H1 library name heading, optional description paragraph,
     entities table (Name/Description with relative links), packages table
     (Name/Description with relative links).
  3. For each entity: write H1 entity name, summary, details, Generics table
     (Name/Type/Default/Description), Ports table (Name/Direction/Type/Description),
     Architectures section.
  4. For each architecture: write H1 arch name, entity reference paragraph, summary.
  5. For each package: write H1 package name, summary.

### Error Handling

- Exceptions from `IMarkdownWriterFactory.CreateMarkdown` or from the Markdown writer
  propagate to the caller (`VhdlEmitter.Emit`) without wrapping.
- Missing or null doc-comment fields produce the `VhdlEmitter.NoDescriptionPlaceholder`
  string in table cells rather than throwing.

### Dependencies

- **VhdlEmitter** (internal) — instantiates this class and supplies `Options` and
  shared helpers (`GetSummary`, `DescriptionColumnHeader`, `NoDescriptionPlaceholder`).
- **IMarkdownWriterFactory** (ApiMarkCore) — used to create each per-file Markdown
  writer.
- **VhdlAstModel** (internal) — consumes `VhdlFileModel`, `VhdlEntityDecl`,
  `VhdlArchitectureDecl`, and `VhdlPackageDecl` record types.

### Callers

- **VhdlEmitter** — instantiates and calls `Emit` when `config.Format` is
  `GradualDisclosure`.
