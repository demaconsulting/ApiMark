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
- A non-zero exit code from the spawned tool causes Execute to return false and log an MSBuild error.
- When `ApiMarkPackDocs` is `true`, the generated `api/` folder is included in the
  NuGet package; when `false` or unset, the `api/` folder is not packaged.
- For C++ projects, `ApiMarkLibraryName` is forwarded as `--library-name` when set.
- For C++ projects, `ApiMarkDefines` semicolons are converted to commas when forwarding as
  `--defines`.
- For C++ projects, `ApiMarkCppStandard` is forwarded as `--cpp-standard` when set.
- For C++ projects, each entry in `ApiMarkApiHeaders` is forwarded as a separate `--api-headers`
  flag in order, with `!`-prefixed exclusion patterns passed verbatim.
- When `ApiMarkIncludePaths` is empty for a C++ project, the task returns success with no
  side effects.

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

**C++ library name is forwarded to the tool**: Verifies that the `ApiMarkLibraryName` property
is passed to the spawned tool as the `--library-name` argument for C++ builds. This scenario is
tested by `ApiMarkTask_Cpp_LibraryName_ForwardedToTool`.

**C++ defines semicolons converted to commas**: Verifies that the semicolons in
`ApiMarkDefines` are converted to commas before being forwarded as the `--defines` argument for
C++ builds. This scenario is tested by `ApiMarkTask_Cpp_Defines_SemicolonsConvertedToCommas`.

**C++ standard is forwarded to the tool**: Verifies that the `ApiMarkCppStandard` property is
passed to the spawned tool as the `--cpp-standard` argument for C++ builds. This scenario is
tested by `ApiMarkTask_Cpp_CppStandard_ForwardedToTool`.

**C++ api-headers patterns are forwarded as individual flags**: Verifies that each
semicolon-delimited entry in `ApiMarkApiHeaders` is emitted as its own `--api-headers` flag
in order, and that `!`-prefixed exclusion patterns are forwarded verbatim so the generator
can apply last-match-wins gitignore semantics. This scenario is tested by
`ApiMarkTask_Cpp_ApiHeaders_ForwardedAsIndividualFlags`.

**Empty include paths causes graceful skip for C++ project**: Verifies that when
`ApiMarkIncludePaths` is not set for a C++ project, the task returns success immediately with no
side effects and no tool invocation. This scenario is tested by
`ApiMarkTask_Cpp_EmptyIncludePaths_SkipsExecution`.

**DotNet project executes tool and generates documentation**: End-to-end integration test that
exercises the complete .NET documentation generation path — locates the bundled `ApiMark.Tool.dll`,
spawns it against a real fixture assembly, and verifies that `api.md` is produced in the output
directory. This scenario is tested by `ApiMarkTask_Execute_WithDotNetProject_GeneratesDocumentation`.

**C++ project generates documentation via spawned tool**: Verifies that a C++ project
referencing the `DemaConsulting.ApiMark.MSBuild` NuGet package generates documentation
automatically when a `.vcxproj` build is invoked, confirming end-to-end integration from
package import through task execution to Markdown output. Full C++ NuGet package integration
is verified by the package test; unit-level CppGenerator invocation test is a known gap.
This scenario is tested by
`ApiMarkMsbuild_NuGetPackage_CppVcxprojProject_AutoDocumentsOnBuild`.

**ApiMarkIncludeObsolete flag is forwarded**: Verifies that when `ApiMarkIncludeObsolete` is
set to `true`, the `--include-obsolete` flag is added to the spawned tool command. This
scenario is tested by `ApiMarkTask_IncludeObsolete_True_ForwardsIncludeObsoleteFlag`.
