## DemaConsulting.HelpCards

### Verification Approach

`DemaConsulting.HelpCards` is an OTS runtime library used only by `HelpCommand` to serve the
built-in `help` command. It has no self-validation mode, so it is verified by tests in
`test/ApiMark.Tool.Tests/` that use the real package and the cards embedded in the tool assembly.
No mocking is applied. The tests need no network access, and the example runner creates a scratch
folder per card in the temporary directory. The tests run on every operating system and .NET
version in the CI matrix. Their TRX results are consumed by ReqStream to satisfy the
DemaConsulting.HelpCards OTS requirements.

### Test Scenarios

**HelpCards answers the help command**: The help verb is answered from the embedded cards,
in-process and by the built tool. This scenario is tested by
`HelpCommand_TryRun_HelpVerb_PrintsTopicIndexAndReturnsZero` and
`ApiMarkTool_HelpTopics_HelpVerb_ListsTopics`.

**HelpCards test helpers report card defects and example results**: The validator accepts the
shipped cards, and the example runner runs every command example against the built tool. This
scenario is tested by `HelpCommand_CreateOptions_ShippedCatalog_PassesValidation` and
`ApiMarkTool_HelpExamples_AllCommandExamples_GiveStatedResults`.
