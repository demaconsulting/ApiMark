---
name: sysml2tools-query
description: Query this repository's SysML2 architecture model (docs/sysml2/) to understand software structure, purpose, and relationships before deep-diving into source code. Use this when asked to understand the codebase, find which unit implements something, assess the impact of a change, or trace requirements to code.
---

# SysML2Tools Query Skill

This repository models its software structure in SysML2 under `docs/sysml2/`. Prefer
querying this model over grepping/reading source files when you need to understand
*what* a piece of the system is, *why* it exists, or *what it depends on*.

## Prerequisites

The `sysml2tools` CLI is a local .NET tool pinned in `.config/dotnet-tools.json`.
Restore it once per session if not already available:

```pwsh
dotnet tool restore
```

## Model files

- `docs/sysml2/model/{system-name}.sysml` — system-level `part def` only (no subsystems/units
  inline). A repository may contain more than one system.
- `docs/sysml2/model/{system-name}/{subsystem-name}.sysml` and
  `docs/sysml2/model/{system-name}/{subsystem-name}/{unit-name}.sysml` — one file per
  Subsystem/Unit, nested to mirror the same folder depth as that item's companion
  `docs/design/`, `docs/reqstream/`, and `docs/verification/` files.
- `docs/sysml2/model/ots.sysml` — off-the-shelf (OTS) dependency parts (present when OTS items
  exist).
- `docs/sysml2/model/shared.sysml` — Shared Package parts (present when Shared Package items
  exist).
- `docs/sysml2/views/design-views.sysml` — named views rendered for the design document; a
  sibling of `model/`, not nested inside it. Not usually needed for query workflows, but
  useful to see how diagrams are scoped.

Always pass all `.sysml` files (or a glob covering them) to every `sysml2tools`
invocation — the model spans multiple files and cross-references between them; files that
reopen the same `package Name { ... }` from different files merge into one namespace
automatically, no `import` required. As of `sysml2tools` 0.1.0-beta.5, every subcommand
(`lint`, `render`, `query`) expands `*`/`**` glob patterns internally and recurses
correctly — pass a single **quoted** glob string and let the tool resolve it, rather than
relying on the shell (quoting works identically in both PowerShell and bash):

```pwsh
dotnet sysml2tools query list 'docs/sysml2/model/**/*.sysml'
```

## Artifact-location metadata

Every System/Subsystem/Unit `part def` carries named `comment` elements pointing at its
companion artifacts (source, test, design, verification, and — for repositories that still
use ReqStream — requirements file). `query describe` returns every comment attached to
an element, so one query gets you every artifact location for that item without grepping the
source tree:

```pwsh
dotnet sysml2tools query describe -e {SystemName}::{UnitName} 'docs/sysml2/model/**/*.sysml'
```

```text
- Documentation: One-line purpose statement for the unit.
- Comment: Source: src/{Project}/{Subsystem}/{UnitName}.cs
- Comment: Test: test/{Project}.Tests/{Subsystem}/{UnitName}Tests.cs
- Comment: Design: docs/design/{system-name}/{subsystem-name}/{unit-name}.md
- Comment: Verification: docs/verification/{system-name}/{subsystem-name}/{unit-name}.md
- Comment: Requirements: docs/reqstream/{system-name}/{subsystem-name}/{unit-name}.yaml
```

Not every item has all five comments — Systems/Subsystems have no `Source` comment, and
units without a dedicated companion doc point at their parent's doc instead (see the
Artifact-Location Comments section of `sysml2-modeling.md` for the full rules). Prefer these
paths over guessing a file location from the unit name; fall back to `grep`/`glob` only when
a comment is missing or the model is stale.

## Recommended workflow

1. **Discover the system(s) present** — do not assume a fixed name:

   ```pwsh
   dotnet sysml2tools query list --kind "part def" 'docs/sysml2/model/**/*.sysml'
   ```

   Look for the top-level part def with no containing part (or the one with the most
   children) to identify each system's qualified name.

2. **Get the full hierarchy** (subsystems and units) for a system found above:

   ```pwsh
   dotnet sysml2tools query describe -e <QualifiedName> 'docs/sysml2/model/**/*.sysml'
   ```

   `describe` lists direct children and every artifact-location comment; repeat on each
   child to walk deeper, or use `query list` to see the full flat inventory.

3. **Understand a specific unit's purpose and find its files** before opening its source
   file — `describe` returns both the purpose `doc` and every `Comment:` artifact-location
   line in one call (see Artifact-location metadata above):

   ```pwsh
   dotnet sysml2tools query describe -e <QualifiedName> 'docs/sysml2/model/**/*.sysml'
   ```

4. **Assess impact before editing a unit** — see what depends on it:

   ```pwsh
   dotnet sysml2tools query used-by -e <QualifiedName> 'docs/sysml2/model/**/*.sysml'
   dotnet sysml2tools query impact -e <QualifiedName> 'docs/sysml2/model/**/*.sysml'
   ```

   Note: this only surfaces structural (typing/containment) references modeled in
   `.sysml` — not full call-graph/behavioral dependencies. Confirm actual usage in source
   before making impact-sensitive changes.

5. **Trace requirements** linked to a unit, if modeled:

   ```pwsh
   dotnet sysml2tools query requirements -e <QualifiedName> 'docs/sysml2/model/**/*.sysml'
   ```

6. **Search by name or kind** when the qualified name is unknown:

   ```pwsh
   dotnet sysml2tools query find --name <PartialOrFullName> 'docs/sysml2/model/**/*.sysml'
   dotnet sysml2tools query list --kind "part def" 'docs/sysml2/model/**/*.sysml'
   ```

Use `--format json` on any query verb for machine-parsed output.

## Fallback

If the model is stale, doesn't yet cover a unit, or a query returns nothing useful, fall
back to `grep`/`glob`/reading source directly. When you finish work that adds or changes a
Unit/Subsystem, update the corresponding `.sysml` model file (including its artifact-location
comments) in the same change (mirrors the existing requirement to keep `docs/design/*.md` and
`docs/reqstream/*.yaml` companion artifacts in sync). See the `sysml2-modeling.md`
standard for full modeling and view-authoring conventions.

## Validating changes to the model

Before committing changes to any `.sysml` file, lint it:

```pwsh
dotnet sysml2tools lint 'docs/sysml2/**/*.sysml'
```
