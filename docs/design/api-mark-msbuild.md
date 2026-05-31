# ApiMarkMsbuild

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

## Architecture

ApiMarkMsbuild is a single-unit system containing only the ApiMarkTask unit. It is
the MSBuild integration layer that runs documentation generation as part of any
build — both `.csproj` SDK-style builds (Windows, macOS, Linux) and `.vcxproj`
Visual Studio C++ builds (Windows).

Rather than calling language generators in-process, ApiMarkTask spawns the
`ApiMark.Tool` .NET executable as a child process, passing all configuration as
command-line arguments. This out-of-process design allows the language generators
to target `net8.0` freely — using libraries that do not support `netstandard2.0`
such as newer versions of CppAst.Net — while the MSBuild task itself targets
`netstandard2.0` for full compatibility with both .NET Framework MSBuild (Visual
Studio) and the .NET SDK MSBuild (`dotnet build`).

The NuGet package `ApiMark.MSBuild` bundles both the task assembly and the
pre-compiled `ApiMark.Tool` DLL and its dependencies under `tools/net8.0/`. The
`.targets` file wires `ApiMarkTask` into `AfterTargets="Build"` and sets the
`ToolDllPath` property to the bundled tool location.

When `ApiMarkPackDocs` is set to `true`, the `.targets` file also hooks the
`_ApiMarkIncludeDocsInPackage` target into `TargetsForTfmSpecificContentInPackage`
so that `dotnet pack` includes the generated `api/` folder in the NuGet package under
`api/`. This feature defaults to `false` so the package size is unaffected unless the
project explicitly opts in.

## External Interfaces

**MSBuild task (provided)**: ApiMarkTask is consumed by MSBuild as an in-process
task.

- *Type*: MSBuild task (`netstandard2.0`, in-process in the MSBuild host).
- *Role*: Provider — `.csproj` and `.vcxproj` files reference the task via the
  NuGet package; MSBuild invokes the task at build time.
- *Contract*: MSBuild properties `ApiMarkLanguage`, `ApiMarkOutputDir`,
  `ApiMarkVisibility`, `ApiMarkIncludeObsolete`, `DisableApiMark`,
  `ProjectExtension` (required; maps to `$(MSBuildProjectExtension)` and is used
  to infer the language when `ApiMarkLanguage` is not set), `ApiMarkPackDocs`
  (opt-in; when `true`, causes the generated `api/` folder to be included in the
  NuGet package via `TargetsForTfmSpecificContentInPackage`), and language-specific
  properties (`ApiMarkAssemblyPath`, `ApiMarkXmlDocPath` for dotnet;
  `ApiMarkIncludePaths` for cpp). Fires `AfterTargets="Build"` unless
  `DisableApiMark` is true. Language is inferred from `ProjectExtension` when
  `ApiMarkLanguage` is not explicitly set.
- *Constraints*: Must not load any language-generator libraries in the MSBuild
  process; all generation is delegated out-of-process. `dotnet` must be locatable
  at task execution time.

**ApiMark.Tool process (spawned)**: ApiMarkTask spawns the `ApiMark.Tool` .NET
executable to perform generation.

- *Type*: Child process; invoked via `dotnet <ToolDllPath> <language> [options]`.
- *Role*: Consumer — ApiMarkTask locates `dotnet`, builds the argument list from
  MSBuild properties, and starts the process.
- *Contract*: `dotnet <ToolDllPath> dotnet --assembly <path> --xml-doc <path>
  [--output <dir>] [--visibility <value>] [--include-obsolete]` for .NET;
  `dotnet <ToolDllPath> cpp --includes <paths> [--output <dir>]
  [--visibility <value>] [--include-obsolete]` for C++.
- *Constraints*: `ToolDllPath` is set by the `.targets` file to the bundled
  `ApiMark.Tool.dll`. The `dotnet` executable is resolved from the
  `DOTNET_HOST_PATH` environment variable first, then from `PATH`. The task treats
  a non-zero exit code as a build failure.

## Dependencies

- **ApiMark.Tool** (out-of-process): all documentation generation is delegated to
  the spawned tool process; no in-process dependency on ApiMarkDotNet or ApiMarkCpp.
- **Microsoft.Build.Framework / Microsoft.Build.Utilities.Core**: the MSBuild task
  base class and logging APIs.

## Risk Control Measures

N/A — not a safety-classified software item.

## Data Flow

1. MSBuild evaluates the `.targets` file and sets `ApiMarkTask` properties from the
   project property bag before invoking the task.
2. ApiMarkTask checks `DisableApiMark`; if true, returns success immediately.
3. ApiMarkTask determines `ApiMarkLanguage`: if not set, infers `cpp` for
   `.vcxproj` projects and `dotnet` for all others.
4. ApiMarkTask resolves the `dotnet` executable path using `DOTNET_HOST_PATH`,
   falling back to searching `PATH`.
5. ApiMarkTask builds the CLI argument list from MSBuild properties according to
   the language-specific argument mapping.
6. ApiMarkTask starts the child process `dotnet <ToolDllPath> <language> [args]`
   and captures both stdout and stderr.
7. While the process runs, ApiMarkTask pipes stdout lines as MSBuild messages and
   stderr lines as MSBuild errors.
8. When the process exits, ApiMarkTask returns true if the exit code is zero or
   false if non-zero, causing MSBuild to mark the build as failed.

**NuGet packaging flow (when `ApiMarkPackDocs` is `true`)**: The `.targets` file
hooks `_ApiMarkIncludeDocsInPackage` into `TargetsForTfmSpecificContentInPackage`.
During `dotnet pack`, this target collects all files under `ApiMarkOutputDir` as
`TfmSpecificPackageFile` items with `PackagePath="api/..."`, causing them to be
bundled in the NuGet package.

## Design Constraints

- Task platform: targets `netstandard2.0` so the same assembly runs in .NET
  Framework MSBuild (Visual Studio) and .NET SDK MSBuild (`dotnet build`).
- Tool platform: `ApiMark.Tool.dll` targets `net8.0`; language generators are free
  to use any library regardless of `netstandard` support.
- Cross-platform `dotnet` resolution: `DOTNET_HOST_PATH` is set by the SDK on all
  platforms; the task must fall back to searching `PATH` for environments where it
  is not set (e.g., Visual Studio on .NET Framework MSBuild).
- No in-process generation: language libraries must never be loaded into the MSBuild
  host process; ApiMarkTask must remain a thin process spawner.
- Graceful opt-out: when `DisableApiMark` is true, the task returns success with no
  side effects so projects can suppress generation without removing the package.
