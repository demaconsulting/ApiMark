# MSBuild Integration

The `DemaConsulting.ApiMark.MSBuild` NuGet package adds an MSBuild task that runs
automatically after every build, generating API reference documentation in Markdown
alongside the compiled output.

## Overview

The `ApiMarkTask` runs the `apimark` CLI tool out-of-process immediately after a
successful build. The language is inferred from the project file extension:

- `.vcxproj` → `cpp`
- All other project types → `dotnet`

Set `ApiMarkLanguage` explicitly to override automatic detection.

## Common Properties

| Property | Default | Description |
| --- | --- | --- |
| `ApiMarkOutputDir` | `$(MSBuildProjectDirectory)\api` | Output directory for generated Markdown |
| `ApiMarkFormat` | _(unset, defaults to `gradual`)_ | Output format: `gradual` or `single-file` |
| `ApiMarkLanguage` | _(inferred)_ | Override language detection: `dotnet` or `cpp` |
| `ApiMarkPackDocs` | `false` | Include the `api/` folder in the NuGet package (C# only) |
| `DisableApiMark` | _(unset)_ | Set to `true` to disable generation entirely |

## Multiple Output Trees

To generate both gradual-disclosure and single-file output in the same build, use the
`ApiMarkOutput` item group. Each item specifies an `OutputDir`, an optional `Visibility`,
and an optional `Format`. When `ApiMarkOutput` items are present they replace the scalar
`ApiMarkOutputDir`, `ApiMarkVisibility`, and `ApiMarkFormat` properties:

```xml
<ItemGroup>
  <!-- Gradual-disclosure output for browsing -->
  <ApiMarkOutput Include="gradual" OutputDir="$(MSBuildProjectDirectory)\api" Format="gradual" />

  <!-- Single-file output for AI context windows -->
  <ApiMarkOutput Include="single" OutputDir="$(MSBuildProjectDirectory)\api-single" Format="single-file" />
</ItemGroup>
```

## Including Docs in the NuGet Package

When `ApiMarkPackDocs` is set to `true`, the generated `api/` folder is included in the
NuGet package at `api/`. This allows consumers to access the documentation directly from
the `.nupkg`.

The feature works with both `dotnet pack` and `dotnet pack --no-build` — the docs are
included if they already exist on disk at pack time.

## See Also

See the _.NET Documentation_ section for C#-specific MSBuild properties such as
`ApiMarkAssemblyPath`, `ApiMarkXmlDocPath`, `ApiMarkVisibility`, and
`ApiMarkIncludeObsolete`.

See the _C++ Documentation_ section for C++-specific MSBuild properties such as
`ApiMarkIncludePaths`, `ApiMarkApiHeaders`, `ApiMarkDefines`, `ApiMarkCppStandard`,
and `ApiMarkClangPath`.
