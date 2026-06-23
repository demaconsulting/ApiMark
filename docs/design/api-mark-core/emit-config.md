## EmitConfig and OutputFormat

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

EmitConfig and OutputFormat together define the shared output-configuration
contract for the second stage of the generation pipeline. OutputFormat is a
simple two-value enum that discriminates between the supported file-layout
strategies; EmitConfig is a read-only value object that carries OutputFormat
and the heading-depth setting. EmitConfig is constructed by the CLI or
build-tool caller and passed to `IApiEmitter.Emit` so that the output format
and heading depth can be chosen at emit time without coupling them to
language-specific generator options.

### Data Model

**EmitConfig**: `sealed class` — immutable value object; all properties are
`init`-only.

**EmitConfig.Format**: `OutputFormat` — determines the file-layout strategy for
the emitted Markdown. Default: `OutputFormat.GradualDisclosure`. Values:

- `GradualDisclosure` — one file per concept (library index, namespace pages,
  type pages, member detail pages). This is the multi-file tree format.
- `SingleFile` — all content written into a single `api.md` file using offset
  heading levels.

**EmitConfig.HeadingDepth** defaults to `1` and is validated in the range 1–3 at
init time. Valid range: 1–3. The effective member heading level is `HeadingDepth + 3`;
at depth 3 members are at H6, which is the maximum heading level supported by Markdown.
Values outside 1–3 throw `ArgumentOutOfRangeException`. Ignored by `GradualDisclosure`
emitters.

**OutputFormat**: `enum` — discriminates between the two supported output
strategies.

- `GradualDisclosure` (value 0) — multi-file tree format.
- `SingleFile` (value 1) — single-file format.

### Key Methods

N/A - EmitConfig is a data object; it has no significant methods. `EmitConfig`
uses reference equality (standard `sealed class` behavior); `init` properties
do not affect equality semantics.

### Error Handling

`EmitConfig` enforces the `HeadingDepth` range at init time. Setting `HeadingDepth`
to a value less than 1 throws `ArgumentOutOfRangeException` (via
`ArgumentOutOfRangeException.ThrowIfLessThan`). Setting it greater than 3 throws
`ArgumentOutOfRangeException` (via `ArgumentOutOfRangeException.ThrowIfGreaterThan`).
`Format` accepts any `OutputFormat` enum value and performs no range check.

### Dependencies

N/A - EmitConfig and OutputFormat are self-contained value types defined in
ApiMarkCore; they have no dependencies on other units, OTS items, or shared
packages.

### Callers

- **Program** — constructs an `EmitConfig` from the parsed `Context` (using
  `Context.Format` and `Context.HeadingDepth`) and passes it to
  `IApiEmitter.Emit`.
- **ApiMarkTask** — passes `--format` and `--depth` CLI arguments to the
  spawned tool process, which constructs `EmitConfig` inside `Program`.
