## Cli

### Verification Approach

The Cli subsystem is verified through unit tests in
`test/ApiMark.Tool.Tests/Cli/ContextTests.cs` that exercise `Context.Create(string[] args)`
directly, confirming all parsed properties, default values, error conditions, and log
file behavior. Tests use the real `Context.Create` factory with no mocking; each test
exercises one specific parsing scenario in isolation.

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

### Test Scenarios

**All global flags parsed together**: Verifies that `--version`, `--help`, `--silent`,
and `--validate` can all be supplied in a single argument array and all corresponding
properties are set. Tested by `Context_Cli_ParsesAllGlobalFlags`.

For detailed unit-level test scenarios covering individual flags, options, default
values, error conditions, and log file behavior, see the Context Unit Verification.
