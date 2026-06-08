# MSBuild Integration

The `DemaConsulting.ApiMark.MSBuild` NuGet package adds an MSBuild task that
runs automatically after every build, generating API reference documentation
in Markdown alongside the compiled output. It supports both `.csproj` (C#)
and `.vcxproj` (C++) projects — the language is detected automatically from
the project file extension.

## Platform Support

| Platform | C# | C++ |
| --- | --- | --- |
| Windows x64 | ✅ | ✅ |
| Linux x64 | ✅ | ✅ |
| macOS (Apple Silicon) | ✅ | ✅ |

## MSBuild Properties

### Common Properties

| Property | Default | Description |
| --- | --- | --- |
| `ApiMarkOutputDir` | `$(MSBuildProjectDirectory)\api` | Output directory for generated Markdown |
| `ApiMarkVisibility` | `Public` | Visibility filter: `Public`, `PublicAndProtected`, `All` |
| `ApiMarkIncludeObsolete` | `false` | Include `[Obsolete]` / deprecated members |
| `ApiMarkPackDocs` | `false` | Include the `api/` folder in the NuGet package (C# only) |
| `DisableApiMark` | _(unset)_ | Set to `true` to disable generation entirely |
| `ApiMarkLanguage` | _(inferred)_ | Override language: `dotnet` or `cpp` |

### C#-Specific Properties

| Property | Default | Description |
| --- | --- | --- |
| `ApiMarkAssemblyPath` | `$(TargetPath)` | Path to the compiled assembly |
| `ApiMarkXmlDocPath` | `$(DocumentationFile)` | Path to the XML documentation file |

### C++-Specific Properties

| Property | Default | Description |
| --- | --- | --- |
| `ApiMarkIncludePaths` | _(required)_ | Semicolon-separated list of public include directories. Entries with `*` or `?` are forwarded as `--include-patterns`; entries starting with `!` are forwarded as `--exclude-patterns` (with `!` stripped). |
| `ApiMarkLibraryName` | `$(MSBuildProjectName)` | Library name used as the top-level heading in `api.md` |
| `ApiMarkLibraryDescription` | _(unset)_ | Optional description for the `api.md` introduction paragraph |
| `ApiMarkDefines` | _(unset)_ | Comma-separated preprocessor definitions (e.g. `MYLIB_API=,NDEBUG`) |
| `ApiMarkCppStandard` | `c++17` | C++ language standard passed to Clang |
| `ApiMarkClangPath` | _(auto-discovered)_ | Path to clang executable; overrides PATH / xcrun / vswhere discovery |
| `ApiMarkSearchPaths` | _(unset)_ | Semicolon-separated compiler-only `-I` paths for `#include` resolution; declarations are never documented |

## Configuration Examples

### C# Project

```xml
<PropertyGroup>
  <!-- Required: enable XML documentation so ApiMark can read doc comments -->
  <GenerateDocumentationFile>true</GenerateDocumentationFile>

  <!-- Change the output directory -->
  <ApiMarkOutputDir>$(MSBuildProjectDirectory)\docs\api</ApiMarkOutputDir>

  <!-- Include protected members as well as public ones -->
  <ApiMarkVisibility>PublicAndProtected</ApiMarkVisibility>

  <!-- Include the generated api/ folder in the NuGet package -->
  <ApiMarkPackDocs>true</ApiMarkPackDocs>

  <!-- Disable generation entirely (e.g., for test projects) -->
  <DisableApiMark>true</DisableApiMark>
</PropertyGroup>
```

### C++ Project

```xml
<PropertyGroup>
  <!-- Required: the public include root(s) to document -->
  <ApiMarkIncludePaths>$(MSBuildProjectDirectory)\include</ApiMarkIncludePaths>

  <!-- Change the output directory -->
  <ApiMarkOutputDir>$(MSBuildProjectDirectory)\docs\api</ApiMarkOutputDir>

  <!-- Set the library name used in api.md heading -->
  <ApiMarkLibraryName>MyLibrary</ApiMarkLibraryName>

  <!-- Add a one-line description to api.md -->
  <ApiMarkLibraryDescription>A fast, portable geometry library.</ApiMarkLibraryDescription>

  <!-- Use C++20 -->
  <ApiMarkCppStandard>c++20</ApiMarkCppStandard>

  <!-- Override clang path (optional; normally auto-discovered) -->
  <!-- <ApiMarkClangPath>C:\Program Files\LLVM\bin\clang.exe</ApiMarkClangPath> -->

  <!-- Compiler-only search paths (e.g. SDK headers not part of the documented API) -->
  <!-- <ApiMarkSearchPaths>$(SdkIncludePath)</ApiMarkSearchPaths> -->

  <!-- Glob and exclusion syntax is supported in ApiMarkIncludePaths: -->
  <!-- <ApiMarkIncludePaths>$(MSBuildProjectDirectory)\include;*.h;!detail/**</ApiMarkIncludePaths> -->
</PropertyGroup>
```

## Including Docs in the NuGet Package

When `ApiMarkPackDocs` is set to `true`, the generated `api/` folder is
included in the NuGet package at `api/`. This allows consumers to access
the documentation directly from the `.nupkg`.

The feature works with both `dotnet pack` and `dotnet pack --no-build` — the
docs are included if they already exist on disk at pack time.
