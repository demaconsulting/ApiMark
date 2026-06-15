# Installation

## Prerequisites

The ApiMark CLI tool and MSBuild package require the .NET SDK (version 8.0 or later).

C++ documentation generation additionally requires `clang` to be installed. See the
*C++ Documentation* section for platform-specific installation instructions and clang
executable discovery details.

VHDL documentation generation has no additional prerequisites — parsing is done
in-process using the ANTLR4 vhdl2008 grammar.

## CLI Tool

Install the ApiMark CLI as a global .NET tool:

```bash
dotnet tool install --global DemaConsulting.ApiMark.Tool
```

Verify the installation:

```bash
apimark --version
```

## MSBuild Package

Add the `DemaConsulting.ApiMark.MSBuild` NuGet package to any supported project to
automatically generate API documentation after every build:

```xml
<ItemGroup>
  <PackageReference Include="DemaConsulting.ApiMark.MSBuild" Version="x.y.z" />
</ItemGroup>
```

See the *.NET Documentation* section for C#-specific setup steps and the
*C++ Documentation* section for C++-specific setup steps.
