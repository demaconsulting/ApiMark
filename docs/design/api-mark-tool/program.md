## Program

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

Program is the CLI entry point for ApiMarkTool. It parses command-line arguments
via the `Context` class, dispatches to the appropriate priority path (version, help,
self-validation, or main tool logic), constructs the appropriate `IApiGenerator`
implementation for the requested language using a language switch, and calls
`Generate`. It is the sole public CLI surface of ApiMarkTool.

### Data Model

**Program**: Static class — contains the CLI entry point, dispatch logic, generator
construction, and help/banner printing.

**Program.Version** (public static `string` property): Returns the informational
version string read from `AssemblyInformationalVersionAttribute` via reflection, with
fallback to `AssemblyVersion`, then `"0.0.0"`. Used by `PrintBanner` and `Run` when
responding to `--version`.

**Context** (`Cli/Context.cs`): See _Cli Subsystem Design_ (`cli.md`) and
_Context Unit Design_ (`cli/context.md`) for the full data model and interface.

**Validation** (`SelfTest/Validation.cs`): See _SelfTest Subsystem Design_
(`self-test.md`) and _Validation Unit Design_ (`self-test/validation.md`) for
the full design.

### Key Methods

**Program.Main**: CLI entry point.

- _Parameters_: `string[] args` — command-line arguments from the host environment.
- _Returns_: `int` — exit code; 0 on success, non-zero on error.
- _Preconditions_: None — `Main` handles all argument parsing errors gracefully.
- _Postconditions_: On success, the output directory contains a complete Markdown
  tree for the requested component. On error, a descriptive message is written to
  stderr and the process exits non-zero.

Execution steps: create `Context` from args; call `Run`; return `context.ExitCode`.
`ArgumentException` and `InvalidOperationException` from `Context.Create` are
caught, written to `Console.Error`, and exit 1 is returned. All other exceptions
are written to `Console.Error` and re-thrown.

**Program.Run** (public static): Priority-ordered dispatch method.

- _Parameters_: `Context context` — fully initialized context.
- Priority 1: `--version` — writes version string, returns immediately (no banner).
- Priority 2: banner printed for all subsequent paths.
- Priority 3: `--help` — prints help text.
- Priority 4: `--validate` — calls `Validation.Run`.
- Priority 5: main tool logic via `RunToolLogic`.

**Program.RunToolLogic** (private static): Validates required options, constructs
the generator, and calls `Generate`.

- _Parameters_: `Context context`.
- Validates `Language`, `Output`, and (for `dotnet`) `Assembly`; calls
  `context.WriteError` and `PrintHelp` if any are missing.
- Calls `CreateGenerator(context)` and `generator.Generate(factory, context)` inside a
  broad try-catch; all exceptions are routed to `context.WriteError`.

**Program.CreateGenerator** (private static): Constructs and returns an
`IApiGenerator` configured from the parsed context.

- _Parameters_: `Context context` — fully parsed CLI context.
- _Returns_: `IApiGenerator` — language-specific generator instance.
- _Preconditions_: `context.Language` must be a recognized, implemented subcommand.
- Throws `ArgumentException` for invalid `Visibility` values; throws
  `NotSupportedException` for unrecognized or not-yet-implemented language
  identifiers.
- For the `cpp` language, `CppGeneratorOptions` is populated with
  `PublicIncludeRoots` (from `context.Includes`), `ApiHeaderPatterns` (from
  `context.ApiHeaders`), and the other cpp-specific options (`LibraryName`,
  `Description`, `Defines`, `CppStandard`, `Visibility`, `IncludeDeprecated`,
  `ClangPath`).

**Program.PrintBanner** (private static): Prints the application banner (tool name,
version, copyright line, and a blank line).

**Program.PrintHelp** (private static): Prints usage, options, languages, and
language-specific options to the context output stream.

### Error Handling

Argument parsing errors in `Context.Create` (unrecognized flags, missing values)
throw `ArgumentException`, which is caught in `Main`, written to `Console.Error`,
and returns exit code 1. Log file open failures throw `InvalidOperationException`,
handled the same way. Generator construction and execution errors inside
`RunToolLogic` are caught locally and routed to `context.WriteError` so that
`context.ExitCode` becomes 1 without an unhandled-exception stack trace.
Unexpected exceptions propagated out of `Main` are written to `Console.Error` and
re-thrown.

### Dependencies

- **Context** (Cli subsystem) — owns argument parsing and output routing; created by
  Program — see _Cli Subsystem Design_ (`cli.md`) and _Context Unit Design_
  (`cli/context.md`).
- **Validation** (SelfTest subsystem) — runs self-validation tests when `--validate`
  is specified — see _SelfTest Subsystem Design_ (`self-test.md`) and _Validation
  Unit Design_ (`self-test/validation.md`).
- **IApiGenerator** — Program references `IApiGenerator` from ApiMarkCore as the
  common interface for all language generators — see IApiGenerator Unit Design.
- **DotNetGenerator** — Program constructs `DotNetGenerator` for the `dotnet`
  language subcommand — see DotNetGenerator Unit Design.

### Callers

N/A — entry point, called by the host environment (ApiMarkTask process spawn,
dotnet tool invocation, or CI pipeline script).
