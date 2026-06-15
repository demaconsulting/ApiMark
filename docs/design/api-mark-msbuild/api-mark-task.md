## ApiMarkTask

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

ApiMarkTask is the MSBuild task entry point for ApiMarkMsbuild. It fires
automatically `AfterTargets="Build"` unless `DisableApiMark` is true. Its sole
responsibility is to translate MSBuild properties into a `dotnet ApiMark.Tool.dll
<language> [options]` child-process invocation and surface the exit code and output
back to the MSBuild build log. ApiMarkTask never loads language-generator code into
the MSBuild host process.

### Data Model

**ApiMarkTask**: MSBuild `Task`-derived class — the single public entry point of
the ApiMarkMsbuild system.

**ApiMarkTask.DisableApiMark**: `bool` — MSBuild property `$(DisableApiMark)`;
when true, the task returns success immediately with no side effects, allowing
projects to opt out of documentation generation without removing the package.

**ApiMarkTask.ApiMarkLanguage**: `string` — MSBuild property `$(ApiMarkLanguage)`;
selects the generation language. Accepted values: `dotnet`, `cpp`. Any other value
causes `Execute` to log an error and return `false`. When not set,
the task infers the language: `.vcxproj` project → `cpp`, all others → `dotnet`.

**ApiMarkTask.ProjectExtension**: `string` — MSBuild property
`$(MSBuildProjectExtension)`; required. Provides the project file extension used
to infer the generation language when `ApiMarkLanguage` is not explicitly set.
A value of `.vcxproj` (case-insensitive) causes the task to infer `cpp`; all
other extensions infer `dotnet`.

**ApiMarkTask.ApiMarkOutputDir**: `string` — MSBuild property `$(ApiMarkOutputDir)`;
the directory where Markdown output is written.

**ApiMarkTask.ApiMarkVisibility**: `string` — MSBuild property
`$(ApiMarkVisibility)`; the visibility filter forwarded to the tool. Accepted
values: `Public`, `PublicAndProtected`, `All`. When not set, the `--visibility`
flag is omitted and the tool applies its own default of `Public`.

**ApiMarkTask.ApiMarkIncludeObsolete**: `bool` — MSBuild property
`$(ApiMarkIncludeObsolete)`; when true, the tool includes members marked
`[Obsolete]`. Defaults to false.

**ApiMarkTask.ApiMarkAssemblyPath**: `string` — MSBuild property
`$(ApiMarkAssemblyPath)`; for the `dotnet` language, the path to the compiled
assembly. Defaults to `$(TargetPath)` via the `.targets` file when not explicitly
set.

**ApiMarkTask.ApiMarkXmlDocPath**: `string` — MSBuild property
`$(ApiMarkXmlDocPath)`; for the `dotnet` language, the path to the
compiler-generated XML documentation file. Defaults to `$(DocumentationFile)` via
the `.targets` file when not explicitly set. If not set and the language is
`dotnet`, the task skips generation and returns success.

**ApiMarkTask.ApiMarkIncludePaths**: `string` — MSBuild property
`$(ApiMarkIncludePaths)`; for the `cpp` language, a semicolon-separated list of
include directory paths. Each entry is forwarded as an individual `--includes`
flag; all paths are passed to Clang as `-I` flags. When `ApiMarkApiHeaders` is
not set, all headers with recognized C++ extensions under these paths are
documented. For `.vcxproj` projects this property is automatically defaulted by
the `.targets` file from the `AdditionalIncludeDirectories` metadata of all
`ClCompile` items (including paths injected by NuGet packages) when the property
is not set explicitly. Setting `$(ApiMarkIncludePaths)` in the project file
suppresses auto-population and uses the supplied value instead.

**ApiMarkTask.ApiMarkApiHeaders**: `string` — MSBuild property
`$(ApiMarkApiHeaders)`; for the `cpp` language, a semicolon-separated,
order-preserved list of glob and exclusion pattern strings forwarded as individual
`--api-headers` flags. Entries with a `!` prefix are exclusion patterns;
gitignore-style last-match-wins semantics apply. Optional — when empty or not
set, all headers with recognized C++ extensions under `ApiMarkIncludePaths` are
documented.

**ApiMarkTask.ApiMarkLibraryName**: `string` — MSBuild property
`$(ApiMarkLibraryName)`; for the `cpp` language, the library name used as the
top-level heading in `api.md`. The `.targets` file defaults this to
`$(MSBuildProjectName)` when not explicitly set.

**ApiMarkTask.ApiMarkLibraryDescription**: `string` — MSBuild property
`$(ApiMarkLibraryDescription)`; for the `cpp` language, an optional description
emitted as an introductory paragraph in `api.md`. Omitted when empty or not set.

**ApiMarkTask.ApiMarkDefines**: `string` — MSBuild property
`$(ApiMarkDefines)`; for the `cpp` language, a semicolon-separated list of
preprocessor symbol definitions passed to the Clang parser. Semicolons are
converted to commas when forwarding to the `--defines` argument.

**ApiMarkTask.ApiMarkCppStandard**: `string` — MSBuild property
`$(ApiMarkCppStandard)`; for the `cpp` language, the C++ language standard
passed to Clang (e.g. `c++17`, `c++20`). The `.targets` file defaults this to
`c++17` when not explicitly set.

**ApiMarkTask.ApiMarkClangPath**: `string` — MSBuild property
`$(ApiMarkClangPath)`; for the `cpp` language, the path to the clang
executable. Optional — when empty, clang is located using the priority order:
`APIMARK_CLANG_PATH` environment variable → `clang` on PATH → `xcrun` (macOS)
→ vswhere / default LLVM path (Windows).

**ApiMarkTask.ApiMarkFormat**: `string` — MSBuild property `$(ApiMarkFormat)`;
controls the output format forwarded to the tool. Accepted values: `gradual`
(multi-file gradual-disclosure tree) or `single-file` (single `api.md`
document). Defaults to empty — the tool uses its own default of `gradual` when
not set.

**ApiMarkTask.ApiMarkOutputs**: `ITaskItem[]` — MSBuild item group
`@(ApiMarkOutput)`; optional. When non-empty, the task spawns one child process
per item. Each item's `OutputDir`, `Format`, and `Visibility` metadata override
the corresponding scalar properties (`ApiMarkOutputDir`, `ApiMarkFormat`,
`ApiMarkVisibility`) for that invocation. The scalar `ApiMarkOutputDir`,
`ApiMarkFormat`, and `ApiMarkVisibility` remain fully backward-compatible; when
`ApiMarkOutputs` is empty or absent, the task behaves exactly as before.

Example item group usage:

```xml
<ItemGroup>
  <ApiMarkOutput Include="InternalDocs">
    <OutputDir>$(MSBuildProjectDirectory)\docs\api</OutputDir>
    <Format>single-file</Format>
    <Visibility>All</Visibility>
  </ApiMarkOutput>
  <ApiMarkOutput Include="PublicDocs">
    <OutputDir>$(MSBuildProjectDirectory)\api</OutputDir>
    <Format>gradual</Format>
    <Visibility>Public</Visibility>
  </ApiMarkOutput>
</ItemGroup>
```

**ApiMarkTask.ToolDllPath**: `string` — set by the `.targets` file to the path of
the bundled `ApiMark.Tool.dll` inside the NuGet package `tools/net8.0/` directory.
Not intended to be overridden by project authors.

### Key Methods

**ApiMarkTask.Execute**: MSBuild entry point; resolves all paths, builds the
argument list, spawns the tool process, and pipes its output to the MSBuild log.

- *Parameters*: none — MSBuild sets task properties before calling Execute.
- *Returns*: `bool` — true if the child process exits with code zero; false
  otherwise.
- *Preconditions*: `ToolDllPath` must point to a file that exists on disk;
  `dotnet` must be locatable via `DOTNET_HOST_PATH` or `PATH`.
- *Postconditions*: On success (exit code zero), the `ApiMarkOutputDir` contains
  the generated Markdown tree. On failure (non-zero exit code), at least one
  MSBuild error has been logged and Execute returns false, causing MSBuild to mark
  the build as failed.

Execution steps: check `DisableApiMark` — if true, return true immediately; resolve
language from `ApiMarkLanguage` or project extension inference; if language is
not `dotnet` or `cpp`, log an error and return false; if language is
`dotnet` and `ApiMarkXmlDocPath` is not set, return true (skip generation); if
language is `cpp` and `ApiMarkIncludePaths` is not set, return true (skip
generation with an informational log message); resolve the `dotnet` executable
path (check `DOTNET_HOST_PATH` environment variable first, then search `PATH`);
if `ApiMarkOutputs` is non-empty, delegate to `ExecuteAllOutputs` which spawns one
child process per item using metadata overrides for `OutputDir`, `Format`, and
`Visibility`; otherwise build the argument list from scalar MSBuild properties
according to language-specific mapping (for `cpp`, split `ApiMarkIncludePaths` on
`;` and emit one `--includes` flag per entry; split `ApiMarkApiHeaders` on `;` and
emit one `--api-headers` flag per entry, order-preserved including `!` exclusion
patterns; if `ApiMarkLibraryName` is set, append `--library-name`; if
`ApiMarkLibraryDescription` is set, append `--library-description`; if
`ApiMarkDefines` is set, convert semicolons to commas and append `--defines`; if
`ApiMarkCppStandard` is set, append `--cpp-standard`; if `ApiMarkClangPath` is set,
append `--clang-path`; if `ApiMarkFormat` is set, append `--format`); start the
child process and pipe stdout lines as MSBuild messages and stderr lines as MSBuild
errors; wait for exit; return true if exit code is zero, otherwise log an error
with the exit code and return false.

**ApiMarkTask.ExecuteAllOutputs** (private): Iterates `ApiMarkOutputs` and spawns
one child process per item.

- *Parameters*: `string dotnetExe`, `string language`.
- *Returns*: `bool` — true only when every child process exits with code zero.
- *Algorithm*: for each `ApiMarkOutputs` item calls `BuildArgumentsForOutput` then
  `RunToolProcess`; accumulates failures; returns false if any process failed.

### Error Handling

A non-zero exit code from the child process is logged as an MSBuild error and causes
Execute to return false, failing the build. If the `dotnet` executable cannot be
found, the task logs a clear error identifying the problem and returns false. If
`ToolDllPath` does not exist, the task logs an error and returns false before
attempting to spawn the process. When `DisableApiMark` is true, `ApiMarkXmlDocPath`
is not set (dotnet language), or `ApiMarkIncludePaths` is not set (cpp language),
the task returns true silently with no side effects.

### Dependencies

- **Microsoft.Build.Framework** — `ITask`, `IBuildEngine` (provided by the MSBuild
  host; no NuGet reference needed when targeting `netstandard2.0` with the
  appropriate SDK package).
- **Microsoft.Build.Utilities.Core** — `Task` base class, `Log.LogMessage`,
  `Log.LogError`.
- **System.Diagnostics.Process** — BCL; used to spawn the `dotnet` child process
  and capture stdout/stderr.

### Callers

N/A — entry point, called by the MSBuild host environment via the task execution
pipeline when the `AfterTargets="Build"` target fires.
