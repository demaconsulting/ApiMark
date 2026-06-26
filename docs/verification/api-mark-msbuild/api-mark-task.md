## ApiMarkTask

### Verification Approach

`ApiMarkTask` is the MSBuild task that spawns the `ApiMark.Tool` child process with
language-appropriate arguments. Verification uses unit and integration tests in
`test/ApiMark.MSBuild.Tests/` that directly instantiate `ApiMarkTask` and set properties on
the task object, asserting that the correct child-process arguments are constructed and that
skip conditions are honored. End-to-end NuGet package integration tests in
`test/ApiMark.MSBuild.PackageTests/` verify the complete build path using real `.nupkg` and
fixture project files.

### Test Environment

Unit tests (`test/ApiMark.MSBuild.Tests/`) require the .NET SDK and a pre-built
`ApiMark.Tool.dll`; no fixture MSBuild project files, external service, network dependency,
or elevated permission is required, and the integration test requires a writable output
directory for generated Markdown. Package integration tests
(`test/ApiMark.MSBuild.PackageTests/`) additionally require the pre-built
`DemaConsulting.ApiMark.MSBuild` `.nupkg`, a `.vcxproj` fixture project, Windows with
MSBuild and VC++ tools installed; those tests skip gracefully when the package is absent.

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
- A non-zero exit code from the spawned tool causes Execute to return false and log a
  MSBuild error.
- When `dotnet` cannot be located via `DOTNET_HOST_PATH` or `PATH`, Execute returns false
  and logs an MSBuild error.
- The spawned tool's standard output is forwarded to the MSBuild build log as informational
  messages.
- The spawned tool's standard error output is forwarded to the MSBuild build log as error
  messages.
- For C++ builds, `ApiMarkLibraryName` is forwarded as `--library-name` when set.
- For C++ builds, `ApiMarkLibraryDescription` is forwarded as `--library-description` when set.
- For C++ builds, `ApiMarkClangPath` is forwarded as `--clang-path` when set.
- For C++ builds, `ApiMarkDefines` semicolons are converted to commas and forwarded as
  `--defines`.
- For C++ builds, `ApiMarkCppStandard` is forwarded as `--cpp-standard` when set.
- For C++ builds, each semicolon-delimited entry in `ApiMarkApiHeaders` is forwarded as its own
  `--api-headers` flag in order, with `!`-prefixed exclusion patterns passed verbatim.
- When `ApiMarkIncludePaths` is not set for a C++ project, the task returns true immediately
  with no side effects.
- When `ApiMarkXmlDocPath` is not set for a .NET project, the task returns true immediately
  with no side effects.
- When `ApiMarkOutputs` is non-empty, the task spawns one child process per item in the
  `ApiMarkOutput` item group, passing per-item metadata overrides for `OutputDir`, `Format`,
  and `Visibility`.

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

**Cpp tool invocation passes include paths**: Verifies that each semicolon-delimited entry in
`ApiMarkIncludePaths` is forwarded as its own `--includes` flag to the spawned `cpp`
subcommand; the joined string is never passed directly. This scenario is tested by
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

**C++ library name is forwarded as --library-name**: Verifies that the `ApiMarkLibraryName`
property is passed to the spawned tool as the `--library-name` argument for C++ builds.
This scenario is tested by `ApiMarkTask_Cpp_LibraryName_ForwardedToTool`.

**C++ library description is forwarded as --library-description**: Verifies that the
`ApiMarkLibraryDescription` property is passed to the spawned tool as the
`--library-description` argument for C++ builds. This scenario is tested by
`ApiMarkTask_Cpp_LibraryDescription_ForwardedToTool`.

**C++ clang path is forwarded as --clang-path**: Verifies that the `ApiMarkClangPath` property
is passed to the spawned tool as the `--clang-path` argument for C++ builds when set. This
scenario is tested by `ApiMarkTask_Cpp_ClangPath_ForwardedToTool`.

**C++ defines semicolons are converted to commas**: Verifies that semicolons in the
`ApiMarkDefines` property are converted to commas before being forwarded as the `--defines`
argument for C++ builds, because MSBuild uses semicolons as its list separator but the tool
expects commas. This scenario is tested by
`ApiMarkTask_Cpp_Defines_SemicolonsConvertedToCommas`.

**C++ standard is forwarded as --cpp-standard**: Verifies that the `ApiMarkCppStandard`
property is passed to the spawned tool as the `--cpp-standard` argument for C++ builds.
This scenario is tested by `ApiMarkTask_Cpp_CppStandard_ForwardedToTool`.

**C++ api-headers patterns are forwarded as individual flags**: Verifies that each
semicolon-delimited entry in `ApiMarkApiHeaders` is emitted as its own `--api-headers` flag in
order, and that `!`-prefixed exclusion patterns are forwarded verbatim so the tool can apply
last-match-wins gitignore semantics. This scenario is tested by
`ApiMarkTask_Cpp_ApiHeaders_ForwardedAsIndividualFlags`.

**Empty include paths skips execution for C++ project**: Verifies that when
`ApiMarkIncludePaths` is not set for a C++ project, the task returns true immediately without
spawning any process, providing graceful skip behavior parallel to the dotnet XmlDocPath
behavior. This scenario is tested by `ApiMarkTask_Cpp_EmptyIncludePaths_SkipsExecution`.

**Empty XmlDocPath skips execution for .NET project**: Verifies that when `ApiMarkXmlDocPath`
is not set for a .NET project, the task returns true immediately without spawning any process,
providing graceful skip behavior for projects that do not generate XML documentation. This
scenario is tested by `ApiMarkTask_DotNet_EmptyXmlDocPath_SkipsExecution`.

**IncludeObsolete flag is forwarded**: Verifies that when `ApiMarkIncludeObsolete` is set to
`true`, the `--include-obsolete` flag is added to the spawned tool command. This scenario is
tested by `ApiMarkTask_IncludeObsolete_True_ForwardsIncludeObsoleteFlag`.

**DotNet project executes tool and generates documentation**: End-to-end integration test that
exercises the complete .NET documentation generation path — locates the bundled `ApiMark.Tool.dll`,
spawns it against a real fixture assembly, and verifies that `api.md` is produced in the output
directory. This scenario is tested by `ApiMarkTask_Execute_WithDotNetProject_GeneratesDocumentation`.

**C++ project generates documentation via spawned tool**: End-to-end package integration test
that verifies a C++ project referencing the `DemaConsulting.ApiMark.MSBuild` NuGet package
generates documentation when a `.vcxproj` build is invoked. Full C++ NuGet package
integration is verified by the package test (in `test/ApiMark.MSBuild.PackageTests/`);
unit-level CppGenerator invocation test is a known gap. This scenario is tested by
`ApiMarkMsbuild_NuGetPackage_CppVcxprojProject_AutoDocumentsOnBuild` (Windows-only).

**C++ include paths auto-populated from AdditionalIncludeDirectories**: End-to-end package
integration test that verifies `ApiMarkIncludePaths` is correctly defaulted from `ClCompile`
`AdditionalIncludeDirectories` metadata when not explicitly set in the project file. The
`.vcxproj` fixture uses only `ItemDefinitionGroup/ClCompile/AdditionalIncludeDirectories`
with no explicit `$(ApiMarkIncludePaths)` property, and documentation is still generated
successfully. This scenario is tested by
`ApiMarkMsbuild_NuGetPackage_CppVcxprojProject_AutoDocumentsOnBuild`.

**Format is forwarded as --format**: Verifies that the `ApiMarkFormat` value is passed to
the tool as the `--format` argument when set. This scenario is tested by
`ApiMarkTask_Format_ForwardedToToolAsFormatArgument`.

**Format is not forwarded when not set**: Verifies that the `--format` flag is omitted from
the spawned command when `ApiMarkFormat` is null or empty. This scenario is tested by
`ApiMarkTask_Format_NotForwarded_WhenNotSet`.

**BuildArgumentsForOutput overrides scalar properties from item metadata**: Verifies that
`BuildArgumentsForOutput` overrides the `OutputDir`, `Visibility`, and `Format` scalar
properties with values taken from the supplied output item's metadata, so per-output
overrides are applied correctly. This scenario is tested by
`ApiMarkTask_BuildArgumentsForOutput_OverridesScalarPropertiesFromMetadata`.

**BuildArgumentsForOutput restores scalar properties after call**: Verifies that the scalar
properties (`OutputDir`, `Visibility`, `Format`) are restored to their original values after
`BuildArgumentsForOutput` returns, so subsequent calls still use the original property
values. This scenario is tested by
`ApiMarkTask_BuildArgumentsForOutput_RestoresScalarPropertiesAfterCall`.

**Non-zero tool exit returns false and logs error**: Verifies that when the spawned tool
process exits with a non-zero exit code, `Execute` returns false and a MSBuild error is
logged. This scenario is tested by
`ApiMarkTask_Execute_ToolExitsNonZero_ReturnsFalseAndLogsError`.

**dotnet executable not resolved returns false and logs error**: Verifies that when neither
`DOTNET_HOST_PATH` nor `PATH` provides a valid `dotnet` executable, `Execute` returns false
and logs an MSBuild error identifying the problem, allowing the build failure to be diagnosed
quickly. This scenario is tested by
`ApiMarkTask_Execute_DotNetExeNotResolved_ReturnsFalseAndLogsError`.

**Stdout from spawned tool forwarded as informational messages**: Verifies that standard
output written by the spawned `ApiMark.Tool` child process is routed to the MSBuild build log
as informational messages, making generation progress visible in the IDE output window and CI
logs. This scenario is tested by `ApiMarkTask_Execute_WithDotNetProject_GeneratesDocumentation`.

**Stderr from spawned tool forwarded as MSBuild errors**: Verifies that standard error output
written by the spawned tool is routed to the MSBuild build log as error messages, so
diagnostic information is surfaced in the IDE error list and CI failure summary. This scenario
is tested by `ApiMarkTask_Execute_ToolWritesToStderr_ForwardsToMsBuildErrors`.

**Multiple outputs run tool once per output item**: Verifies that when `ApiMarkOutputs` is
non-empty, `Execute` delegates to `ExecuteAllOutputs` and calls `RunToolProcess` exactly once
per item in the `ApiMarkOutput` item group, using per-item metadata to override scalar
properties for each invocation. This scenario is tested by
`ApiMarkTask_Execute_WithMultipleOutputs_RunsToolForEachOutput`.
