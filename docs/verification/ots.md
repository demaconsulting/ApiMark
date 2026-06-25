# OTS Verification

## Verification Strategy

ApiMark verifies OTS software items by testing the exact externally supplied behavior that the
repository depends on rather than attempting to re-validate the full third-party product. For the
current scope, Mono.Cecil is exercised through ApiMark.DotNet integration tests that open fixture
assemblies, enumerate metadata, and feed that metadata into Markdown generation.
`Microsoft.Extensions.FileSystemGlobbing` is exercised through `GlobFileCollector` unit tests
that match patterns against real temporary directories. The ANTLR4 runtime
(`Antlr4.Runtime.Standard`) is exercised indirectly by every `VhdlAstParser` test.
Local evidence is preferred because ApiMark depends on a specific subset of each OTS library's
features that must remain stable across package upgrades.

## Qualification Evidence

Qualification evidence consists of passing automated integration tests, expected-output comparisons,
and focused scenario coverage for the metadata features ApiMark consumes: namespace and type
discovery, member signatures, accessibility, generics, nullable types, and relevant attributes such
as obsolete markers. Package upgrade review also includes inspection of upstream release notes for
breaking changes that could affect metadata interpretation.

## Regression Approach

Whenever the Mono.Cecil package version changes, the repository re-runs all DotNet generator and OTS
verification scenarios against the same fixture assemblies used for baseline qualification. Any
change in discovered members, rendered signatures, or generated file layout is treated as a
regression candidate and must be reviewed before the upgrade is accepted.

## Mono.Cecil

Mono.Cecil is used by `ApiMark.DotNet` as an OTS component for .NET assembly reflection.
It is not modified; ApiMark reads assembly metadata, type and member definitions, generic
parameters, nullable annotations, and custom attributes through Mono.Cecil's public API
without altering the component in any way.

**Verification approach**: ApiMark exercises the subset of Mono.Cecil's API surface that it
depends on through its existing integration tests. `DotNetGenerator` tests open the fixture
assembly via `AssemblyDefinition.ReadAssembly`, enumerate types and members, inspect
accessibility modifiers and custom attributes, and feed the resulting metadata into Markdown
generation. These tests collectively verify that Mono.Cecil correctly discovers namespace and
type structure, member signatures, generic parameters, nullable annotations, obsolete markers,
and inheritance chains for the pattern of assemblies ApiMark is expected to document.

**Qualification evidence**: The Mono.Cecil package version is pinned in the project's NuGet
references. ApiMark uses only the public Mono.Cecil API. All `ApiMark.DotNet` tests must pass
with each version of Mono.Cecil to confirm continued compatibility.

**Regression criteria**: All `ApiMark.DotNet` tests pass with each Mono.Cecil version update.
Any change in discovered members, rendered signatures, or generated file layout relative to
the fixture assembly baseline is treated as a regression candidate and must be reviewed before
the upgrade is accepted.

## clang

ApiMark verifies the clang integration by testing the exact externally supplied behavior that
ApiMark.Cpp depends on. The clang executable is exercised through integration tests in
`test/ApiMark.Cpp.Tests/` that invoke `clang -Xclang -ast-dump=json` on fixture C++ headers,
parse the resulting JSON AST, and feed the structured data into Markdown generation. Local
evidence is preferred because ApiMark depends on a specific subset of clang output: the JSON AST
structure for namespaces, classes, enums, functions, fields, and Doxygen doc comment nodes.

Qualification evidence consists of passing automated integration tests and focused scenario coverage
for the metadata features consumed: header parsing via clang, per-declaration source file location
from the JSON `loc.file` field, class and enum enumeration, function signatures including variadic
parameters, doc comment trees (`FullComment`, `ParagraphComment`, `ParamCommandComment`,
`BlockCommandComment`), access specifiers, and clang option forwarding.

Whenever the minimum supported clang version changes, the repository re-runs all CppGenerator
integration tests against the same fixture headers used for baseline qualification. Any change in
discovered types, rendered signatures, doc comment availability, or generated file layout is treated
as a regression candidate and must be reviewed before the version change is accepted.

## ANTLR4

The ANTLR4 runtime (`Antlr4.Runtime.Standard`) is verified indirectly through `VhdlAstParser` unit
tests in `test/ApiMark.Vhdl.Tests/`. Because ANTLR4 was used once to generate committed C# source
files, there is no ongoing tool dependency to verify. The runtime is exercised by every test that
invokes the parser. See `docs/verification/ots/antlr4.md` for detailed test scenarios.

## Microsoft.Extensions.FileSystemGlobbing

`Microsoft.Extensions.FileSystemGlobbing` is verified through `GlobFileCollector` unit tests in
`test/ApiMark.Core.Tests/GlobFileCollectorTests.cs`. These tests operate against real temporary
directories and confirm that the `Matcher` API correctly handles include patterns, exclude patterns,
and non-existent roots. See `docs/verification/ots/file-system-globbing.md` for detailed test
scenarios.

## DemaConsulting.TestResults

`DemaConsulting.TestResults` is verified through the self-validation tests in
`test/ApiMark.Tool.Tests/SelfTest/ValidationTests.cs`. These tests exercise the
pass/fail outcome recording and results-file serialization paths of `Validation.cs`,
which is the sole consumer of `DemaConsulting.TestResults` in the repository. The
verification focus is the subset of capabilities the product depends on: creating a
`TestResults` collection, recording `Passed` and `Failed` outcomes on individual
`TestResult` objects, and serializing the collection to TRX and JUnit XML via
`TrxSerializer` and `JUnitSerializer`. See
`docs/verification/ots/dema-consulting-test-results.md` for detailed test scenarios.

## cpp-ast-net (Archived)

The `cpp-ast-net` OTS item is archived and retained for historical reference only. It was
superseded by direct `clang -ast-dump=json` invocation (see the *clang* section above for the
current approach). No active verification scenarios exist for this item; the design, requirements,
and verification documents (`docs/design/ots/cpp-ast-net.md`, `docs/reqstream/ots/cpp-ast-net.yaml`,
`docs/verification/ots/cpp-ast-net.md`) are preserved to document the historical integration
decision.
