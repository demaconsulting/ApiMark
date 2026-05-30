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
selects the generation language. Accepted values: `dotnet`, `cpp`. When not set,
the task infers the language: `.vcxproj` project → `cpp`, all others → `dotnet`.

**ApiMarkTask.ApiMarkOutputDir**: `string` — MSBuild property `$(ApiMarkOutputDir)`;
the directory where Markdown output is written.

**ApiMarkTask.ApiMarkVisibility**: `string` — MSBuild property
`$(ApiMarkVisibility)`; the visibility filter forwarded to the tool. Accepted
values: `Public`, `PublicAndProtected`, `All`. Defaults to `Public` when not set.

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
include paths passed to the C++ parser.

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
  `dotnet` must be locatable via `DOTNET_HOST_PATH` or `PATH`; `ApiMarkOutputDir`
  must be a writable path.
- *Postconditions*: On success (exit code zero), the `ApiMarkOutputDir` contains
  the generated Markdown tree. On failure (non-zero exit code), at least one
  MSBuild error has been logged and Execute returns false, causing MSBuild to mark
  the build as failed.

Execution steps: check `DisableApiMark` — if true, return true immediately; resolve
language from `ApiMarkLanguage` or project extension inference; if language is
`dotnet` and `ApiMarkXmlDocPath` is not set, return true (skip generation); resolve
the `dotnet` executable path (check `DOTNET_HOST_PATH` environment variable first,
then search `PATH`); build the argument list from MSBuild properties according to
language-specific mapping; start the child process and pipe stdout lines as MSBuild
messages and stderr lines as MSBuild errors; wait for exit; return true if exit code
is zero, otherwise log an error with the exit code and return false.

### Error Handling

A non-zero exit code from the child process is logged as an MSBuild error and causes
Execute to return false, failing the build. If the `dotnet` executable cannot be
found, the task logs a clear error identifying the problem and returns false. If
`ToolDllPath` does not exist, the task logs an error and returns false before
attempting to spawn the process. When `DisableApiMark` is true or `ApiMarkXmlDocPath`
is not set (dotnet language), the task returns true silently with no side effects.

### Dependencies

- **Microsoft.Build.Framework** — `ITask`, `IBuildEngine` (provided by the MSBuild
  host; no NuGet reference needed when targeting `netstandard2.0` with the
  appropriate SDK package).
- **Microsoft.Build.Utilities.Core** — `Task` base class, `ToolTask` helper APIs,
  `Log.LogMessage`, `Log.LogError`.
- **System.Diagnostics.Process** — BCL; used to spawn the `dotnet` child process
  and capture stdout/stderr.

### Callers

N/A — entry point, called by the MSBuild host environment via the task execution
pipeline when the `AfterTargets="Build"` target fires.
