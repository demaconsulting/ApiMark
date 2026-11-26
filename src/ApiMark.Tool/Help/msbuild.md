---
title: MSBuild integration
summary: Generate the API reference after every build with the MSBuild package.
aliases:
  - msbuild-package
  - csproj
  - vcxproj
  - build-integration
tags:
  - reference
order: 9
see-also:
  - dotnet
  - cpp
  - doc-coverage
examples:
  - name: package-reference
    kind: snippet
    language: xml
    save-as: package-reference.xml
    description: Add the package to a .csproj or .vcxproj project.
    content: |
      <ItemGroup>
        <PackageReference Include="DemaConsulting.ApiMark.MSBuild" Version="x.y.z" />
      </ItemGroup>
  - name: properties
    kind: snippet
    language: xml
    save-as: properties.xml
    description: Choose the folder and the format, and fail on gaps.
    content: |
      <PropertyGroup>
        <GenerateDocumentationFile>true</GenerateDocumentationFile>
        <ApiMarkOutputDir>$(MSBuildProjectDirectory)\docs\api</ApiMarkOutputDir>
        <ApiMarkFormat>single-file</ApiMarkFormat>
        <ApiMarkEnforceDocs>Public</ApiMarkEnforceDocs>
        <ApiMarkEnforceDocsSeverity>Error</ApiMarkEnforceDocsSeverity>
      </PropertyGroup>
---
The package `DemaConsulting.ApiMark.MSBuild` runs a copy of {{tool}} that
it carries after each successful build of the project, so no tool install
is needed ([[#package-reference]]). It needs the .NET 10 runtime or later.
A `.vcxproj` project is documented as `cpp`, any other project as
`dotnet`; set `ApiMarkLanguage` to choose. VHDL has no MSBuild support.
A project with several target frameworks is documented once, for the
first one.

**Common properties** ([[#properties]]):

- `ApiMarkOutputDir`: the folder; default `api` beside the project.
- `ApiMarkFormat`: `gradual` (default) or `single-file`.
- `ApiMarkVisibility`, `ApiMarkIncludeObsolete`, `ApiMarkLibraryName`,
  and `ApiMarkLibraryDescription`: as the matching options.
- `ApiMarkEnforceDocs` and `ApiMarkEnforceDocsSeverity`: see
  [[doc-coverage]].
- `DisableApiMark`: `true` turns generation off.
- `ApiMarkPackDocs`: `true` adds the pages to the NuGet package as `api/`.

**.NET projects.** The assembly and its XML file are found from the build;
turn on `GenerateDocumentationFile`. `ApiMarkExclude` lists patterns for
`--exclude`. The referenced assemblies become `--reference-paths` unless
you set `ApiMarkReferencePaths` or `ApiMarkDisableReferencePathsHarvest`.
`ApiMarkExternalLinks` lists `Assembly=path` pairs, separated by `;`; a
`{version}` in a path becomes `ApiMarkLinkVersion`, else `PackageVersion`,
else `Version`.

**C++ projects.** `ApiMarkIncludePaths` defaults to the include folders of
the project. `ApiMarkApiHeaders`, `ApiMarkDefines`, `ApiMarkCppStandard`,
`ApiMarkClangPath`, and `ApiMarkClangTimeoutMs` match the options of
[[cpp]].

**Several trees.** `ApiMarkOutput` items, each with `OutputDir` and an
optional `Format` and `Visibility`, write more than one tree in one build
and replace the three matching properties.
