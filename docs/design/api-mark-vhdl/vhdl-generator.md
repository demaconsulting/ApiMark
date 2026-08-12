## VhdlGenerator

![VhdlGenerator Structure](ApiMarkVhdlView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

VhdlGenerator is the public entry point for VHDL API documentation generation.
It implements `IApiGenerator`, accepts `VhdlGeneratorOptions`, evaluates all
configured glob patterns to enumerate VHDL source files, delegates parsing to
`VhdlAstParser`, and returns a `VhdlEmitter` ready to produce Markdown output.

### Data Model

**VhdlGeneratorOptions** (public): Configuration record supplied by the caller.

- `LibraryName`: `string` — the name of the VHDL library to document; must be non-empty.
- `Description`: `string` — introductory paragraph text emitted at the top of the api index
  page (gradual disclosure) or below the top-level heading (single-file). An empty string
  produces no description paragraph.
- `Sources`: `IList<string>` — glob patterns that identify source files to include.
  Patterns prefixed with `!` are exclusion patterns. Evaluated with gitignore-style
  last-match-wins semantics by `GlobFileCollector` from ApiMarkCore. An empty list
  produces no matched files.
- `WorkingDirectory`: `string?` — the base directory for glob evaluation. When `null`,
  defaults to `Directory.GetCurrentDirectory()`.
- `EnforceDocsVisibility`: `string?` — optional documentation-coverage
  enforcement tier string (`"Public"`/`"PublicAndProtected"`/`"All"`).
  Defaults to `null` (enforcement disabled). Unlike the .NET and C++ options
  types, this is stored as a raw string rather than an `ApiVisibility` enum,
  since VHDL has no corresponding visibility enum to parse into — see the
  VhdlGenerator DocumentationCoverageChecker design document.

### Key Methods

**VhdlGenerator constructor**: Validates configuration at construction time.

- *Parameters*: `VhdlGeneratorOptions options` — must not be null and `LibraryName`
  must be non-empty.
- *Returns*: a configured `VhdlGenerator` instance.
- *Preconditions*: `options` is not null; `options.LibraryName` is not null or whitespace.
- *Postconditions*: the instance is ready to call `Parse`.
- *Algorithm*: throws `ArgumentNullException` when `options` is null; throws
  `ArgumentException` when `options.LibraryName` is null or whitespace; normalizes
  a null `options.Sources` to an empty list.

**VhdlGenerator.Parse** (implements `IApiGenerator`): Enumerates source files and
returns a ready-to-emit `VhdlEmitter`.

- *Parameters*: `IContext context` — logging channel; must not be null.
- *Returns*: `IApiEmitter` — a `VhdlEmitter` holding all parsed file models.
- *Algorithm*:
  1. Resolve the working directory: use `options.WorkingDirectory` when non-null,
     otherwise `Directory.GetCurrentDirectory()`.
  2. Call `GlobFileCollector.Collect(_options.Sources, vhdlExtensions, cwd)` to build
     the sorted, deduplicated list of matched `.vhd` and `.vhdl` files.
  3. When no files are matched, emit `"Error: no .vhd or .vhdl files matched the
     --source patterns."` via `context.WriteError` and return an empty `VhdlEmitter`.
  4. Call `VhdlAstParser.Parse(filePath)` for each matched file path, emitting
     `context.WriteLine($"Parsing {file}")` before each parse call.
  5. Cache the parsed file models on the generator instance so
     `CheckDocumentationCoverage` can be called afterward without re-parsing.
  6. Construct and return `new VhdlEmitter(options, fileModels)`.

**VhdlGenerator.CheckDocumentationCoverage** (`IDocumentationCoverageCapable`):
scans the file models cached by the most recent `Parse` call for entities,
ports, generics, packages, and package-level exports lacking a documentation
summary. See the VhdlGenerator DocumentationCoverageChecker design document
for the scan algorithm and the public-interface-only scope decision.

- *Parameters*: `string? enforceTier` — the enforcement tier string
  (`"Public"`/`"PublicAndProtected"`/`"All"`, case-insensitive), or
  `null`/empty to fall back to `VhdlGeneratorOptions.EnforceDocsVisibility`.
- *Returns*: a `DocumentationCoverageResult` describing every undocumented
  declaration found.
- *Preconditions*: must be called after `Parse` has completed.
- *Postconditions*: the parsed tier value itself is discarded beyond
  validation — VHDL has no visibility concept, so all three recognized
  values enable the identical public-interface-only check.

### Error Handling

- `ArgumentNullException` — thrown by the constructor when `options` is null, and by
  `Parse` when `context` is null.
- `ArgumentException` — thrown by the constructor when `LibraryName` is null or
  whitespace; thrown by `CheckDocumentationCoverage` when the enforcement tier is
  set but not one of `Public`, `PublicAndProtected`, or `All` (case-insensitive).
- File-level parse errors are caught per file: a warning is emitted via
  `context.WriteError` and the file is skipped, so a single malformed file does not
  abort the entire parse run.
- `InvalidOperationException` — thrown by `CheckDocumentationCoverage` when called
  before `Parse`, or when no enforcement tier is configured via either the
  parameter or `VhdlGeneratorOptions.EnforceDocsVisibility`.

### Dependencies

- **VhdlAstParser** (internal) — called once per matched source file.
- **VhdlEmitter** (internal) — constructed and returned from `Parse`.
- **IApiGenerator** (ApiMarkCore) — the interface this class implements.
- **IDocumentationCoverageCapable** (ApiMarkCore) — the capability interface this
  class implements.
- **DocumentationCoverageChecker** (internal) — performs the actual scan on
  behalf of `CheckDocumentationCoverage`.
- **GlobFileCollector** (ApiMarkCore) — used to evaluate `Sources` glob patterns
  and return sorted, deduplicated file paths.

### Callers

- **ApiMark host / CLI** — constructs `VhdlGenerator` with a `VhdlGeneratorOptions`
  instance, calls `Parse` to obtain an `IApiEmitter`, and — when `--enforce-docs`
  is configured — calls `CheckDocumentationCoverage` via the
  `IDocumentationCoverageCapable` interface. VHDL enforcement is CLI-only today:
  `ApiMarkTask` (MSBuild) has no VHDL language support at all, so
  `CheckDocumentationCoverage` is reachable only through the `ApiMark.Tool` CLI.
