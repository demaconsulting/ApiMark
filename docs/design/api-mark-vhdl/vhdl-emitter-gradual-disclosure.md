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
  2. Delegate to `EmitApiIndexPage` to write `api.md`.
  3. For each entity delegate to `EmitEntityPage`.
  4. For each package delegate to `EmitPackagePage`, which calls
     `EmitSubprogramDetailPage` for each subprogram.

**VhdlEmitterGradualDisclosure.EmitApiIndexPage** (private): Writes the `api.md`
index page.

- H1 library name heading, optional description paragraph, entities table
  (Name/Description with relative links), packages table (Name/Description with
  relative links).

**VhdlEmitterGradualDisclosure.EmitEntityPage** (private static): Writes a single
entity detail page.

- H1 entity name, `*Entity declared in \`{fileName}\`*` attribution paragraph,
  summary, details, Generics section (H2 — table when generics are present,
  `NoItemsPlaceholder` paragraph when empty), optional Ports table
  (H2 Name/Direction/Type/Description — section omitted when the entity has no ports),
  optional Architectures section (H2 — section omitted when no architectures implement
  the entity; when present, one bold entry per architecture formatted as
  `**{name}** (\`{fileName}\`): {summary}` with optional details paragraph).

**VhdlEmitterGradualDisclosure.EmitPackagePage** (private static): Writes a single
package detail page and calls `EmitSubprogramDetailPage` for each subprogram.

- H1 package name, `*Package declared in \`{fileName}\`*` attribution paragraph,
  summary, details, Types paragraphs, Constants paragraphs,
  Components as `**name** — summary` paragraphs, Subprograms section with links
  to per-subprogram detail pages.

**VhdlEmitterGradualDisclosure.EmitSubprogramDetailPage** (private static): Writes
one `{packageName}/{subprogramName}.md` detail file.

- H1 subprogram name, kind attribution, summary, details, Parameters table
  (Name/Type/Description — type formatted by `VhdlEmitter.FormatParamType`),
  optional Returns paragraph (functions only), Signature fenced code block.

### Error Handling

- Exceptions from `IMarkdownWriterFactory.CreateMarkdown` or from the Markdown writer
  propagate to the caller (`VhdlEmitter.Emit`) without wrapping.
- Missing or null doc-comment fields produce the `VhdlEmitter.NoDescriptionPlaceholder`
  string in table cells rather than throwing.

### Dependencies

- **VhdlEmitter** (internal) — instantiates this class and supplies `Options` and
  shared helpers (`GetSummary`, `DescriptionColumnHeader`, `NoDescriptionPlaceholder`,
  `NoItemsPlaceholder`).
- **IMarkdownWriterFactory** (ApiMarkCore) — used to create each per-file Markdown
  writer.
- **VhdlAstModel** (internal) — consumes `VhdlFileModel`, `VhdlEntityDecl`,
  `VhdlArchitectureDecl`, `VhdlPackageDecl`, `VhdlSubprogramDecl`, and
  `VhdlParamDecl` record types.

### Callers

- **VhdlEmitter** — instantiates and calls `Emit` when `config.Format` is
  `GradualDisclosure`.
