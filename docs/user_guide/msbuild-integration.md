# MSBuild Integration

The `DemaConsulting.ApiMark.MSBuild` NuGet package adds an MSBuild task that
runs automatically after every `dotnet build`, generating API reference
documentation in Markdown alongside the compiled output.

## MSBuild Properties

| Property | Default | Description |
|---|---|---|
| `ApiMarkOutputDir` | `$(MSBuildProjectDirectory)\api` | Output directory for generated Markdown |
| `ApiMarkVisibility` | `Public` | Visibility filter: `Public`, `PublicAndProtected`, `All` |
| `ApiMarkAssemblyPath` | `$(TargetPath)` | Path to the compiled assembly |
| `ApiMarkXmlDocPath` | `$(DocumentationFile)` | Path to the XML documentation file |
| `ApiMarkIncludeObsolete` | `false` | Include `[Obsolete]` members |
| `ApiMarkPackDocs` | `false` | Include the `api/` folder in the NuGet package |
| `DisableApiMark` | _(unset)_ | Set to `true` to disable generation entirely |
| `ApiMarkLanguage` | _(inferred)_ | Override language: `dotnet` or `cpp` |

## Configuration Example

```xml
<PropertyGroup>
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

## Including Docs in the NuGet Package

When `ApiMarkPackDocs` is set to `true`, the generated `api/` folder is
included in the NuGet package at `api/`. This allows consumers to access
the documentation directly from the `.nupkg`.

The feature works with both `dotnet pack` and `dotnet pack --no-build` — the
docs are included if they already exist on disk at pack time.
