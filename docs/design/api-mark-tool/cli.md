## Cli

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
  `Assembly`, `XmlDoc`, `Includes`, `Output`, `Visibility`, `IncludeObsolete`,
  `ResultsFile`, `HeadingDepth`, `ExitCode`) and provides `WriteLine` and
  `WriteError` for all program output routing.

**Consumed**:

N/A — The Cli subsystem has no dependencies beyond the .NET runtime.

### Design

The Cli subsystem contains one unit: `Context` (see Context Unit Design).

`Context` implements parsing through a private `ArgumentParser` inner class
that performs a single left-to-right pass over the argument array. Standard
flags (`-v`, `--version`, `--help`, `--silent`, `--validate`, `--log`,
`--results`, `--depth`) are recognized anywhere in the argument list. The
first positional non-flag token (a token that does not start with `-`) is
captured as the language subcommand. Language-specific options (`--assembly`,
`--xml-doc`, `--includes`, `--output`, `--visibility`, `--include-obsolete`)
may appear anywhere in the argument list after the language token is
recognized.

`Context` implements `IDisposable` to release the optional log file writer
when the caller is done with the context.
