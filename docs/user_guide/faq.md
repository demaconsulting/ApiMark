# Frequently Asked Questions

## C# and .NET

### My .NET API documentation is empty or missing

Ensure XML documentation generation is enabled in your `.csproj`:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

Without this, the compiler does not emit the XML doc file and ApiMark has no
documentation to read. The assembly is still documented structurally (types,
signatures) but all doc-comment content will be absent.

### How do I include the generated docs in my NuGet package?

Set `ApiMarkPackDocs` to `true`:

```xml
<PropertyGroup>
  <ApiMarkPackDocs>true</ApiMarkPackDocs>
</PropertyGroup>
```

The `api/` folder is included in the `.nupkg` at `api/`. This works with both
`dotnet pack` and `dotnet pack --no-build` provided the docs already exist on disk.

### How do I disable ApiMark for test projects or internal assemblies?

```xml
<PropertyGroup>
  <DisableApiMark>true</DisableApiMark>
</PropertyGroup>
```

Set this in any project where documentation generation is not wanted — test
projects, benchmark projects, or internal tooling.

### Internal and protected members are showing up in my output

The default visibility is `Public`. If internal or protected members are appearing,
check whether `ApiMarkVisibility` has been set explicitly upstream (e.g. in a
`Directory.Build.props`). Set it explicitly in the affected project:

```xml
<PropertyGroup>
  <ApiMarkVisibility>Public</ApiMarkVisibility>
</PropertyGroup>
```

---

## C++

### My C++ output is empty or clang cannot be found

ApiMark locates clang using this priority order:

1. `--clang-path` CLI option or `ApiMarkClangPath` MSBuild property
2. `APIMARK_CLANG_PATH` environment variable
3. `clang` on the system `PATH`
4. `xcrun clang` (macOS)
5. vswhere / `C:\Program Files\LLVM\bin\clang.exe` (Windows)

If none of these resolve, set `APIMARK_CLANG_PATH` in your CI environment or shell
profile, or pass `--clang-path` explicitly.

### No headers are being documented

When `--api-headers` is not specified, ApiMark documents all headers with
recognized extensions (`.h`, `.hpp`, `.hxx`, `.h++`) under the `--includes`
directories. If output is empty, confirm that `--includes` points to the directory
that actually contains the headers and that the headers use a recognized extension.

### How do I exclude third-party or generated headers from my docs?

Use `--api-headers` with gitignore-style exclusion patterns:

```text
apimark cpp \
  --includes include/ \
  --api-headers "include/**" \
  --api-headers "!include/third_party/**"
```

Only headers matching the final positive pattern are documented.

### My project has complex include paths or multiple build targets

For projects with unusual include structures, generated headers, or multiple build
configurations, the MSBuild auto-detection of `AdditionalIncludeDirectories` may
not produce the right set of paths. In these cases, run `apimark cpp` as a
dedicated documentation step rather than relying on the MSBuild plugin:

```bash
apimark cpp \
  --includes src/mylib/include \
  --includes build/generated/include \
  --api-headers "mylib/**" \
  --output docs/api
```

For multi-target projects (e.g. a library that ships as both a static and shared
build), run the documentation step once against the canonical include tree and
share the output across all packages rather than regenerating per target. In CI
this is typically a separate job that runs before packaging.

---

## MSBuild and CI

### How do I run ApiMark in CI without rebuilding?

Use the CLI tool directly in a separate CI step:

```bash
# C#
apimark dotnet --assembly bin/Release/net8.0/MyLib.dll --xml-doc bin/Release/net8.0/MyLib.xml --output docs/api

# C++
apimark cpp --includes include/ --output docs/api
```

This avoids triggering a full build and gives precise control over inputs and
outputs, which is useful when documentation is published independently of the
build artifact.

### How do I run self-validation in CI?

```bash
apimark --validate --results results/apimark.trx
```

The `--results` flag writes a `.trx` or `.xml` results file compatible with most
CI test reporters. The exit code is non-zero if any validation test fails.

### ApiMark is slowing down my build

Set `DisableApiMark=true` on projects where documentation is not needed (test
projects, benchmarks). For C++ projects with many headers, use `--api-headers`
patterns to limit which files are parsed by clang — undocumented headers still
contribute to compilation but are not analyzed for documentation.

---

## VHDL

### How do I specify which .vhd files to document?

Use `--source` with a glob pattern:

```bash
apimark vhdl --source "src/**/*.vhd" --output docs/api
```

Multiple `--source` patterns are evaluated in order using gitignore-style
last-match-wins semantics. A pattern prefixed with `!` excludes matching files.

### How do I exclude testbenches or simulation-only files?

Use an exclusion pattern after the inclusion pattern:

```bash
apimark vhdl \
  --source "src/**/*.vhd" \
  --source "!src/tb/**/*.vhd" \
  --output docs/api
```

Files matching the exclusion pattern (`!src/tb/**/*.vhd`) are removed from
the set even if they matched an earlier inclusion pattern.

### What VHDL constructs are documented?

ApiMark documents the following VHDL constructs when they carry `--!` doc comments:

- **Entities** — entity name, generic parameters, port declarations
- **Architectures** — architecture name and doc comment (listed inline on the entity page)
- **Packages** — package name, types, constants, components, and subprograms

`--!` doc comments are single-line comments prefixed with `--!` placed
immediately before the construct they describe.

### Does VHDL support require any additional tools?

No. VHDL parsing is done in-process using the ANTLR4 vhdl2008 grammar —
no external tools or runtimes are required beyond the .NET SDK.
