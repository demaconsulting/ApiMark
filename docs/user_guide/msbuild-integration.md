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
| `ApiMarkFormat` | _(unset, defaults to `gradual`)_ | Output format: `gradual` (file-per-type) or `single-file` (single `api.md`) |
| `ApiMarkPackDocs` | `false` | Include the `api/` folder in the NuGet package (C# only) |
| `DisableApiMark` | _(unset)_ | Set to `true` to disable generation entirely |
| `ApiMarkLanguage` | _(inferred)_ | Override language: `dotnet` or `cpp` |

### C#-Specific Properties

| Property | Default | Description |
| --- | --- | --- |
| `ApiMarkAssemblyPath` | `$(TargetPath)` | Path to the compiled assembly |
| `ApiMarkXmlDocPath` | `$(DocumentationFile)` | Path to the XML documentation file |

### C++-Specific Properties

> **Note**: C++ MSBuild integration works well for projects where
> `AdditionalIncludeDirectories` is set in the conventional way. For projects with
> unusual include structures, generated headers, or complex NuGet arrangements,
> use the `apimark cpp` CLI command directly — it gives full control over every
> argument passed to clang.

| Property | Default | Description |
| --- | --- | --- |
| `ApiMarkIncludePaths` | _(auto-detected)_ | Semicolon-separated list of include directory paths passed to Clang as `-I` paths. Defaults to the resolved `AdditionalIncludeDirectories` from all `ClCompile` items (including NuGet-injected paths). Set explicitly to override auto-detection. When `ApiMarkApiHeaders` is not set, all headers with recognized C++ extensions under these paths are documented. |
| `ApiMarkApiHeaders` | _(unset)_ | Semicolon-separated, order-preserved list of glob and exclusion pattern strings. Entries with `!` are exclusion patterns; gitignore-style last-match-wins semantics apply. When unset, all headers with recognized C++ extensions under `ApiMarkIncludePaths` are documented. |
| `ApiMarkLibraryName` | `$(MSBuildProjectName)` | Library name used as the top-level heading in `api.md` |
| `ApiMarkLibraryDescription` | _(unset)_ | Optional description for the `api.md` introduction paragraph |
| `ApiMarkDefines` | _(unset)_ | Semicolon-separated preprocessor definitions (e.g. `MYLIB_API=;NDEBUG`) |
| `ApiMarkCppStandard` | `c++17` | C++ language standard passed to Clang |
| `ApiMarkClangPath` | _(auto-discovered)_ | Path to clang executable; overrides PATH / xcrun / vswhere discovery |

## Multiple Output Formats

Use `ApiMarkFormat` to select the output format for a build:

```xml
<PropertyGroup>
  <!-- Write all docs to a single api.md instead of one file per type -->
  <ApiMarkFormat>single-file</ApiMarkFormat>
</PropertyGroup>
```

To generate both formats in one build, use the `ApiMarkOutput` item group.
Each item specifies an `OutputDir`, an optional `Visibility`, and an optional
`Format`. When `ApiMarkOutput` items are present they replace the scalar
`ApiMarkOutputDir`, `ApiMarkVisibility`, and `ApiMarkFormat` properties:

```xml
<ItemGroup>
  <!-- Gradual-disclosure output for browsing -->
  <ApiMarkOutput Include="gradual" OutputDir="$(MSBuildProjectDirectory)\api" Format="gradual" />

  <!-- Single-file output for AI context windows -->
  <ApiMarkOutput Include="single" OutputDir="$(MSBuildProjectDirectory)\api-single" Format="single-file" />
</ItemGroup>
```

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

  <!-- Document all headers except a detail/ subtree (gitignore-style last-match-wins) -->
  <!-- <ApiMarkApiHeaders>**/*;!**/detail/**</ApiMarkApiHeaders> -->

  <!-- Re-include one header from the excluded subtree -->
  <!-- <ApiMarkApiHeaders>**/*;!**/detail/**;**/detail/public_api.h</ApiMarkApiHeaders> -->

  <!-- Override include paths (optional; defaults to AdditionalIncludeDirectories) -->
  <!-- <ApiMarkIncludePaths>$(MSBuildProjectDirectory)\include</ApiMarkIncludePaths> -->
</PropertyGroup>
```

## Including Docs in the NuGet Package

When `ApiMarkPackDocs` is set to `true`, the generated `api/` folder is
included in the NuGet package at `api/`. This allows consumers to access
the documentation directly from the `.nupkg`.

The feature works with both `dotnet pack` and `dotnet pack --no-build` — the
docs are included if they already exist on disk at pack time.
