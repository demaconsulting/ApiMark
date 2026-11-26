### HelpCommand

![HelpCommand Structure](CliView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

#### Purpose

`HelpCommand` answers the `apimark help` command from the help cards that are embedded in the
tool assembly. It is the only unit that references the HelpCards library.

#### Data Model

**`ToolName`**: `string` constant — `apimark`, the name of the tool as the user types it. The
usage lines, the index preamble, and the `{{tool}}` token of the cards are derived from it.

**`RootCardIds`**: `string[]` — the ids of the root cards, which are the topics that
`apimark help` lists: `getting-started`, `command-line`, `dotnet`, `cpp`, `vhdl`, `output`,
`doc-coverage`, `response-files`, `msbuild`, `self-validation`, and `troubleshooting`. It is the
one list of topics; a missing or extra root card fails the catalog.

**`PurposeTags`**: `string[]` (private) — `tutorial`, `reference`, and `troubleshooting`; the
closed tag vocabulary. Every card carries one of these tags and no other tag.

**`Cards`**: `HelpSource` (private, static) — the cards embedded in the tool assembly. Creating
the source reads nothing.

**`Default`**: `HelpCommand` (static) — the instance used by the tool; it writes to the console.

**`_cli`**: `HelpCli` — the command-line helper of HelpCards, created from `CreateOptions`. It is
immutable and keeps no state between calls.

**Help cards**: the Markdown files under `src/ApiMark.Tool/Help/`, embedded by the HelpCards
build targets. The rules for writing them are in the *Help Cards* section of `CONTRIBUTING.md`.

#### Key Methods

**TryRun(string[] args, out int exitCode) → bool**: Runs the help command when the arguments
start with the `help` verb.

- *Parameters*: `string[] args` — the command-line arguments of the tool; `out int exitCode` —
  receives the exit code when the command was answered.
- *Returns*: `bool` — `true` when the command was answered; `false` when the tool should
  continue with its own option parser.
- *Preconditions*: None.
- *Postconditions*: When `false` is returned, nothing was written.

Delegates to `HelpCli.TryRun`, which builds the catalog only after the verb was recognized.

**Footer → string**: Gets the one-line pointer to the help command that ends the usage text,
without building the catalog.

**CreateOptions(TextWriter? output, TextWriter? error) → HelpCliOptions**: Creates the options
of the command-line helper.

- *Parameters*: optional `TextWriter` output and error writers (`null` for the console).
- *Returns*: `HelpCliOptions` — the tool name, the version from `Program.Version`, the catalog
  configuration (the embedded cards, `PurposeTags`, and `RootCardIds`), the usage lines, the
  index preamble, and the writers.
- *Preconditions*: None.
- *Postconditions*: `GetCatalogConfiguration` of the options gives the tests the composition
  that the tool ships.

**Create(TextWriter output, TextWriter error) → HelpCommand**: Creates a help command that
writes to the given writers; used by tests.

#### Error Handling

`TryRun` does not throw for user input; the library writes errors to the error writer and sets
the exit code (see *Help Command* in the ApiMarkTool system design). The validation test
reports every card defect at build time, so a defective catalog is not shipped.

The command is answered before the options are parsed, so no `Context` exists and the output
and error providers of HelpCards are not used: routing the answers through a `Context` would
mean parsing the options a second time just for help.

#### Dependencies

- **DemaConsulting.HelpCards** (OTS) — `HelpCli`, `HelpCliOptions`, `HelpSource`, and
  `HelpCatalogBuilder`; see *DemaConsulting.HelpCards Integration Design*.
- **Program** — `Program.Version` supplies the version shown in the index and the
  `{{version}}` token. `Program` is the top-level unit, not a subsystem, so the Cli subsystem
  still depends on no other subsystem.

#### Callers

- **Program** — `Program.Main` calls `HelpCommand.Default.TryRun` before `Context.Create`, and
  `Program.PrintHelp` adds the `apimark help [topic]` usage line from `HelpCommand.ToolName`
  and ends the usage text with `HelpCommand.Default.Footer`.
