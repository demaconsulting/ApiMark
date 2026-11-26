### HelpCommand

#### Verification Approach

`HelpCommand` is verified with unit tests in `test/ApiMark.Tool.Tests/Cli/HelpCommandTests.cs`.
Each test creates a help command with `HelpCommand.Create`, which writes to in-memory writers
instead of the console. No mocking is applied: the tests use the real help cards embedded in the
tool assembly and the real HelpCards library, a documented dependency of the unit.

#### Test Environment

N/A - standard test environment. `HelpCommandTests.cs` runs within the standard `dotnet test`
host; no files, external services, or network access are required, because the cards are read
from the tool assembly.

#### Acceptance Criteria

- All tests in `HelpCommandTests.cs` pass with zero failures.
- The help verb prints the topic index, the shipped catalog passes validation with warnings
  treated as errors, and every card prints in lines of at most 120 characters.

#### Test Scenarios

**Help verb prints the topic index**: Verifies that `TryRun` with `["help"]` answers with exit
code 0 and that the output lists the command of every root topic. This scenario is tested by
`HelpCommand_TryRun_HelpVerb_PrintsTopicIndexAndReturnsZero`.

**Shipped catalog passes validation**: Verifies that the catalog configuration from
`CreateOptions` passes validation with warnings treated as errors, which proves that the root
cards are exactly `RootCardIds`, that every card carries one purpose tag, and that every command
example parses as a command line of the tool. This scenario is tested by
`HelpCommand_CreateOptions_ShippedCatalog_PassesValidation`.

**Every card fits the maximum line width**: Verifies that every card of the shipped catalog,
printed with its links and tokens expanded and a fixed, long prerelease version in place of the
build's version, is answered with exit code 0 and has no line longer than 120 characters. This
scenario is tested by `HelpCommand_TryRun_EveryCard_FitsMaximumLineWidth`.
