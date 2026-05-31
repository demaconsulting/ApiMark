# ApiMark

[![Build](https://github.com/DemaConsulting/ApiMark/actions/workflows/build_on_push.yaml/badge.svg)](https://github.com/DemaConsulting/ApiMark/actions/workflows/build_on_push.yaml)

<!-- IMPORTANT: All links in this file must be absolute URLs.
     This file is distributed in packages and relative links will not resolve. -->

## Overview

ApiMark generates compact, AI-friendly API reference documentation in Markdown
from source code and associated metadata (XML doc comments, header files,
docstrings, etc.). The output is designed for gradual disclosure: an AI can
read a lightweight index, drill into a namespace summary, and then read a full
type page — consuming only as much context as the task requires.

## Features

- Generates compact Markdown API reference from XML doc comments and source code
- Gradual disclosure output: root index → namespace summary → full type page
- C#/.NET support via Mono.Cecil and XML documentation comments
- MSBuild task integration for `.csproj`-based builds
- `dotnet tool` CLI (`apimark`) covering all supported languages
- Designed for AI consumption — minimal noise, explicit navigation links between levels

## Installation

### CLI Tool

```bash
dotnet tool install --global DemaConsulting.ApiMark.Tool
```

### MSBuild Integration

Add the NuGet package to your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="DemaConsulting.ApiMark.MSBuild" Version="x.y.z" />
</ItemGroup>
```

Enable XML documentation generation so ApiMark can read doc comments:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

ApiMark generates documentation automatically after every `dotnet build`.

## Usage

### CLI Usage

```bash
# Generate API documentation from a .NET assembly
apimark dotnet --assembly MyProject.dll --xml-doc MyProject.xml --output docs/api
```

Run `apimark --help` for all options. Run `apimark dotnet --help` for .NET-specific options.

### MSBuild Usage

Documentation is generated automatically after `dotnet build`. Output goes to
`$(MSBuildProjectDirectory)\api` by default. Configure with MSBuild properties:

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

## Building

```pwsh
pwsh ./build.ps1
```

## User Guide

The ApiMark User Guide is available on the [ApiMark releases page](https://github.com/DemaConsulting/ApiMark/releases).

## Contributing

See [CONTRIBUTING.md](https://github.com/DemaConsulting/ApiMark/blob/main/CONTRIBUTING.md) for guidelines.

## License

This project is licensed under the MIT License — see [LICENSE](https://github.com/DemaConsulting/ApiMark/blob/main/LICENSE).

## Support

- [Report a bug or request a feature](https://github.com/DemaConsulting/ApiMark/issues)
- [Ask a question or start a discussion](https://github.com/DemaConsulting/ApiMark/discussions)
