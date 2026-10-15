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

## Default Visibility

For C# projects, `ApiMarkVisibility` defaults to `PublicAndProtected`. Set
`<ApiMarkVisibility>Public</ApiMarkVisibility>` to document public members only. C++ and VHDL
default to `Public`. `ApiMarkEnforceDocs` is independent of this setting.

## External Links

`ApiMarkExternalLinks` is a semicolon-separated list of `Assembly=path` pairs (path is a relative path or an
`http(s)` URL to that assembly's separately generated ApiMark documentation); it applies to C# projects. As
`;` is the list separator, a path or URL containing `;` cannot be expressed.

A path that contains the library's version, such as a URL under a versioned documentation site, should use
the `{version}` token. The task replaces it with the first non-empty of
`ApiMarkLinkVersion`, `PackageVersion`, and `Version`:

```xml
<PropertyGroup>
  <ApiMarkExternalLinks>Other.Lib=https://docs.example.com/other-lib/{version}/api</ApiMarkExternalLinks>
</PropertyGroup>
```

NuGet packs with `PackageVersion` when it is set and falls back to `Version`, so this order matches the
version the package gets. Use `$(PackageVersion)` or `$(Version)` directly in the mapping only when you want
that specific property: `dotnet pack -p:PackageVersion=1.2.3` sets `PackageVersion` but not `Version`, so a
mapping that uses `$(Version)` evaluates to the project's default (`1.0.0`) and the links are dead. To force
a different value, set `ApiMarkLinkVersion` in your project. If neither `ApiMarkLinkVersion`,
`PackageVersion`, nor `Version` is set, the `{version}` token is left unchanged.

When `PackageVersion` and `Version` are both set, differ, and `ApiMarkExternalLinks` is configured, the task
logs a warning naming both values and the one that `{version}` uses, because the two normally agree.

## Including Docs in the NuGet Package

When `ApiMarkPackDocs` is set to `true`, the generated `api/` folder is included in the
NuGet package at `api/`. This allows consumers to access the documentation directly from
the `.nupkg`.

The feature works with both `dotnet pack` and `dotnet pack --no-build` — the docs are
included if they already exist on disk at pack time.

## See Also

See the _.NET Documentation_ section for C#-specific MSBuild properties such as
`ApiMarkAssemblyPath`, `ApiMarkXmlDocPath`, `ApiMarkVisibility`,
`ApiMarkIncludeObsolete`, `ApiMarkExternalLinks`, and the opt-in `ApiMarkEnforceDocs`/
`ApiMarkEnforceDocsSeverity` documentation-coverage enforcement properties.

See the _C++ Documentation_ section for C++-specific MSBuild properties such as
`ApiMarkIncludePaths`, `ApiMarkApiHeaders`, `ApiMarkDefines`, `ApiMarkCppStandard`,
`ApiMarkClangPath`, and the opt-in `ApiMarkEnforceDocs`/`ApiMarkEnforceDocsSeverity`
documentation-coverage enforcement properties (supported for `cpp` alongside
`dotnet` — VHDL documentation-coverage enforcement is CLI-only today, since
VHDL has no MSBuild task support at all).
