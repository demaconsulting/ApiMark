## Program

![Program Structure](ApiMarkToolView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

Program is the CLI entry point for ApiMarkTool. It parses command-line arguments
via the `Context` class, dispatches to the appropriate priority path (version, help,
self-validation, or main tool logic), constructs the appropriate `IApiGenerator`
implementation for the requested language using a language switch, calls `Parse` to
obtain an `IApiEmitter`, and calls `Emit` with an `EmitConfig` constructed from the
CLI options. It is the sole public CLI surface of ApiMarkTool.

### Data Model

**Program**: Static class — contains the CLI entry point, dispatch logic, generator
construction, and help/banner printing.

**Program.Version** (public static `string` property): Returns the informational
version string read from `AssemblyInformationalVersionAttribute` via reflection, with
fallback to `AssemblyVersion`, then `"0.0.0"`. Used by `PrintBanner`
and `Run` when responding to `--version`.

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
- Banner is printed for all paths that do not return at Priority 1.
- Priority 2: `--help` — prints help text.
- Priority 3: `--validate` — calls `Validation.Run`.
- Priority 4: main tool logic via `RunToolLogic`.

**Program.RunToolLogic** (private static): Validates required options, constructs
the generator, and calls `Parse` then `Emit`.

- _Parameters_: `Context context`.
- Validates `Language` and `Output` for all subcommands; additionally validates `Assembly` and
  `XmlDoc` for dotnet, `Includes` for cpp, and at least one non-exclusion `--source` pattern
  (i.e. a pattern not prefixed with `!`) for vhdl; calls `context.WriteError` and `PrintHelp`
  if any are missing.
- Enforces the single-file format depth constraint: if `Format` is `SingleFile` and
  `HeadingDepth` is greater than 3, calls `context.WriteError` with a diagnostic naming
  `--depth` and exits without constructing the generator. This check is placed here rather
  than in `Context` because it is a cross-argument constraint (requiring knowledge of both
  `--format` and `--depth`) that can only be evaluated after the full argument list is parsed.
  The single-file emitters render member headings at `depth+3`; a depth above 3 would
  produce H7+ headings unsupported by CommonMark.
- Documentation-coverage enforcement dispatches polymorphically across all three
  languages: when `--enforce-docs` is set, `RunToolLogic` calls
  `generator.Parse(context)` first, then (before constructing the
  `IMarkdownWriterFactory` or calling `Emit`) checks whether the constructed
  generator implements `IDocumentationCoverageCapable`. When it does — true for
  `dotnet`, `cpp`, and `vhdl` today — `RunToolLogic` calls
  `coverageCapable.CheckDocumentationCoverage(context.EnforceDocs)` and passes
  the result to `ReportDocumentationCoverage`. When it does not (a defensive
  fallback for a future generator that has not yet adopted the capability),
  `RunToolLogic` writes an informational `context.WriteLine` note instead of
  failing. This ordering is mandatory: `Emit` disposes generator-owned parsed
  state (for example, the DotNet assembly), so the coverage check must run
  strictly between `Parse` and `Emit`.
- Calls `CreateGenerator(context)`, then `generator.Parse(context)` to get an
  `IApiEmitter`, then `emitter.Emit(factory, emitConfig, context)` where
  `emitConfig` is constructed from `context.Format` and `context.HeadingDepth`.
  All exceptions are caught and routed to `context.WriteError`.

**Program.ReportDocumentationCoverage** (private static): Writes the
documentation-coverage scan results to the context output stream, and signals
a build failure via `context.WriteError` when the configured severity is
`Error` and at least one violation was found.

- _Parameters_: `Context context`; `DocumentationCoverageResult result` — the
  scan result from `IDocumentationCoverageCapable.CheckDocumentationCoverage`
  on whichever generator is active for the selected language.
- _Algorithm_: Parses `context.EnforceDocsSeverity` into the private
  `EnforcementSeverity` enum (`Warning`, `Error`), throwing `ArgumentException`
  for an unrecognized value. Writes one line per undocumented item, followed
  by a single summary line reporting the undocumented/checked counts. Calls
  `context.WriteError` exactly once — never per item — only when
  `result.HasViolations` is `true` and severity is `Error`; this keeps the
  console output proportionate to a single logical failure rather than
  flooding the error channel with one entry per undocumented item.
- _Reporting scope_: Console-only for v1 — there is no new results-file
  format; enforcement reuses the existing `Context.WriteError`/`ExitCode`
  mechanism that already gates the process exit code for every other kind of
  tool error.

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
  `ClangPath`). The `LibraryName` is resolved from `context.LibraryName` when
  set; otherwise it falls back to the last segment of `context.Output`, or
  `"Library"` if the output path is also absent.
- For the `vhdl` language, `VhdlGeneratorOptions` is populated with `Sources`
  (from `context.Sources`), `LibraryName` (from `context.LibraryName`, same
  fallback as C++), and `Description` (from `context.LibraryDescription`).
- `--enforce-docs` is validated per-subcommand, before construction, so an
  invalid value fails fast for all three languages uniformly: for `dotnet` and
  `cpp`, `context.EnforceDocs` is parsed as the corresponding language's
  `ApiVisibility` enum (case-insensitive), throwing `ArgumentException` with
  the invalid value included in the message when parsing fails, and the parsed
  tier is assigned to `DotNetGeneratorOptions.EnforceDocsVisibility` or
  `CppGeneratorOptions.EnforceDocsVisibility` respectively; for `vhdl`,
  `context.EnforceDocs` is validated against the same three-word vocabulary
  (reusing the dotnet `ApiVisibility` enum purely for consistent validation
  and error messages) but the parsed enum value is discarded — only the raw
  string is forwarded to `VhdlGeneratorOptions.EnforceDocsVisibility`, since
  VHDL has no visibility concept and treats all three tiers identically (see
  `ApiMark.Vhdl.DocumentationCoverageChecker`). When `context.EnforceDocs` is
  absent, each language's `EnforceDocsVisibility` option remains `null`/empty
  and enforcement stays disabled — the same conservative-default reasoning
  applied to every other opt-in CLI flag.

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
An invalid `--enforce-docs` value throws `ArgumentException` from
`CreateGenerator`; an invalid `--enforce-docs-severity` value throws
`ArgumentException` from `ReportDocumentationCoverage` — both are caught by
the same `try`/`catch` in `RunToolLogic` and routed to `context.WriteError`.
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
- **IApiEmitter** — Program calls `IApiEmitter.Emit` on the value returned by
  `IApiGenerator.Parse` — see IApiEmitter Unit Design.
- **EmitConfig** — Program constructs an `EmitConfig` from `Context.Format` and
  `Context.HeadingDepth` before calling `IApiEmitter.Emit` — see EmitConfig Unit Design.
- **IDocumentationCoverageCapable** — Program tests each constructed generator
  for this interface via `generator is IDocumentationCoverageCapable` and, when
  implemented, calls `CheckDocumentationCoverage(context.EnforceDocs)` between
  `Parse` and `Emit` when `--enforce-docs` was specified — see
  IDocumentationCoverageCapable Unit Design.
- **DotNetGenerator** — Program constructs `DotNetGenerator` for the `dotnet`
  language subcommand — see DotNetGenerator Unit Design.
- **CppGenerator** (ApiMarkCpp) — instantiated for the `cpp` subcommand — see
  ApiMarkCpp Component Design.
- **VhdlGenerator** (ApiMarkVhdl) — instantiated for the `vhdl` subcommand — see
  ApiMarkVhdl Component Design.

### Callers

N/A - entry point, called by the host environment (ApiMarkTask process spawn,
dotnet tool invocation, or CI pipeline script).
