## Cli

![Cli Structure](CliView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Overview

The Cli subsystem is responsible for all command-line argument parsing in
ApiMarkTool. It provides the `Context` class — a single unit that parses
command-line arguments into typed properties, routes all program output
(stdout, stderr, and an optional log file), and tracks whether any errors
have been reported. By isolating argument parsing in a dedicated subsystem,
Program.cs is kept free of parsing logic and the argument-parsing contract
can be tested independently.

The subsystem also answers the built-in `help` command through the `HelpCommand`
unit, which serves the help cards embedded in the tool assembly. It contains two
units:

- **`Context`** (`Cli/Context.cs`) — argument parsing, output routing, and error tracking
- **`HelpCommand`** (`Cli/HelpCommand.cs`) — the built-in `help` command, answered from
  embedded help cards

### Interfaces

**Provided**:

- `Context.Create(string[] args)` — static factory method and sole public
  entry point to the Cli subsystem. Callers supply the raw argument array
  from the host environment and receive a fully populated `Context` instance
  ready for dispatch. Throws `ArgumentException` on unknown flags or missing
  required values; throws `InvalidOperationException` if the requested log
  file cannot be opened.
- `Context` (IDisposable) — exposes parsed flags and options as typed
  properties (`Version`, `Help`, `Silent`, `Validate`, `Language`,
  `Assembly`, `XmlDoc`, `Includes`, `ApiHeaders`, `Sources`, `Output`, `Visibility`,
  `IncludeObsolete`, `ResultsFile`, `HeadingDepth`, `Format`, `LibraryName`,
  `LibraryDescription`, `Defines`, `CppStandard`, `ClangPath`, `ClangTimeoutMilliseconds`,
  `Excludes`, `ReferencePaths`, `EnforceDocs`, `EnforceDocsSeverity`, `ExitCode`)
  and provides `WriteLine` and `WriteError` for all program output routing.
- `HelpCommand.Default.TryRun(string[] args, out int exitCode)` — answers the `help`
  command. Returns `false`, writing nothing, when the first argument is not `help`.
  Otherwise writes the answer to the console, returns `true`, and sets the exit code
  (see *Help Command* in the ApiMarkTool system design). Does not throw for user input
  and does not use `Context`.
- `HelpCommand.Default.Footer` — the one-line pointer to the help command that ends the
  usage text. Reading it never builds the help catalog.

**Consumed**:

- **IContext (ApiMark.Core)** — `Context` implements `IContext` so it can be
  passed directly to `IApiGenerator.Parse`/`IApiEmitter.Emit` without an
  adapter; see `IContext` Unit Design.
- **DemaConsulting.HelpCards (OTS)** — `HelpCommand` serves the help cards through the
  command-line helper of this package; see *DemaConsulting.HelpCards Integration Design*.

### Design

The Cli subsystem contains two units: `Context` (see Context Unit Design) and
`HelpCommand` (see HelpCommand Unit Design).

`Context` and `HelpCommand` do not call each other. `Program.Main` offers the arguments
to `HelpCommand.TryRun` first; when the help command answers, the invocation ends with
its exit code and no `Context` is created. `HelpCommand` writes its answers to the
console itself. It is the one exception to `Context` owning all output, because the
`help` command is answered before a `Context` exists.

For every other invocation, `Context` does the parsing described below.

`Context` implements parsing through a private `ArgumentParser` inner class
that performs a single left-to-right pass over the argument array. Standard
flags (`-v`, `--version`, `-?`/`-h`/`--help`, `--silent`, `--validate`, `--log`,
`--results`/`--result`, `--depth`, `--format`) are recognized anywhere in the argument list. The
first positional non-flag token (a token that does not start with `-`) is
captured as the language subcommand. Language-specific options (`--assembly`,
`--xml-doc`, `--includes`, `--api-headers`, `--source`, `--output`, `--visibility`,
`--include-obsolete`, `--library-name`, `--library-description`, `--defines`,
`--cpp-standard`, `--clang-path`, `--clang-timeout-ms`, `--exclude`, `--reference-paths`, `--enforce-docs`,
`--enforce-docs-severity`) may appear anywhere in the argument list after the
language token is recognized.

`Context` implements `IDisposable` to release the optional log file writer
when the caller is done with the context.
