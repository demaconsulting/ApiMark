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
  last-match-wins semantics using `Microsoft.Extensions.FileSystemGlobbing`. An empty
  list produces no documented files.
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
  `ArgumentException` when `options.LibraryName` is null or whitespace.

**VhdlGenerator.Parse** (implements `IApiGenerator`): Enumerates source files and
returns a ready-to-emit `VhdlEmitter`.

- *Parameters*: `IContext context` — logging channel; must not be null.
- *Returns*: `IApiEmitter` — a `VhdlEmitter` holding all parsed file models.
- *Algorithm*:
  1. Resolve the working directory: use `options.WorkingDirectory` when non-null,
     otherwise `Directory.GetCurrentDirectory()`.
  2. Evaluate `options.Sources` glob patterns via `Microsoft.Extensions.FileSystemGlobbing`
     `Matcher`, using last-match-wins semantics: iterate patterns in order; patterns
     without a `!` prefix are added as include patterns; patterns with a `!` prefix
     (stripped of `!`) are added as exclude patterns.
  3. When no non-exclusion patterns are present, return a `VhdlEmitter` with an empty
     file list immediately.
  4. Execute the matcher against the resolved working directory to obtain matched paths.
  5. Call `VhdlAstParser.Parse(filePath)` for each matched file path.
  6. Construct and return `new VhdlEmitter(options, fileModels)`.

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
- **Microsoft.Extensions.FileSystemGlobbing** (NuGet OTS) — used to evaluate
  `Sources` glob patterns against the resolved working directory.

### Callers

- **ApiMark host / CLI** — constructs `VhdlGenerator` with a `VhdlGeneratorOptions`
  instance and calls `Parse` to obtain an `IApiEmitter`.
