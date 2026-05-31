### Context

#### Verification Approach

`Context` is verified through unit tests in
`test/ApiMark.Tool.Tests/Cli/ContextTests.cs` that call `Context.Create(string[] args)`
with a specific argument array and assert on exactly one property or behavior per test.
No mocking or test doubles are used; tests rely on the real `Context` implementation
with `InternalsVisibleTo` access.

#### Test Environment

Standard .NET test runner. The log-file test writes to `Path.GetTempPath()` and cleans
up after itself. No other external files, services, or configuration are required.

#### Acceptance Criteria

- All `ContextTests` tests pass with zero failures.
- Each flag and option sets exactly the documented property.
- Default values match the documented defaults when no argument is supplied.
- Unknown flags produce `ArgumentException`.
- `WriteError` unconditionally sets `ExitCode` to `1` regardless of the `Silent` flag.
- `--log <file>` creates the file and captures `WriteLine` output.
- `--depth <n>` sets `HeadingDepth` to `n`; values outside 1–6 or non-integers throw `ArgumentException`.
- `--results`/`--result` sets `ResultsFile` to the supplied path.
- `--includes` splits on commas and sets `Includes` to the resulting array.

#### Test Scenarios

**`Context_Create_WithVersionFlag_SetsVersionTrue`**: `--version` → `Version = true`.

**`Context_Create_WithShortVersionFlag_SetsVersionTrue`**: `-v` → `Version = true`.

**`Context_Create_WithHelpFlag_SetsHelpTrue`**: `--help` → `Help = true`.

**`Context_Create_WithHelpShortFlags_SetHelpTrue`**: `-?` and `-h` → `Help = true`
(theory test covering both variants).

**`Context_Create_WithSilentFlag_SetsSilentTrue`**: `--silent` → `Silent = true`.

**`Context_Create_WithValidateFlag_SetsValidateTrue`**: `--validate` → `Validate = true`.

**`Context_Create_WithLanguageSubcommand_SetsLanguage`**: `"dotnet"` → `Language = "dotnet"`.

**`Context_Create_WithAssemblyOption_SetsAssemblyPath`**: `--assembly my.dll` →
`Assembly = "my.dll"`.

**`Context_Create_WithXmlDocOption_SetsXmlDocPath`**: `--xml-doc my.xml` →
`XmlDoc = "my.xml"`.

**`Context_Create_WithOutputOption_SetsOutputPath`**: `--output out/dir` →
`Output = "out/dir"`.

**`Context_Create_WithVisibilityOption_SetsVisibility`**: `--visibility PublicAndProtected`
→ `Visibility = "PublicAndProtected"`.

**`Context_Create_WithIncludeObsoleteFlag_SetsIncludeObsoleteTrue`**: `--include-obsolete`
→ `IncludeObsolete = true`.

**`Context_Create_WithIncludesOption_SetsIncludes`**: `--includes path/a,path/b` →
`Includes = ["path/a", "path/b"]`.

**`Context_Create_WithDepthOption_SetsHeadingDepth`**: `--depth 3` → `HeadingDepth = 3`.

**`Context_Create_WithDepthOptionOutOfRange_ThrowsArgumentException`**: `--depth 0`, `--depth 7`,
and `--depth abc` each throw `ArgumentException` (theory test covering all three variants).

**`Context_Create_WithResultsOption_SetsResultsFile`**: `--results results.trx` and
`--result results.trx` both set `ResultsFile = "results.trx"` (theory test covering both variants).

**`Context_Create_WithNoArguments_HasDefaultValues`**: Empty args → all properties at
documented defaults; `ExitCode = 0`, `HeadingDepth = 1`, `Includes` empty.

**`Context_Create_WithUnknownFlag_ThrowsArgumentException`**: `--not-a-flag` →
`ArgumentException` thrown.

**`Context_WriteError_SetsExitCodeToOne`**: `WriteError("oops")` → `ExitCode = 1`.

**`Context_Create_WithLogFile_OpensAndWritesToLog`**: `--log <tempPath>` + `WriteLine`

- `Dispose` → file exists and contains the written line.

**`Context_Cli_ParsesAllGlobalFlags`**: All global flags in one array → all
corresponding properties set simultaneously.
