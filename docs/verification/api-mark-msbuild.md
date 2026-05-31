# ApiMarkMsbuild

## Verification Approach

ApiMark.MSBuild is verified through unit and integration tests in `test/ApiMark.MSBuild.Tests/`
that directly instantiate `ApiMarkTask`, set properties on the task object, and assert on
language resolution, argument construction, and execution behavior. The real out-of-process tool
spawn path is exercised in the end-to-end integration test so the system is verified as shipped.
Verification evidence focuses on the correct translation of MSBuild properties into tool
arguments and on the correct handling of skip conditions and successful generation.

## Test Environment

Tests require the .NET SDK and `ApiMark.Tool.dll` built and available. The end-to-end
integration test requires a writable output directory for generated Markdown. The NuGet
package integration tests additionally require the pre-built `DemaConsulting.ApiMark.MSBuild`
`.nupkg`; those tests skip gracefully when the package is absent so local developers who
have not run `dotnet pack` are not blocked. No external service dependency or elevated
permission is required.

## Acceptance Criteria

- All ApiMark.MSBuild integration tests pass with zero failures.
- The task spawns `ApiMark.Tool` with the correct language subcommand and arguments.
- `DisableApiMark` prevents tool invocation and returns success with no side effects.
- Language inference from project extension selects `cpp` for `.vcxproj` and `dotnet` for
  `.csproj` when `ApiMarkLanguage` is not explicitly set.
- `ApiMarkOutputDir` and `ApiMarkVisibility` are forwarded correctly to the tool.
- `ApiMarkIncludeObsolete` set to `true` adds the `--include-obsolete` flag to the spawned tool
  command.
- A non-zero exit code from the spawned tool is surfaced as an MSBuild build failure.
- When `ApiMarkPackDocs` is `true`, the generated `api/` folder is included in the
  NuGet package; when `false` or unset, the `api/` folder is not packaged.

## Test Scenarios

**DotNet project generates documentation via spawned tool**: Verifies that a .NET project
referencing the `DemaConsulting.ApiMark.MSBuild` NuGet package generates documentation
automatically when `dotnet build` is invoked, confirming the full end-to-end integration
from NuGet package import through task execution to Markdown output. This scenario is tested
by `ApiMarkMsbuild_NuGetPackage_DotNetProject_AutoDocumentsOnBuild`.

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

**NuGet package includes generated docs when ApiMarkPackDocs is true**: Verifies that
when `ApiMarkPackDocs=true`, `dotnet pack` bundles the generated `api/` folder into the
`.nupkg` as `api/api.md`. This scenario is tested by
`ApiMarkMsbuild_NuGetPackage_DotNetProject_PacksDocs_WhenApiMarkPackDocsTrue`.

**NuGet package excludes generated docs by default**: Verifies that when `ApiMarkPackDocs`
is not set or is `false`, `dotnet pack` does not include any `api/` content in the `.nupkg`,
confirming the opt-in default. This scenario is tested by
`ApiMarkMsbuild_NuGetPackage_DotNetProject_DoesNotPackDocs_ByDefault`.
