# VhdlEmitterSingleFile

<!-- All sections below are MANDATORY. -->

## Responsibility

VhdlEmitterSingleFile writes all VHDL API documentation into a single `api.md`
file using heading levels offset by `EmitConfig.HeadingDepth`.

## Output Structure

Single `api.md` file:

- H{depth} library name
- H{depth+1} Entities section (one H{depth+2} per entity, with H{depth+3} Generics/Ports/Architectures sub-sections)
- H{depth+1} Packages section (one H{depth+2} per package)

## Algorithm

1. Create `factory.CreateMarkdown("", "api")` — the single output file.
2. Write H{depth} library name heading and optional description.
3. Write Entities section with nested entity headings, generics/ports tables, and architecture sub-sections.
4. Write Packages section with nested package headings and summaries.

## Design Decisions

- Only one file is created, so `factory.CreateMarkdown("", "api")` is called exactly once.
- Heading levels are offset by `config.HeadingDepth` to allow embedding this output
  as a section inside a larger document.
