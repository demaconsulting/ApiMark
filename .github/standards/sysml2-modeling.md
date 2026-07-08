---
name: SysML2 Modeling
description: Follow these standards when creating or modifying the SysML2 architecture
  model, its rendered views, or the software structure it feeds into design documentation.
globs: ["docs/sysml2/**/*.sysml"]
---

# SysML2 Modeling Standard

## Required Standards

Read these standards first before applying this standard:

- **`software-items.md`** - Software categorization (System/Subsystem/Unit/OTS/Shared Package)
- **`design-documentation.md`** - Design document structure that embeds the diagrams this
  standard produces

## Purpose

The repository's software structure is modeled in SysML2 under `docs/sysml2/` rather than
hand-maintained as prose or an ASCII tree. The model is the authoritative, machine-queryable
source of structure; rendered diagrams and `docs/design/introduction.md`'s narrative are
generated/derived artifacts. AI agents should query the model (see `sysml2tools-query`
skill) before deep-diving into source code to understand what a piece of the system is,
why it exists, and what it depends on.

## Repository Structure

```text
docs/sysml2/
├── model/
│   ├── {system-name}.sysml            # system-level part def only (no subsystems/units inline)
│   ├── {system-name}/
│   │   ├── {subsystem-name}.sysml     # subsystem part def; nests further for sub-subsystems
│   │   └── {subsystem-name}/
│   │       └── {unit-name}.sysml      # one file per unit
│   ├── ots.sysml                      # OTS dependency parts (optional; if OTS items exist)
│   └── shared.sysml                   # Shared Package parts (optional; if Shared Packages exist)
└── views/
    └── design-views.sysml             # named view usages rendered for the design document
```

`docs/sysml2/model/` mirrors the same folder shape as `docs/design/`, `docs/reqstream/`, and
`docs/verification/` — one `.sysml` file per System/Subsystem/Unit, at the same nesting depth
as that item's companion design/requirements/verification files. This keeps each file small
and keeps PRs that touch one unit's model from producing unrelated diffs in unrelated units.
`docs/sysml2/views/` is a sibling of `model/`, not nested inside it, so a single
`docs/sysml2/model/**/*.sysml` glob never needs to exclude view files (`sysml2tools` has no
exclude-glob syntax).

There is no separate "stable entry point" file — a repository may contain multiple systems,
so no single alias name would generalize. Agents discover the system(s) present by running
`query list --kind "part def"` or `query find` (see the `sysml2tools-query` skill) over
`docs/sysml2/model/**/*.sysml`, not by assuming a fixed name.

Always pass the full set of `.sysml` files (or an equivalent glob) to every `sysml2tools`
invocation — the model spans multiple files and cross-references between them; files sharing
the same `package Name { ... }` merge into one namespace automatically with no `import`
required, as long as all files are passed together. As of `sysml2tools` 0.1.0-beta.5,
`lint` and `render` both expand `*`/`**` glob patterns internally (recursing correctly into
subdirectories), so pass a single **quoted** glob string (e.g. `'docs/sysml2/**/*.sysml'`)
and let the tool resolve it — do not rely on the shell to expand it, and do not quote-strip
the pattern before it reaches the tool. Quoting keeps the behavior identical across
PowerShell and bash, since neither shell needs to (or reliably does) expand `**` itself.

## Model Content

- `{system-name}.sysml` defines one `part def` for the System only, with `part` usages
  referencing its direct Subsystems/Units (defined in their own files, see below).
- Each `{subsystem-name}.sysml` / `{unit-name}.sysml` defines exactly one `part def`, with a
  `doc /* ... */` comment stating its purpose — mirroring what would otherwise be written as
  prose in `docs/design/introduction.md`'s Software Structure section. Subsystem files add
  `part` usages for their own direct children (nested subsystems or units), matching the
  Software Item Hierarchy in `software-items.md`.
- `ots.sysml` / `shared.sysml` define one `part def` per OTS item / Shared Package, referenced
  as `part` usages from the system(s) that depend on them.

## Artifact-Location Comments

Every System/Subsystem/Unit `part def` records where its companion artifacts live, as named
`comment` elements — not `attribute`s (attribute values are not surfaced by `sysml2tools query
describe`, even in JSON output; comments are). Comments have zero effect on rendered diagrams
(verified: a part def with several comments attached renders with no extra nodes or size
change) and are fully returned by `query describe`, in declaration order, as
`Comment: ...` summary lines — this is how an agent gets every artifact location for a unit
in a single query, without grepping the source tree.

Use these comment names, in this order, when applicable to the item:

```sysml
part def {UnitName} {
    doc /* One-line purpose statement. */

    comment sourceRef /* Source: {path to the unit's source file} */
    comment testRef /* Test: {path to the unit's test file} */
    comment designRef /* Design: {path to the unit's design doc} */
    comment verificationRef /* Verification: {path to the unit's verification doc} */
    comment reqRef /* Requirements: {path to the unit's reqstream file} */
}
```

Not every item has all five:

- **Systems and Subsystems** have no `sourceRef` (no single source file represents a whole
  system or subsystem) — use `testRef` for the subsystem-level test file when one exists.
- **Units without a dedicated companion doc** (e.g. small data-model types documented inline
  within their subsystem's design doc rather than in their own file) point `designRef` /
  `verificationRef` / `reqRef` at the subsystem-level doc, with a short parenthetical note,
  e.g. `/* Design: docs/design/{system}/{subsystem}.md (documented as a supporting value type) */`.
- **Units without a dedicated test file** (exercised only indirectly through another unit's
  tests) omit `testRef` entirely rather than pointing at an unrelated test file.
- **OTS items** have no `sourceRef`/`testRef` (no local integration code); use `designRef`
  (when the optional design doc exists) / `verificationRef` / `reqRef` only.

This is prose-searchable metadata only — it is not a substitute for the authoritative
requirements/design/verification artifacts, and it does not replace ReqStream as the
requirements source of truth. (This repository currently keeps requirements traceability in
ReqStream; a future migration to native SysML2 `requirement`/`satisfy` modeling is a separate,
not-yet-scheduled change.)

## Views (`docs/sysml2/views/design-views.sysml`)

Views control what gets rendered into diagrams for the design document. Use named `view`
**usages** (not `view def` **definitions**) — `expose` is only valid inside a usage:

```sysml
package {SystemName} {
    view SoftwareStructureView {
        expose {SystemName};         // whole package: system + subsystems + units
    }

    view {SystemName}View {
        expose {SystemName}System;   // system def only, not expanded into subsystems
    }

    view {SubsystemName}View {
        expose {SubsystemName};      // one subsystem def only
    }
}
```

**Critical distinction** (do not confuse these — this cost significant trial-and-error to
discover): `expose <name>;` is what scopes a rendered diagram's content (the union of the
named element's containment subtree). `render <kind>;` selects a rendering *style* (e.g.
`asInterconnectionDiagram`) — it has **no effect on scope**. `expose` is only legal inside a named
`view Name { ... }` **usage**; it is a syntax/semantic error inside a `view def Name { ... }`
**definition**. `expose` targets must be `::`-qualified or locally-resolvable names, not
dotted member-access chains (`expose foo.bar;` is invalid — use `expose Bar;`).

**Naming convention** — one `View` suffix per rendered diagram, no `System`/`Subsystem`
suffix duplication:

- `SoftwareStructureView` - full detail: every system, subsystem, and unit in one diagram
- `{SystemName}View` - one per system, direct members only (not expanded into subsystems)
- `{SubsystemName}View` - one per subsystem (at any nesting depth), that subsystem's own
  members only

When a repository has multiple systems, `SoftwareStructureView` may expose each system's
package individually (`expose {SystemNameA}; expose {SystemNameB};`) or the whole workspace,
whichever produces a single coherent overview diagram.

## Diagram Embedding

Render with a single quoted recursive glob for the model tree, plus the views file
(`sysml2tools` 0.1.0-beta.5+ expands and recurses `**` internally, so no shell-side globbing
or explicit per-file listing is needed):

```pwsh
dotnet sysml2tools render `
  --output docs/design/generated --format svg `
  'docs/sysml2/model/**/*.sysml' `
  'docs/sysml2/views/design-views.sysml'
```

With multiple views declared and no `--view` flag, `sysml2tools` renders every declared view
in one invocation, one file per view, using each view's own name as the filename
(`{ViewName}.svg`) — no post-render rename step is needed.

Embed diagrams in `docs/design/` per this rule: every design doc for an item embeds the
diagram for the narrowest view that still shows that item's own immediate structure —

- `docs/design/introduction.md` — `SoftwareStructureView.svg` (the full-detail overview)
- `docs/design/{system-name}.md` and every unit doc for a unit directly under the system
  (no subsystem parent) — `{SystemName}View.svg`
- `docs/design/{system-name}/{subsystem-name}.md` and every unit doc nested under that
  subsystem (at any depth) — `{SubsystemName}View.svg`

Place the image directly under the file's top-level heading, before its first prose
subsection, e.g. `![{Name} Structure]({ViewName}.svg)`.

## Build and Lint Integration

- `sysml2tools lint` belongs in `lint.ps1`'s compliance-tools section, **not** as a separate
  step in the CI design-document job — `lint.ps1` gates every later job (including document
  generation) transitively, so linting the model there is both earlier and non-duplicated.
  Pass a single quoted recursive glob, e.g. `dotnet sysml2tools lint 'docs/sysml2/**/*.sysml'`
  — `sysml2tools` (0.1.0-beta.5+) expands and recurses this itself, so no
  `Get-ChildItem`/shell-side globbing is needed.
- The CI design-document job renders the views (see the render command above) before running
  Pandoc, so generated SVGs exist before HTML generation. Rename/stable-filename workarounds
  are unnecessary since view names are stable by definition. Pass the model and views globs as
  separate quoted arguments (e.g. `'docs/sysml2/model/**/*.sysml' 'docs/sysml2/views/design-views.sysml'`)
  — plain PowerShell (the default shell) is sufficient; no `shell: bash`/`shopt -s globstar`
  workaround is needed.
- Add `sysml2tools` to `.config/dotnet-tools.json` (`demaconsulting.sysml2tools.tool`) and to
  `.versionmark.yaml`'s captured tool list.

## Fallback

If the model is stale, doesn't yet cover an item, or a query returns nothing useful, fall
back to `grep`/`glob`/reading source directly. When work adds or changes a Unit/Subsystem,
update the corresponding `.sysml` model file in the same change — this mirrors the existing
requirement to keep `docs/design/*.md` and `docs/reqstream/*.yaml` companion artifacts in
sync.

## Quality Checks

- [ ] Every System/Subsystem/Unit in `docs/design/introduction.md` has a matching `part def`
  in its own file under `docs/sysml2/model/`, at the same folder depth as its companion
  design/reqstream/verification files, with a purpose `doc` comment
- [ ] Every Unit-level `part def` has `sourceRef`/`testRef`/`designRef`/`verificationRef`/
  `reqRef` comments where applicable (see Artifact-Location Comments for documented
  exceptions); System/Subsystem `part def`s have `testRef`/`designRef`/`verificationRef`/
  `reqRef` but no `sourceRef`
- [ ] `docs/sysml2/views/design-views.sysml` uses `view Name { expose ...; }` usages, not
  `view def` definitions
- [ ] View names follow the `SoftwareStructureView` / `{SystemName}View` /
  `{SubsystemName}View` convention
- [ ] `sysml2tools lint` passes on all `.sysml` files
- [ ] Rendered diagrams are embedded in every design doc per the Diagram Embedding rule
- [ ] `sysml2tools` is present in `lint.ps1`, `.config/dotnet-tools.json`, and
  `.versionmark.yaml`
