# Installation

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
