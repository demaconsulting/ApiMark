## Cli

### Verification Approach

The Cli subsystem is verified through unit tests in
`test/ApiMark.Tool.Tests/Cli/ContextTests.cs` that exercise `Context.Create(string[] args)`
directly, confirming all parsed properties, default values, error conditions, and log
file behavior. Tests use the real `Context.Create` factory with no mocking; each test
exercises one specific parsing scenario in isolation.

The `HelpCommand` unit is verified by `test/ApiMark.Tool.Tests/Cli/HelpCommandTests.cs` (see
HelpCommand Unit Verification). At the subsystem level, `test/ApiMark.Tool.Tests/Cli/CliSubsystemTests.cs`
asks the help command for a topic through its public `TryRun` entry point, with the real help
cards embedded in the tool assembly and in-memory writers in place of the console.

### Test Environment

Standard .NET test runner. No external files are required except for the log-file test
(`Context_Create_WithLogFile_OpensAndWritesToLog`), which writes a temporary file to
`Path.GetTempPath()` and deletes it after the assertion.

### Acceptance Criteria

- All Context unit tests pass with zero failures; see Context Unit Verification for the
  full set of individual criteria.
- The subsystem-level integration test `Context_Cli_ParsesAllGlobalFlags` passes,
  confirming that all global flags (`--version`, `--help`, `--silent`, `--validate`)
  can be supplied simultaneously and all corresponding properties are set correctly.
- When `--silent` is specified, no output is written to stdout or stderr; `ExitCode`
  still reflects error state when `WriteError` is called.
- All tool output is routed through `Context.WriteLine` or `Context.WriteError`; both
  methods write unconditionally to any open log file regardless of the `--silent` flag.
- `--reference-paths` (repeatable) is parsed and accumulated into `Context.ReferencePaths`, consistent with the other
  repeatable dotnet-only options (`--exclude`, `--includes`, `--api-headers`, `--source`).
- The help command answers a requested topic from the built-in help cards with exit code 0.

### Test Scenarios

**All global flags parsed together**: Verifies that `--version`, `--help`, `--silent`,
and `--validate` can all be supplied in a single argument array and all corresponding
properties are set. Tested by `Context_Cli_ParsesAllGlobalFlags`.

**Silent flag suppresses console output**: Verifies that `--silent` sets the `Silent`
property to `true`. Tested by `Context_Create_WithSilentFlag_SetsSilentTrue`.

**Output routed through Context to log file**: Verifies that both `WriteLine` and
`WriteError` output is captured in the log file when `--log` is supplied.
Tested by `Context_Create_WithLogFile_OpensAndWritesToLog` and
`Context_OpenLogFile_ErrorOutputAlsoWrittenToLog`.

**Silent with log captures output**: Verifies that `--silent --log` combination allows
the tool to run silently while still capturing all output. Tested by
`Program_Main_WithSilentAndLog_DotNetCommand_ExitsZero`.

**Help command prints a requested topic**: Verifies that `help command-line` is answered from
the built-in help cards with exit code 0 and that the output holds the card's title. Tested by
`CliSubsystem_HelpCommand_TopicRequested_PrintsTopic`.

For detailed unit-level test scenarios covering individual flags, options, default
values, error conditions, and log file behavior, see the Context Unit Verification.
