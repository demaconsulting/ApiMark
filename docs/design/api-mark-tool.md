# ApiMarkTool

![ApiMarkTool Structure](ApiMarkToolView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

## Architecture

ApiMarkTool is organized into two subsystems (`Cli` and `SelfTest`) and one top-level
unit (`Program`). It is the .NET executable (`ApiMark.Tool.dll`) distributed as part of
the NuGet package `ApiMark.MSBuild` (under `tools/net8.0/`, `tools/net9.0/`, and
`tools/net10.0/`) and optionally as a standalone dotnet tool in the NuGet package
`DemaConsulting.ApiMark.Tool`.

- `Cli` subsystem — provides the `Context` unit, which parses command-line arguments into
  typed properties and routes all program output (stdout, stderr, and an optional log file).
- `SelfTest` subsystem — provides the `Validation` unit, which runs in-process
  self-validation tests when `--validate` is specified.
- `Program` unit — the CLI entry point; creates a `Context`, dispatches to the appropriate
  action (version display, help, self-validation, or main tool logic), constructs the
  appropriate `IApiGenerator` implementation for the requested language subcommand, calls
  `Parse()` to obtain an `IApiEmitter`, then calls `IApiEmitter.Emit()` with an `EmitConfig`
  built from the parsed context.

ApiMarkTool targets `net8.0;net9.0;net10.0` (multi-target). It is packaged as a
dotnet tool (`PackAsTool=true`, `ToolCommandName=apimark`). Because it runs as a
standalone process rather than inside the MSBuild host, language generators may
depend on any .NET library regardless of `netstandard2.0` support requirements.

## External Interfaces

**CLI (provided)**: Command-line interface invoked by ApiMarkTask and directly by
users or CI pipelines.

- *Type*: .NET executable (run via `dotnet ApiMark.Tool.dll` or `apimark` when
  installed as a dotnet tool).
- *Role*: Provider — ApiMarkTask and end users invoke the tool directly.
- *Contract*: `apimark [options] [language [language-options]]`. Standard options:
  `-v, --version`, `-?, -h, --help`, `--silent`, `--validate`,
  `--results <file>` (alias: `--result <file>`), `--format <format>` (values:
  `gradual (default)`, `single-file`), `--depth <#>`, `--log <file>`.
  Supported subcommands: `dotnet`, `cpp`, `vhdl`.
  Options for `dotnet`: `--assembly <path>`, `--xml-doc <path>`, `--output <dir>`,
  `--visibility <value>`, `--include-obsolete`, `--exclude <pattern>` (repeatable;
  wildcard namespace/type exclusion patterns), `--enforce-docs <value>` (accepted for
  all three languages, though VHDL enforcement covers only the public interface —
  entities, ports, generics, packages, and exports;
  values: `Public`, `PublicAndProtected`, `All`; enables documentation-coverage
  enforcement at the given visibility tier; disabled by default), `--enforce-docs-severity
  <value>` (values: `Warning` (default), `Error`; controls whether
  violations found by `--enforce-docs` fail the build).
  Options for `cpp`: `--includes <path>` (repeatable), `--api-headers <pattern>`
  (repeatable, ordered, supports `!` exclusion patterns),
  `--library-name <name>`, `--library-description <text>`, `--defines <defs>`,
  `--cpp-standard <std>`, `--clang-path <path>`, `--output <dir>`,
  `--visibility <value>`, `--include-obsolete`.
  Options for `vhdl`: `--source <glob>` (repeatable, ordered, supports `!` exclusion patterns),
  `--output <dir>`, `--library-name <name>`, `--library-description <text>`.
  Standard flags are valid anywhere in the argument list,
  before or after the language subcommand (single-pass parser). `--enforce-docs` and
  `--enforce-docs-severity` are validated per-subcommand and, when the constructed
  generator implements `IDocumentationCoverageCapable`, dispatched polymorphically to
  scan and report undocumented items — this applies uniformly to `dotnet`, `cpp`, and
  `vhdl`; see the ApiMarkTool Program design document for the full dispatch flow.
- *Constraints*: Exits non-zero on error; writes a descriptive message to stderr;
  writes Markdown files to the directory specified by `--output`.

**IApiGenerator / IApiEmitter (consumed)**: Program constructs language-specific `IApiGenerator`
implementations from ApiMarkCore and dispatches to them using the two-stage pipeline.

- *Type*: In-process .NET public API.
- *Role*: Consumer — Program constructs the appropriate `IApiGenerator`
  implementation for the requested language, calls `Parse()` to obtain an
  `IApiEmitter`, then calls `IApiEmitter.Emit()` with an `EmitConfig` built
  from the parsed `Context`.
- *Contract*: `IApiGenerator.Parse(IContext context)` returns `IApiEmitter`;
  `IApiEmitter.Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)`.
- *Constraints*: The generator must be fully configured from CLI options before
  `Parse` is called; `EmitConfig` must be constructed from the parsed context
  before `Emit` is called.

## Dependencies

- **ApiMarkDotNet**: Program constructs `DotNetGenerator` for the `dotnet` language
  subcommand — see ApiMarkDotNet System Design.
- **ApiMarkCpp**: Program constructs `CppGenerator` for the `cpp` subcommand — see
  ApiMarkCpp System Design.
- **ApiMarkVhdl**: Program constructs `VhdlGenerator` for the `vhdl` subcommand — see
  ApiMarkVhdl System Design.
- **ApiMarkCore**: Program references `IApiGenerator` from ApiMarkCore — see
  ApiMarkCore System Design.
- **DemaConsulting.TestResults**: Program uses `TrxSerializer` and `JUnitSerializer`
  from this package when writing `--validate` results to a file —
  see DemaConsulting.TestResults OTS Design.

## Risk Control Measures

N/A - not a safety-classified software item.

## Data Flow

1. The host environment (ApiMarkTask, user, or CI pipeline) invokes
   `dotnet ApiMark.Tool.dll [options] <language> [options]`.
2. Program creates a `Context` object via `Context.Create(args)` (Cli subsystem), which
   parses the command-line arguments using a single-pass parser. Program then dispatches
   to the appropriate priority path (version, help, validate, or main tool logic).
3. Program validates that all required options for the requested language are
   present; exits non-zero with a usage message if any are missing.
4. Program constructs the appropriate `IApiGenerator` implementation based on the
   language subcommand (`DotNetGenerator` for `dotnet`; `CppGenerator` for `cpp`;
   `VhdlGenerator` for `vhdl`).
5. Program creates a `FileMarkdownWriterFactory` for the output directory, builds an
   `EmitConfig` from the parsed context (using `--format` and `--depth`), calls
   `IApiGenerator.Parse(context)` to obtain an `IApiEmitter`, then — strictly between
   `Parse` and `Emit`, while the parsed generator state is still available — checks
   documentation coverage when `--enforce-docs` was supplied and the constructed
   generator implements `IDocumentationCoverageCapable` (true for `dotnet`, `cpp`,
   and `vhdl` alike), and finally calls
   `IApiEmitter.Emit(factory, emitConfig, context)`.
6. When the documentation-coverage check runs, violations are written
   to standard output; if `--enforce-docs-severity Error` was specified and at least
   one violation was found, Program reports a build failure via the same non-zero
   exit path used for other errors.
7. On success, Program exits 0. On error, exceptions are caught, written to stderr,
   and Program exits non-zero.

## Design Constraints

- Platform: targets `net8.0;net9.0;net10.0`; runs on Linux, macOS, and Windows
  via `dotnet` or as an installed dotnet tool.
- Language extensibility: new language subcommands are added by implementing
  `IApiGenerator` and adding a dispatch branch; no changes to Core are required.
- No direct assembly reflection: all assembly reading is delegated to the
  appropriate `IApiGenerator` implementation; Program never calls Mono.Cecil or
  CppAst.Net directly.
- NuGet packaging: bundled inside `ApiMark.MSBuild` under `tools/net8.0/`,
  `tools/net9.0/`, and `tools/net10.0/`; also published separately as
  `DemaConsulting.ApiMark.Tool` for direct CLI use.
