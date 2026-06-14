## VhdlGenerator

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
- `Sources`: `IList<string>` — glob patterns that identify source files to include.
  Patterns prefixed with `!` are exclusion patterns. Evaluated with gitignore-style
  last-match-wins semantics by `GlobFileCollector` from ApiMarkCore. An empty list
  produces no matched files.
- `WorkingDirectory`: `string?` — the base directory for glob evaluation. When `null`,
  defaults to `Directory.GetCurrentDirectory()`.
- Additional display and format options are forwarded to `VhdlEmitter` unchanged.

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
  5. Construct and return `new VhdlEmitter(options, fileModels)`.

### Error Handling

- `ArgumentNullException` — thrown by the constructor when `options` is null, and by
  `Parse` when `context` is null.
- `ArgumentException` — thrown by the constructor when `LibraryName` is null or
  whitespace.
- File-level parse errors are caught per file: a warning is emitted via
  `context.WriteError` and the file is skipped, so a single malformed file does not
  abort the entire parse run.

### Dependencies

- **VhdlAstParser** (internal) — called once per matched source file.
- **VhdlEmitter** (internal) — constructed and returned from `Parse`.
- **IApiGenerator** (ApiMarkCore) — the interface this class implements.
- **GlobFileCollector** (ApiMarkCore) — used to evaluate `Sources` glob patterns
  and return sorted, deduplicated file paths.

### Callers

- **ApiMark host / CLI** — constructs `VhdlGenerator` with a `VhdlGeneratorOptions`
  instance and calls `Parse` to obtain an `IApiEmitter`.
