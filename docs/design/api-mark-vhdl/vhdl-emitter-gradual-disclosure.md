## VhdlEmitterGradualDisclosure

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

VhdlEmitterGradualDisclosure writes one Markdown file per VHDL entity and
package plus an `api.md` index page, enabling gradual disclosure navigation
from the index to individual declaration pages. Package subprograms each get
their own detail page under a per-package subfolder.

### Data Model

**VhdlEmitterGradualDisclosure** (internal sealed class):

- `_emitter`: `VhdlEmitter` — parent emitter supplying options and shared helpers.
- `_fileModels`: `IReadOnlyList<VhdlFileModel>` — all parsed file models to emit.

**Output file layout**:

- `api.md` — library index listing all entities and packages with descriptions and
  relative Markdown links.
- `{entityName}.md` — entity detail page with generics table, ports table, and
  inline architecture list.
- `{packageName}.md` — package detail page with types, constants, components, and
  subprogram index.
- `{packageName}/{subprogramName}.md` — subprogram detail page with parameters
  table, optional returns section, and signature.

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
     Architectures section (inline — one bold entry per architecture).
  4. For each package: write H1 package name, summary, details, Types paragraphs,
     Constants paragraphs, Components paragraphs, Subprograms section with links
     to per-subprogram detail pages.
  5. For each package subprogram: write `{packageName}/{subprogramName}.md` with
     H1 subprogram name, kind attribution, summary, Parameters table
     (Name/Mode/Type/Description), optional Returns paragraph, Signature code block.

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
  `VhdlArchitectureDecl`, `VhdlPackageDecl`, `VhdlSubprogramDecl`, and
  `VhdlParamDecl` record types.

### Callers

- **VhdlEmitter** — instantiates and calls `Emit` when `config.Format` is
  `GradualDisclosure`.
