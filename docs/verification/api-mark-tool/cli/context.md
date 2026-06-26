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
- `--log <file>` also captures `WriteError` output.
- `--depth <n>` sets `HeadingDepth` to `n`; values outside 1–6 or non-integers throw `ArgumentException`.
- When `--format single-file` is in effect, `--depth` values above 3 throw `ArgumentException` regardless of argument order.
- `--depth 3` with `--format single-file` is accepted (boundary value).
- `--results`/`--result` sets `ResultsFile` to the supplied path.
- Flag tokens (starting with `-`) supplied as values for string-valued options are rejected with `ArgumentException`.
- `--includes` accepts one directory path per flag; repeated flags accumulate paths into `Includes`.
- `--api-headers` patterns are accumulated in order; `!`-prefixed exclusion patterns are forwarded verbatim.
- `--source` patterns are accumulated in order; `!`-prefixed exclusion patterns are forwarded verbatim.
- C++ named options (`--library-name`, `--library-description`, `--defines`, `--cpp-standard`) set their
  corresponding properties.
- `--clang-path` sets `ClangPath` to the supplied path.
- `--format` accepts `gradual` and `single-file`; defaults to `GradualDisclosure`; invalid values throw
  `ArgumentException`.

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

**`Context_Create_WithIncludesOption_SetsIncludes`**: `--includes path/a` →
`Includes = ["path/a"]` (one path per flag; no comma splitting).

**`Context_Create_WithRepeatedIncludes_AccumulatesAllPaths`**:
`--includes /usr/include --includes /opt/include` → `Includes = ["/usr/include", "/opt/include"]`.

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

**`Context_Create_WithFormatGradual_SetsGradualDisclosureFormat`**: `--format gradual` →
`Format = OutputFormat.GradualDisclosure`.

**`Context_Create_WithFormatSingleFile_SetsSingleFileFormat`**: `--format single-file` →
`Format = OutputFormat.SingleFile`.

**`Context_Create_WithNoFormatOption_DefaultsToGradualDisclosure`**: No `--format` argument →
`Format = OutputFormat.GradualDisclosure` (default).

**`Context_Create_WithInvalidFormat_ThrowsArgumentException`**: `--format unknown-value` →
`ArgumentException` thrown.

**`Context_Create_WithRepeatedApiHeaders_AccumulatesAllPatternsInOrder`**:
`--api-headers "**/*" --api-headers "!**/detail/**" --api-headers "**/detail/public_api.h"` →
`ApiHeaders = ["**/*", "!**/detail/**", "**/detail/public_api.h"]`.

**`Context_Create_WithApiHeadersExclusionPattern_ForwardsVerbatim`**:
`--api-headers "!**/internal/**"` → `ApiHeaders = ["!**/internal/**"]`
(the `!` prefix is preserved verbatim so `CppGenerator` can apply gitignore semantics).

**`Context_Create_WithSingleInclude_SetsSinglePath`**: `--includes /usr/include` →
`Includes = ["/usr/include"]`, `ApiHeaders` is empty.

**`Context_Create_WithNoArguments_HasEmptyApiHeaders`**: Empty args →
`ApiHeaders` defaults to an empty array.

**`Context_Create_WithLibraryNameOption_SetsLibraryName`**: `--library-name MyAwesomeLib` →
`LibraryName = "MyAwesomeLib"`.

**`Context_Create_WithLibraryDescriptionOption_SetsLibraryDescription`**:
`--library-description "A fast geometry library."` → `LibraryDescription = "A fast geometry library."`.

**`Context_Create_WithDefinesOption_SetsDefines`**: `--defines MYLIB_API=,NDEBUG` →
`Defines = ["MYLIB_API=", "NDEBUG"]` (comma-separated value split into individual entries).

**`Context_Create_WithCppStandardOption_SetsCppStandard`**: `--cpp-standard c++20` →
`CppStandard = "c++20"`.

**`Context_Create_WithClangPathOption_SetsClangPath`**: `--clang-path /usr/bin/clang` →
`ClangPath = "/usr/bin/clang"`.

**`Context_Create_WithSourceOption_SetsSources`**: `--source "src/**/*.vhd"` →
`Sources = ["src/**/*.vhd"]`.

**`Context_Create_WithRepeatedSource_AccumulatesAllPaths`**:
`--source "src/**/*.vhd" --source "src/**/*.vhdl"` → `Sources = ["src/**/*.vhd", "src/**/*.vhdl"]`.

**`Context_Create_WithSourceExclusionPattern_ForwardsVerbatim`**:
`--source "src/**/*.vhd" --source "!src/tb/**/*.vhd"` →
`Sources = ["src/**/*.vhd", "!src/tb/**/*.vhd"]`
(the `!` prefix is preserved verbatim so `VhdlGenerator` can apply gitignore semantics).

**`Context_OpenLogFile_ErrorOutputAlsoWrittenToLog`**: `--log <tempPath>` + `WriteError`
→ file exists and contains the error message after `Dispose`.

**`Context_Create_WithDepthAbove3AndSingleFileFormat_ThrowsArgumentException`**:
`--format single-file --depth 4` and `--depth 4 --format single-file` both throw
`ArgumentException` (theory test covering both argument orderings).

**`Context_Create_WithDepth3AndSingleFileFormat_Succeeds`**: `--format single-file --depth 3`
parses successfully with `HeadingDepth = 3` and `Format = OutputFormat.SingleFile`
(boundary value is accepted).

**`Context_Create_WithFlagValueForOutput_ThrowsArgumentException`**: `["dotnet", "--output", "--silent"]`
— a flag token supplied as the `--output` value — throws `ArgumentException`, confirming
that the parser rejects flag tokens as string-valued option values.

**`Context_Create_WithFlagValueForLog_ThrowsArgumentException`**: `["--log", "--silent"]`
— a flag token supplied as the `--log` value — throws `ArgumentException`, confirming
that the parser rejects flag tokens as string-valued option values.
