# CLI Reference

## Synopsis

```text
Usage: apimark [options] [language [language-options]]
```

## Global Options

| Option | Description |
| --- | --- |
| `-v`, `--version` | Display version information |
| `-?`, `-h`, `--help` | Display the help message |
| `--silent` | Suppress console output |
| `--validate` | Run self-validation tests |
| `--results <file>` | Write validation results to file (`.trx` or `.xml`) |
| `--depth <#>` | Set the top-level heading depth for generated Markdown output (default: `1`; see below) |
| `--log <file>` | Write all output to log file |

The `--depth` value is restricted to `1`–`3` when `--format single-file` is used.

## Languages

| Subcommand | Description |
| --- | --- |
| `dotnet` | Generate API documentation from a .NET assembly |
| `cpp` | Generate API documentation from C++ headers using Clang |
| `vhdl` | Generate API documentation from VHDL source files |

See the *.NET Documentation*, *C++ Documentation*, and *VHDL Documentation* sections for
the full option reference and usage details for each language subcommand.

## Self-Validation

The `--validate` flag runs ApiMark's built-in self-validation suite without generating
any API documentation for your project. Use this in CI to verify that the installed tool
is functioning correctly: the results file is evidence that the pinned tool does its job
on that machine, not only that it starts. Each test runs the tool end to end, in-process,
on small sample sources it writes into its own temporary folder (removed afterwards), and
passes only when the exit code, the reported findings, and the generated Markdown are
exactly as expected.

- `ApiMark_VersionDisplay` - `--version` prints exactly the tool version.
- `ApiMark_HelpDisplay` - `--help` prints the usage and the options of every subcommand.
- `ApiMark_DotNetGeneration` - `dotnet --format single-file` documents a sample assembly's types, members,
  signatures, and descriptions in one `api.md`.
- `ApiMark_GradualDisclosureOutput` - `dotnet` writes the expected index, namespace, type, and member pages, and
  every link between them resolves.
- `ApiMark_CppGeneration` - `cpp` documents a sample header's struct and field.
- `ApiMark_VhdlGeneration` - `vhdl` documents a sample entity's description, generics, and ports.
- `ApiMark_DotNetEnforceDocs` - `--enforce-docs` reports exactly a missing .NET doc comment and fails the run only
  at `Error` severity.
- `ApiMark_CppEnforceDocs` - `--enforce-docs` reports exactly an undocumented C++ field and fails the run.
- `ApiMark_VhdlEnforceDocs` - `--enforce-docs` reports exactly an undocumented VHDL port and fails the run.

The .NET sample assembly is built in memory by the tool itself, so no SDK or compiler is
needed. The two C++ tests need clang, which is not part of the tool package; they are
automatically skipped (not failed) when no clang installation can be found — install
LLVM clang or set `APIMARK_CLANG_PATH` to include them in validation.

```bash
apimark --validate
```

Combine with `--results` to write a structured results file for CI test reporters:

```bash
apimark --validate --results results/apimark.trx
```

The `--results` option accepts either a `.trx` file path (Visual Studio Test Results
format) or an `.xml` file path (JUnit-compatible XML). The summary output includes
`Total Tests`, `Passed`, `Skipped`, and `Failed` counts. The exit code is non-zero if
any validation test fails; skipped tests (such as the C++ check when clang is
unavailable) never affect the exit code.

## Platform Support

| Platform | `dotnet` | `cpp` | `vhdl` |
| --- | --- | --- | --- |
| Windows x64 | ✅ | ✅ | ✅ |
| Linux x64 | ✅ | ✅ | ✅ |
| macOS | ✅ | ✅ | ✅ |
