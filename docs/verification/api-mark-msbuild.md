# ApiMarkMsbuild

## Verification Approach

ApiMark.MSBuild is verified through integration tests in `test/ApiMark.MSBuild.Tests/` that
build fixture project files and observe task behavior when MSBuild properties are applied. The
real `ApiMarkTask`, real MSBuild property evaluation, and the real out-of-process tool spawn
path are kept in place so the unit is verified as shipped. Verification evidence focuses on the
correct translation of MSBuild properties into tool arguments and on the correct handling of
skip conditions, successful generation, and error propagation from the child process.

## Test Environment

Tests require the .NET SDK, `ApiMark.Tool.dll` built and available, fixture project files
configured with appropriate ApiMark properties, and writable output locations for generated
Markdown. No external service dependency is required.

## Acceptance Criteria

- All ApiMark.MSBuild integration tests pass with zero failures.
- The task spawns `ApiMark.Tool` with the correct language subcommand and arguments.
- `DisableApiMark` prevents tool invocation and returns success with no side effects.
- Language inference from project extension selects `cpp` for `.vcxproj` and `dotnet` for
  `.csproj` when `ApiMarkLanguage` is not explicitly set.
- `ApiMarkOutputDir` and `ApiMarkVisibility` are forwarded correctly to the tool.
- A non-zero exit code from the spawned tool is surfaced as a MSBuild build failure.

## Test Scenarios

**DotNet project generates documentation via spawned tool**: Verifies that building a fixture
`.csproj` file with ApiMark enabled results in the tool being spawned with the correct
`dotnet` subcommand, assembly path, XML documentation path, and output directory, producing
the expected Markdown output. This scenario is tested by
`ApiMarkMsbuild_Build_WithDotNetProject_GeneratesDocumentation`.

**Language is inferred from project extension**: Verifies that the task correctly infers
`cpp` when built in the context of a `.vcxproj` project and `dotnet` otherwise, without
requiring explicit `ApiMarkLanguage` configuration. This scenario is tested by
`ApiMarkTask_Language_InferredAsCpp_ForVcxproj` and
`ApiMarkTask_Language_InferredAsDotNet_ForCsproj`.

**DisableApiMark prevents tool invocation**: Verifies that when `DisableApiMark` is true,
the task returns success immediately without launching any child process, confirming that
projects can opt out of generation without side effects. This scenario is tested by
`ApiMarkTask_DisableApiMark_True_SkipsToolInvocation`.

**Output directory and visibility properties are forwarded**: Verifies that the values of
`ApiMarkOutputDir` and `ApiMarkVisibility` set in the project are passed to the spawned tool
as `--output` and `--visibility` arguments respectively. These scenarios are tested by
`ApiMarkTask_OutputDir_ForwardedToToolAsOutputArgument` and
`ApiMarkTask_Visibility_ForwardedToToolAsVisibilityArgument`.
