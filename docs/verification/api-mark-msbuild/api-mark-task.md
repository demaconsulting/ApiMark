## ApiMarkTask

### Verification Approach

`ApiMarkTask` is the MSBuild task that spawns the `ApiMark.Tool` child process with
language-appropriate arguments. Verification uses unit and integration tests in
`test/ApiMark.MSBuild.Tests/` that directly instantiate `ApiMarkTask` and set properties on
the task object, asserting that the correct child-process arguments are constructed and that
skip conditions are honored. The real process-spawn path is exercised in the integration test
so the unit is verified as it is shipped.

### Test Environment

Tests require the .NET SDK and a pre-built `ApiMark.Tool.dll`. The integration test requires a
writable output directory for generated Markdown. No fixture MSBuild project files, external
service, network dependency, or elevated permission is required.

### Acceptance Criteria

- All `ApiMarkTask` integration tests pass with zero failures.
- The task resolves the language from `ApiMarkLanguage` or infers it from the project
  extension (`.vcxproj` → `cpp`, otherwise → `dotnet`).
- For the `dotnet` language, the spawned command includes `--assembly` and `--xml-doc`
  arguments from `ApiMarkAssemblyPath` and `ApiMarkXmlDocPath`.
- For the `cpp` language, the spawned command includes `--includes` arguments from
  `ApiMarkIncludePaths`.
- `ApiMarkOutputDir` is forwarded as `--output` in all cases.
- `ApiMarkVisibility` is forwarded as `--visibility` when set.
- `ApiMarkIncludeObsolete` is forwarded as `--include-obsolete` when true.
- `DisableApiMark` suppresses tool invocation and returns true with no side effects.
- A non-zero exit code from the spawned tool causes `Execute` to return false and log a
  MSBuild error.

### Test Scenarios

**Language inferred as cpp for vcxproj project**: Verifies that when no `ApiMarkLanguage`
is set and the project is a `.vcxproj`, the task spawns the tool with the `cpp` subcommand.
This scenario is tested by `ApiMarkTask_Language_InferredAsCpp_ForVcxproj`.

**Language inferred as dotnet for csproj project**: Verifies that when no `ApiMarkLanguage`
is set and the project is a `.csproj`, the task spawns the tool with the `dotnet` subcommand.
This scenario is tested by `ApiMarkTask_Language_InferredAsDotNet_ForCsproj`.

**DotNet tool invocation passes assembly and xml-doc paths**: Verifies that the assembly
path from `ApiMarkAssemblyPath` and the XML doc path from `ApiMarkXmlDocPath` appear as
`--assembly` and `--xml-doc` arguments in the spawned command. This scenario is tested by
`ApiMarkTask_DotNet_SpawnsToolWithCorrectAssemblyAndXmlDocArguments`.

**Cpp tool invocation passes include paths**: Verifies that the semicolon-separated value
of `ApiMarkIncludePaths` is forwarded as-is to the `--includes` argument of the spawned
`cpp` subcommand. This scenario is tested by
`ApiMarkTask_Cpp_SpawnsToolWithCorrectIncludePathArguments`.

**Output directory is forwarded as --output**: Verifies that the `ApiMarkOutputDir` value
is passed to the tool as the `--output` argument without modification. This scenario is
tested by `ApiMarkTask_OutputDir_ForwardedToToolAsOutputArgument`.

**Visibility is forwarded as --visibility**: Verifies that the `ApiMarkVisibility` value
is passed to the tool as the `--visibility` argument. This scenario is tested by
`ApiMarkTask_Visibility_ForwardedToToolAsVisibilityArgument`.

**DisableApiMark skips tool invocation**: Verifies that when `DisableApiMark` is true, the
task returns true immediately without spawning any process or producing any output. This
scenario is tested by `ApiMarkTask_DisableApiMark_True_SkipsToolInvocation`.
