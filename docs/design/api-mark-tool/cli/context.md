### Context

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

#### Purpose

Context is the command-line argument parser and output router for
ApiMarkTool. It exposes parsed flags and options as typed properties and
provides `WriteLine` and `WriteError` methods that route output to stdout,
stderr, and an optional log file. Argument parsing is performed by a private
`ArgumentParser` inner class using a single left-to-right pass over the
argument array.

#### Data Model

**Properties** (all `private init`; set during construction):

| Property | Type | Default | Description |
| :------- | :--- | :------ | :---------- |
| `Version` | `bool` | `false` | `--version` / `-v` flag |
| `Help` | `bool` | `false` | `--help` / `-?` / `-h` flag |
| `Silent` | `bool` | `false` | `--silent` flag |
| `Validate` | `bool` | `false` | `--validate` flag |
| `ResultsFile` | `string?` | `null` | Path from `--results` (alias: `--result`) |
| `HeadingDepth` | `int` | `1` | Value from `--depth` (range 1–6) |
| `Language` | `string?` | `null` | First positional non-flag token |
| `Assembly` | `string?` | `null` | Path from `--assembly` |
| `XmlDoc` | `string?` | `null` | Path from `--xml-doc` |
| `Includes` | `string[]` | `[]` | Plain directory paths accumulated from repeated `--includes` invocations |
| `ApiHeaders` | `string[]` | `[]` | Ordered patterns from repeated `--api-headers` invocations (may start with `!`) |
| `Output` | `string?` | `null` | Directory from `--output` |
| `Visibility` | `string` | `"Public"` | Value from `--visibility` |
| `IncludeObsolete` | `bool` | `false` | `--include-obsolete` flag |
| `LibraryName` | `string?` | `null` | Library name from `--library-name`; optional, used for C++ `api.md` heading |
| `LibraryDescription` | `string?` | `null` | Library description from `--library-description`; optional, omitted when null |
| `Defines` | `string[]` | `[]` | Preprocessor definitions from `--defines` (comma-separated, split into entries of the form `NAME` or `NAME=value`) |
| `CppStandard` | `string?` | `null` | C++ language standard from `--cpp-standard` (e.g. `c++17`); optional, tool defaults to `c++17` when null |
| `ClangPath` | `string?` | `null` | Explicit clang executable path from `--clang-path`; optional, auto-discovered when null |
| `Format` | `OutputFormat` | `GradualDisclosure` | Output format from `--format` (`gradual` → `GradualDisclosure`, `single-file` → `SingleFile`) |

**Private fields**:

- `_logWriter` (`StreamWriter?`) — the optional log file writer; `null` when
  `--log` was not specified.
- `_hasErrors` (`bool`) — set to `true` by any call to `WriteError`; used
  to compute `ExitCode`.

**Derived property**:

- `ExitCode` (`int`) — returns `1` when `_hasErrors` is `true`; `0` otherwise.

#### Key Methods

**Context.Create(string[] args)** — Static factory; the only public
construction path.

- *Parameters*: `string[] args` — raw command-line arguments from the host.
- *Returns*: A new fully populated `Context` instance.
- *Algorithm*: Creates an `ArgumentParser`, calls `ParseArguments`, copies
  all parsed values to a new `Context` via property initializers, and
  optionally opens the log file. Each `--includes` flag appends a single
  directory path to the `Includes` list; each `--api-headers` flag appends
  a single pattern string (which may start with `!`) to the `ApiHeaders`
  list, preserving order for gitignore-style evaluation.
- *Preconditions*: `args` must be non-null.
- *Postconditions*: All properties reflect the parsed argument values;
  log file is open if `--log` was specified.
- *Exceptions*: `ArgumentException` on unknown flag or missing required
  value; `InvalidOperationException` if the log file cannot be opened.

**Context.WriteLine(string message)** — Writes a line to stdout and to the
log file.

- Stdout output is suppressed when `Silent` is `true`.
- Log file write is unconditional when a log file is open.

**Context.WriteError(string message)** — Sets `_hasErrors = true` and writes
to stderr (in red) and to the log file.

- Stderr output is suppressed when `Silent` is `true`.
- `_hasErrors` is set unconditionally; `ExitCode` returns `1` from this
  point forward regardless of `Silent`.

**Context.Dispose()** — Closes and disposes the log file writer.

#### Error Handling

- Unknown flags throw `ArgumentException` with the unsupported argument
  name in the message.
- Flags that require a value (e.g., `--assembly`) throw `ArgumentException`
  when the value token is absent.
- Log file open failures throw `InvalidOperationException` wrapping the
  original exception with a descriptive message including the file path.
- No exception is thrown by `WriteError`; errors are communicated through
  `ExitCode`.

#### Dependencies

N/A — Context depends only on the .NET runtime (`System.IO.StreamWriter`,
`System.Console`). It has no dependencies on other units, OTS items, or
shared packages.

#### Callers

- **Program** — creates a `Context` instance in `Main` via
  `Context.Create(args)` and passes it to `Program.Run`. Disposes the
  context after `Run` returns.
- **Validation** — creates child `Context` instances via
  `Context.Create(childArgs)` to run self-tests in isolation and capture
  their output to a temporary log file.
