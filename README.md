# ApiMark

<!-- IMPORTANT: All links in this file must be absolute URLs.
     This file is distributed in packages and relative links will not resolve. -->

[![GitHub forks](https://img.shields.io/github/forks/demaconsulting/ApiMark?style=plastic)](https://github.com/demaconsulting/ApiMark/network/members)
[![GitHub stars](https://img.shields.io/github/stars/demaconsulting/ApiMark?style=plastic)](https://github.com/demaconsulting/ApiMark/stargazers)
[![GitHub contributors](https://img.shields.io/github/contributors/demaconsulting/ApiMark?style=plastic)](https://github.com/demaconsulting/ApiMark/graphs/contributors)
[![License](https://img.shields.io/github/license/demaconsulting/ApiMark?style=plastic)](https://github.com/demaconsulting/ApiMark/blob/main/LICENSE)
[![Build](https://img.shields.io/github/actions/workflow/status/demaconsulting/ApiMark/build_on_push.yaml)](https://github.com/demaconsulting/ApiMark/actions/workflows/build_on_push.yaml)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_ApiMark&metric=alert_status)](https://sonarcloud.io/dashboard?id=demaconsulting_ApiMark)
[![Security](https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_ApiMark&metric=security_rating)](https://sonarcloud.io/dashboard?id=demaconsulting_ApiMark)
[![NuGet](https://img.shields.io/nuget/v/DemaConsulting.ApiMark.MSBuild?style=plastic)](https://www.nuget.org/packages/DemaConsulting.ApiMark.MSBuild)

## Overview

ApiMark generates compact, AI-friendly API reference documentation in Markdown
from source code and associated metadata (XML doc comments, header files,
docstrings, etc.). The output is designed for gradual disclosure: an AI can
read a lightweight index, drill into a namespace summary, and then read a full
type page — consuming only as much context as the task requires.

## Features

- 📄 **Compact Markdown Output** - AI-friendly API reference from source code
- 🔍 **Gradual Disclosure** - Index → namespace → type → member detail
- 🔷 **C#/.NET Support** - Mono.Cecil + XML documentation comments
- ➕ **C++ Support** - `clang -ast-dump=json` + Doxygen-style comments
- 🔧 **MSBuild Integration** - Auto-documents `.csproj` and `.vcxproj` builds
- 🖥️ **CLI Tool** - `apimark` dotnet tool covering all languages
- 🤖 **AI-Optimized** - Minimal noise, explicit navigation links
- 🌐 **Multi-Platform** - Windows, Linux, and macOS on .NET 8, 9, and 10
- ✅ **Self-Validation** - Built-in qualification tests for regulated environments

## Platform Support

| Platform | .NET | C++ |
| --- | --- | --- |
| Windows | ✅ | ✅ |
| Linux | ✅ | ✅ |
| macOS | ✅ | ✅ |

## Prerequisites

### C++ Support

C++ documentation generation requires `clang` to be installed and available:

- **Windows**: Install [LLVM](https://releases.llvm.org/) or the "C++ Clang tools for Windows"
  component via the Visual Studio Installer. The `ClangPath` MSBuild property or `--clang-path`
  CLI option can point to a specific installation.
- **macOS**: Xcode Command Line Tools (`xcode-select --install`) — `clang` is included.
- **Linux**: Install via the system package manager (e.g. `apt install clang` or `dnf install clang`).

.NET support has no additional prerequisites beyond the .NET SDK.

## Installation

### CLI Tool

```bash
dotnet tool install --global DemaConsulting.ApiMark.Tool
```

### MSBuild Integration

**C# projects** — add the NuGet package to your `.csproj`:

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

**C++ projects** — add the NuGet package to your `.vcxproj`:

```xml
<ItemGroup>
  <PackageReference Include="DemaConsulting.ApiMark.MSBuild" Version="x.y.z" />
</ItemGroup>
```

Set the public include root so ApiMark knows which headers to document:

```xml
<PropertyGroup>
  <ApiMarkIncludePaths>$(MSBuildProjectDirectory)\include</ApiMarkIncludePaths>
</PropertyGroup>
```

ApiMark generates documentation automatically after every build.

## Usage

### CLI Usage

```bash
# Generate API documentation from a .NET assembly
apimark dotnet --assembly MyProject.dll --xml-doc MyProject.xml --output docs/api

# Generate API documentation from C++ public headers
apimark cpp --includes include/ --output docs/api
```

Run `apimark --help` for all options. Run `apimark dotnet --help` or `apimark cpp --help` for language-specific options.

### MSBuild Usage

Documentation is generated automatically after every build. Output goes to
`$(MSBuildProjectDirectory)\api` by default. Configure with MSBuild properties:

```xml
<PropertyGroup>
  <!-- Change the output directory -->
  <ApiMarkOutputDir>$(MSBuildProjectDirectory)\docs\api</ApiMarkOutputDir>

  <!-- Include protected members as well as public ones -->
  <ApiMarkVisibility>PublicAndProtected</ApiMarkVisibility>

  <!-- Include the generated api/ folder in the NuGet package (C# only) -->
  <ApiMarkPackDocs>true</ApiMarkPackDocs>

  <!-- Disable generation entirely (e.g., for test projects) -->
  <DisableApiMark>true</DisableApiMark>
</PropertyGroup>
```

See the [User Guide](https://github.com/DemaConsulting/ApiMark/releases) for the full list of properties including C++-specific options.

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
