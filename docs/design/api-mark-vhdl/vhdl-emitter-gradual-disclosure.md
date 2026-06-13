# VhdlEmitterGradualDisclosure

<!-- All sections below are MANDATORY. -->

## Responsibility

VhdlEmitterGradualDisclosure writes one Markdown file per VHDL declaration
(entity, architecture, package) plus an `api.md` index page, enabling gradual
disclosure navigation from the index to individual declaration pages.

## Output Structure

- `api.md` — library index listing all entities and packages with descriptions.
- `{entityName}.md` — entity detail page with generics table, ports table, and architecture list.
- `{archName}_{entityName}_arch.md` — architecture detail page.
- `{packageName}.md` — package detail page.

## Algorithm

1. Collect all entities, architectures, and packages from all `VhdlFileModel` instances.
2. Write `api.md`: H1 library name heading, optional description paragraph, entities table, packages table.
3. For each entity: write H1 entity name, summary, details, Generics table (Name/Type/Default/Description), Ports table (Name/Direction/Type/Description), Architectures section.
4. For each architecture: write H1 arch name, entity reference paragraph, summary.
5. For each package: write H1 package name, summary.

## Design Decisions

- Architecture pages use the combined name `{archName}_{entityName}_arch` to avoid
  collision with entity pages of the same name.
- Entities table and packages table link to the corresponding detail pages using
  relative Markdown links (e.g. `[counter](counter.md)`).
