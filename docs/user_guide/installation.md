# Installation

## Prerequisites

The ApiMark CLI tool and MSBuild package require the .NET SDK (version 8.0 or later).

C++ documentation generation additionally requires `clang` to be installed:

- **Windows**: Install [LLVM](https://releases.llvm.org/) or add "C++ Clang tools for Windows"
  via the Visual Studio Installer.
- **macOS**: Install Xcode Command Line Tools: `xcode-select --install`
- **Linux**: Install via your package manager, e.g. `sudo apt install clang` or
  `sudo dnf install clang`.

If `clang` is not on your PATH, use the `--clang-path` CLI option or `ApiMarkClangPath` MSBuild
property to specify the full path to the clang executable.

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

Add the `DemaConsulting.ApiMark.MSBuild` NuGet package to any `.csproj` project to
automatically generate API documentation after every build.

In your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="DemaConsulting.ApiMark.MSBuild" Version="x.y.z" />
</ItemGroup>
```

Enable XML documentation generation so ApiMark can read your doc comments:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

After the next `dotnet build`, documentation is written to `$(MSBuildProjectDirectory)\api`.
