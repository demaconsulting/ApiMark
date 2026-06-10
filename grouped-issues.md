# Grouped Issues for Discussion

This file consolidates the 80 findings from `review-findings.md` into 15 logical groups,
ordered from highest impact to lowest. Each group is a single discussion item with
source findings listed so remediation can be tracked back to the review.

---

## GRP-01 — Verification introduction is stale on C++ scope

**Impact**: Resolves ~15 findings across all six systems  
**Severity**: High  
**Status**: ✅ Done
It explicitly puts C++ in an "Out of scope" clause:
*"Planned future language implementations outside the current .NET/MSBuild scope."*

Meanwhile, the design introduction, requirements, README, and the actual code all treat C++ as
fully in scope and delivered. This single stale document creates contradictions in every system
review. The same file also omits `IContext` and `PathHelpers` from the Core scope, and omits
`clang` from the OTS section.

**Specific gaps to fill:**

1. Remove the stale "Planned future language implementations" out-of-scope clause
2. Add ApiMarkCpp and CppGenerator to the Scope section
3. Add `clang` to the OTS scope entry
4. Add `IContext` and `PathHelpers` to the ApiMarkCore scope entry
5. Add all ApiMarkCpp companion artifact paths to the Companion Artifact Structure
6. Add `src/ApiMark.Cpp/`, `test/ApiMark.Cpp.Tests/`, `test/ApiMark.Cpp.Fixtures/` to Source/Test
   lists

**Source findings**: P-F01, P-F05, P-F07, P-F08, C-A03, C-A04, DN-A03, X-A03, X-A04,
M-A04, T-A03, T-A04, T-A05, RS1-03 (Cpp), RS1-04 (Cpp)

---

## GRP-02 — Broken ReqStream decomposition chains

**Impact**: ReqStream cannot validate traceability for orphaned requirements  
**Severity**: High  
**Status**: ✅ Done

Four `reqstream` YAML files have unit requirements that are not listed as children of their parent
system requirement. ReqStream uses this parent-child linkage to validate the decomposition chain.
Orphaned requirements cannot be traced back to an approved system-level need.

| File | Orphaned requirements |
| :--- | :-------------------- |
| `docs/reqstream/api-mark-core.yaml` | `IMarkdownWriterFactory-RejectInvalidOutputDirectory`, `IMarkdownWriterFactory-EnsureOutputDirectory` (2 of 4 children missing) |
| `docs/reqstream/api-mark-cpp.yaml` | 12 of 17 unit requirements unlisted (see full list in X-A01) |
| `docs/reqstream/api-mark-dot-net.yaml` | `EmitCombinedMemberPageForCaseInsensitiveCollisions`, `ApiMdListsAllNamespacesWithTypeCount`, `ShowDirectInheritanceInTypeSignature`, `EmitIntraDocLinksInTableCells`, `EmitExternalTypesSection` (5 of N missing) |
| `docs/reqstream/api-mark-tool/cli.yaml` | `ParseIncludes`, `ParseApiHeaders` defined in `context.yaml` but not listed in `cli.yaml` children |

**Source findings**: C-A01, C-R01, X-A01, X-R01, DN-A01, DN-R01, T-R01

---

## GRP-03 — Missing requirements for implemented features

**Impact**: Designed, implemented, and tested features with no backing requirement (design-first violation)  
**Severity**: High  
**Status**: ✅ Done — 4 MSBuild requirements added; 1 Tool parent + 4 Tool children added. Note: several tests referenced do not yet exist (`ApiMarkTask_Cpp_LibraryDescription_ForwardedToTool`, `ApiMarkTask_Cpp_ClangPath_ForwardedToTool`, `ApiMarkTask_DotNet_EmptyXmlDocPath_SkipsExecution`, and the 4 Context option tests).

Several features are fully built and tested but have no requirement. This violates the
unidirectional flow principle (requirements → design → implementation → verification).

**MSBuild** — four missing requirements:

1. `ApiMarkTask.ApiMarkLibraryDescription` → forwards `--library-description` to the tool
2. `ApiMarkTask.ApiMarkClangPath` → forwards `--clang-path` to the tool
3. Auto-population of `ApiMarkIncludePaths` from `AdditionalIncludeDirectories` on `ClCompile`
   items (the feature we just implemented!)
4. Skip behavior when DotNet `ApiMarkXmlDocPath` is not set (asymmetric counterpart to the
   existing `SkipWhenCppIncludePathsNotSet` requirement)

**Tool** — four missing requirements in Context unit:

1. Parsing `--library-name` CLI option
2. Parsing `--library-description` CLI option
3. Parsing `--defines` CLI option
4. Parsing `--cpp-standard` CLI option

**Source findings**: M-A01, M-A02, M-R01, M-D04, T-R02

---

## GRP-04 — PathHelpers security invariant incorrectly described

**Impact**: Factual error that misrepresents a security invariant  
**Severity**: High  
**Status**: ✅ Done

`docs/design/api-mark-core.md` External Interfaces — Internal Utilities states:

> "PathHelpers — Rejects null, rooted, or `..`-containing segments"

This is **factually wrong**. The actual rule (confirmed in `path-helpers.md` and the unit
requirements) is:

> Segments containing `..` or that are rooted are **accepted** when the combined result resolves
> within the base directory. Only combinations that escape the base are rejected.

The correct description should be something like:
*"Rejects combinations that resolve outside the base directory, and rejects null arguments."*

This matters because the incorrect wording implies a stricter rule than actually exists, causing
a reviewer to form a wrong mental model of what path operations are permitted.

**Source findings**: C-A02, C-D01, F1-02 (Core)

---

## GRP-05 — `IContext context` missing from unit-level `Generate` designs

**Impact**: Published interface undocumented at unit level; design inconsistency  
**Severity**: Medium  
**Status**: ✅ Done — `IContext context` added to Generate Key Method in both unit designs; `IMarkdownWriterFactory` and `IContext` added as consumed interfaces in DotNet system design.

The system design for both DotNet and Cpp documents the `Generate` method signature as:

```csharp
Generate(IMarkdownWriterFactory factory, IContext context)
```

But the unit designs for both systems omit `IContext context` from the Key Methods parameter table
entirely. Neither unit design describes how the generator uses IContext (emitting diagnostics,
forwarding to parser, etc.).

**Affected files:**

- `docs/design/api-mark-dot-net/dot-net-generator.md` — Key Methods, `Generate`
- `docs/design/api-mark-cpp/cpp-generator.md` — Key Methods, `Generate`

Note: The DotNet finding (DN-D01) also requires checking whether the system design or the unit
design has the correct signature — one of them may need correction rather than extension.

**Source findings**: DN-D01, X-D02, DN-A02

---

## GRP-06 — `--includes` three-way contradiction (Tool)

**Impact**: Observable system behavior is ambiguous — requirement, design, and test criterion all disagree  
**Severity**: Medium  
**Status**: ✅ Done — code confirmed repeated-flag behavior; fixed verification acceptance criterion to match.

Three documents describe the `--includes` option differently:

| Document | Claims |
| :------- | :----- |
| `docs/reqstream/api-mark-tool/cli/context.yaml` (`ParseIncludes` requirement) | "repeated --includes flags" accumulate paths |
| `docs/design/api-mark-tool/cli/context.md` | "each --includes flag appends a single directory path" |
| `docs/verification/api-mark-tool/cli/context.md` (Acceptance Criteria) | "`--includes` splits on commas and sets Includes to the resulting array" |

These are **mutually exclusive behaviors**: repeated flags vs. comma-splitting in a single flag.
The test `Context_Create_WithIncludesOption_SetsIncludes` is linked to both and cannot verify both
simultaneously. The actual implementation is the ground truth here.

**Source findings**: T-R03, T-V01, RS2-F3 (Tool)

---

## GRP-07 — Cpp design gaps: ClangAstParser and constructor/Generate validation

**Impact**: Unit design too incomplete for code review; factual contradiction  
**Severity**: Medium  
**Status**: ✅ Done — created `docs/design/api-mark-cpp/clang-ast-parser.md`; moved disk-existence check from constructor preconditions to Generate preconditions in `cpp-generator.md`; added ClangAstParser to design introduction tree and `ApiMark-Cpp-Generator` review set.

**Gap 1 — ClangAstParser and CppAstModel undocumented** (X-D01)

`ClangAstParser` is a distinct class in `src/ApiMark.Cpp/CppAst/` that invokes `clang -ast-dump=json`,
captures JSON output, and parses it into a structured `CppCompilationResult`. It is named in the
system design Data Flow but has no Key Method entry in `cpp-generator.md`, and the CppAstModel
types (`CppCompilationResult`, `CppNamespaceDecl`, `CppClass`, `CppFunction`, `CppField`,
`CppTypeAlias`) are absent from the Data Model section.

**Gap 2 — Constructor precondition vs. Generate-time exception** (X-A02, X-D03)

The constructor Key Method states: *"each entry must be an existing directory"* (precondition).
The Error Handling section states: *"throws `DirectoryNotFoundException`"* during Generate.
The test name `CppGenerator_Generate_NonexistentIncludeRoot_ThrowsDirectoryNotFoundException`
confirms validation is Generate-time, not constructor-time. Both the system design and unit design
repeat this contradiction.

**Source findings**: X-D01, X-A02, X-D03

---

## GRP-08 — MSBuild External Interfaces: incomplete C++ contract

**Impact**: Reader of system design cannot determine the full set of MSBuild properties or C++ process arguments  
**Severity**: High  
**Status**: ✅ Done — added all 6 missing C++ properties to task contract; updated C++ process contract with all arguments.

`docs/design/api-mark-msbuild.md` External Interfaces documents the C++ process contract as:

```text
dotnet <ToolDllPath> cpp --includes <paths> [--output <dir>] [--visibility <value>] [--include-obsolete]
```

Six arguments are missing: `--library-name`, `--library-description`, `--defines`,
`--cpp-standard`, `--api-headers`, `--clang-path`.

The corresponding MSBuild task contract property list also omits six properties:
`ApiMarkLibraryName`, `ApiMarkLibraryDescription`, `ApiMarkApiHeaders`, `ApiMarkDefines`,
`ApiMarkCppStandard`, `ApiMarkClangPath`.

**Source findings**: M-A03, M-D01

---

## GRP-09 — Cpp verification: 3 requirements with no scenarios

**Impact**: Reviewers cannot confirm test strategies for three implemented features  
**Severity**: High  
**Status**: ✅ Done — added test scenarios for `EmitIntraDocLinksInTableCells`, `ApplyGitignoreStylePatternSelectionToHeaders` (5 tests), and `EmitExternalTypesSection`; added corresponding acceptance criteria bullets.

Three implemented and tested requirements have no documentation in either the unit or system
verification document. A reviewer cannot confirm coverage without reading test source code.

| Requirement | Linked test(s) | Missing from |
| :---------- | :------------- | :----------- |
| `EmitIntraDocLinksInTableCells` | `CppGenerator_Generate_IntraLibraryReturnType_EmitsMarkdownLinkInReturnsCell` | Both unit and system verification |
| `ApplyGitignoreStylePatternSelectionToHeaders` | 5 tests (include, exclude, re-include patterns) | Both unit and system verification |
| `EmitExternalTypesSection` | `CppTypeLinkResolver_Linkify_UnknownNamespacedType_TracksExternalType` | Both unit and system verification |

No Acceptance Criteria entries cover these three features in either document.

**Source findings**: X-V01, X-V02, X-V03

---

## GRP-10 — MSBuild verification gaps

**Impact**: Unit and system verification documents inconsistent; requirement-linked tests undocumented  
**Severity**: Medium  
**Status**: ✅ Done — added `ApiMarkApiHeaders` criterion to unit doc; added `WithDotNetProject` scenario to both docs; aligned `WithCppProject` "not yet implemented" caveat in system doc; added `ApiHeaders_ForwardedAsIndividualFlags` scenario to unit doc.

Four issues in the MSBuild verification documents:

1. **Missing unit acceptance criterion** (M-V01): `api-mark-task.md` Acceptance Criteria omits
   the `ApiMarkApiHeaders` → individual `--api-headers` flags criterion. The criterion exists at
   system level but not unit level.

2. **Missing test scenario** (M-V02): `ApiMarkTask_Execute_WithDotNetProject_GeneratesDocumentation`
   is named in a requirement but has no scenario description in either verification document.

3. **Inconsistent test implementation status** (M-V03): Unit verification marks
   `ApiMarkTask_Execute_WithCppProject_GeneratesDocumentation` as "not yet implemented"; system
   verification presents it as a fully realized scenario.

4. **Unit-level scenario missing from unit doc** (M-V04): `ApiMarkTask_Cpp_ApiHeaders_ForwardedAsIndividualFlags`
   satisfies a unit-level requirement but its scenario description is only in the system
   verification document.

**Source findings**: M-V01, M-V02, M-V03, M-V04

---

## GRP-11 — Tool: Context design missing cpp-specific properties

**Impact**: Design documents incomplete; program.md and context.md are internally contradictory  
**Severity**: Medium  
**Status**: ✅ Done — added 5 missing properties (`LibraryName`, `LibraryDescription`, `Defines`, `CppStandard`, `ClangPath`) to `context.md` Data Model table; updated `cli.md` property list and recognized options list.

`program.md` Key Methods (CreateGenerator) reads four cpp-specific properties from the Context to
populate `CppGeneratorOptions`:

- `LibraryName`, `Description`, `Defines`, `CppStandard`

But `context.md` Data Model has no entries for these properties. The `cli.md` Design section also
omits `--library-name`, `--library-description`, `--defines`, `--cpp-standard` from its list of
recognized options — yet they appear in the External Interfaces of `api-mark-tool.md`.

These three documents are mutually contradictory about what the Context exposes.

**Source findings**: T-D01, T-D02, T-V01 (partly), RS3-F1, RS3-F2 (Tool)

---

## GRP-12 — Core verification gaps

**Impact**: Requirement-linked tests have no scenario documentation  
**Severity**: Medium  
**Status**: ✅ Done — added IContext criterion and scenario to system doc; added 6 DoesNotThrow scenarios to `i-markdown-writer.md`; added `HasCreateMarkdown_Method` scenario to `i-markdown-writer-factory.md`.

Three gaps in Core verification documents:

1. **IContext system-level scenario missing** (C-V01, C-A06): `api-mark-core.md` has no Test
   Scenario or Acceptance Criterion for the IContext contract, despite
   `ApiMarkCore-IContext-ProvideOutputChannel` referencing a system-scoped test.

2. **Six IMarkdownWriter tests undocumented** (C-V02): `IMarkdownWriter_WriteX_ValidArgs_DoesNotThrow`
   tests are requirement-linked but have no corresponding scenario descriptions.

3. **One IMarkdownWriterFactory test undocumented** (C-V03): `IMarkdownWriterFactory_HasCreateMarkdown_Method`
   is requirement-linked but has no scenario description.

**Source findings**: C-V01, C-V02, C-V03, C-A06

---

## GRP-13 — Compound requirements

**Impact**: Individual behaviors cannot fail independently; compliance evidence is ambiguous  
**Severity**: Low  
**Status**: ✅ Done — fixed YAML bug in `api-mark-task.yaml` (`ForwardApiHeaders` missing `- id:`); added 4 missing children to `api-mark-msbuild.yaml`; simplified compound requirement titles for `IncludeObsoleteApisWhenRequested`, `RenderDoxygenDocComments`, `ShowDirectInheritanceInTypeSignature`; expanded `DocumentOperatorOverloads` title to include `~ReturnType` suffix explicitly.

Several requirements bundle multiple independently testable behaviors into one statement:

| System | Requirement | Bundled behaviors |
| :----- | :---------- | :---------------- |
| DotNet | `IncludeObsoleteApisWhenRequested` | Include-when-enabled AND exclude-when-disabled |
| DotNet | `DocumentOperatorOverloads` | Grouping + C# operator symbols as headings + `~ReturnType` suffix resolution |
| DotNet | `EmitDetailPageForEveryMember` | General rule + case-collision exception (referenced but not linked) |
| Cpp | `RenderDoxygenDocComments` | `@brief` + `@param` + `@return` rendering |
| Cpp | `ShowDirectInheritanceInTypeSignature` | Base class names + `final` keyword |
| MSBuild | `ForwardApiHeaders` | Individual flags per entry + order preservation + `!`-prefix verbatim pass-through |

**Source findings**: DN-R02, DN-R03, DN-R04, X-R02, X-R03, M-R02

---

## GRP-14 — Documentation structure and quality

**Impact**: Design documents incomplete or inaccurate; minor correctness issues  
**Severity**: Low  
**Status**: ✅ Done — added `XmlDocReader`, `FileMarkdownWriterFactory` to Software Structure; added `DemaConsulting.TestResults` to OTS; fixed `ToolTask` naming error; removed unverified precondition; added Preconditions to 5 writer methods; added `CppGenerator` to Callers in both core docs; fixed grammar in `cpp-generator.md`; moved `ExternalTypeInfo` to Data Model.

A collection of smaller individual documentation issues:

| ID | File | Issue |
| :-- | :--- | :---- |
| DN-D02 | `design/api-mark-dot-net/dot-net-generator.md` | `XmlDocReader` has a dedicated source file with non-trivial logic (XML parsing, O(1) member-key indexing) but is not a declared unit in the Software Structure — documented only as a dependency note. |
| DN-A05 | `design/introduction.md` | `XmlDocReader.cs` appears in the Folder Layout but is absent from the Software Structure. |
| T-A02 | `design/introduction.md` | `DemaConsulting.TestResults` is cited in `api-mark-tool.md` Dependencies but has no entry in the Software Structure. |
| C-A05, C-R02 | `reqstream/api-mark-core/i-markdown-writer-factory.yaml` | `RejectInvalidOutputDirectory` and `EnsureOutputDirectory` test `FileMarkdownWriterFactory` (concrete class) but the unit ID implies they belong to the interface. `FileMarkdownWriterFactory` is not declared as a distinct unit — this is a unit identity ambiguity. |
| M-D02 | `design/api-mark-msbuild/api-mark-task.md` | Dependencies cites "ToolTask helper APIs" — but `ToolTask` is a different MSBuild base class. If `ApiMarkTask` extends `Task` and uses `System.Diagnostics.Process` directly, this is a naming error. |
| M-D03 | `design/api-mark-msbuild/api-mark-task.md` | Key Methods states "`ApiMarkOutputDir` must be a writable path" as a precondition but no writability check is described. |
| C-D02 | `design/api-mark-core/i-markdown-writer.md` | `WriteSignature`, `WriteParagraph`, `WriteTable`, `WriteCodeBlock`, `WriteLink` all lack Preconditions entries (only `WriteHeading` has one). |
| C-D03 | `design/api-mark-core/i-markdown-writer.md`, `i-markdown-writer-factory.md` | Callers sections list only `DotNetGenerator`; `CppGenerator` is also a caller. |
| DN-A04, DN-V01, X-V05 | Various verification docs | System verification documents contain unit-level test scenarios, blurring the system/unit boundary. |
| X-A05, X-D04 | `design/api-mark-cpp/cpp-generator.md` | Grammar: "If a section do not apply" → "does not apply." |
| DN-D03 | `design/api-mark-dot-net/dot-net-generator.md` | `ExternalTypeInfo` documented under Key Methods instead of Data Model. |

**Source findings**: DN-D02, DN-A05, T-A02, C-A05, C-R02, M-D02, M-D03, C-D02, C-D03, DN-A04, DN-V01, X-V05, X-A05, X-D04, DN-D03

---

## GRP-15 — User-facing documentation gaps

**Impact**: Users get incomplete or incorrect guidance from the documentation  
**Severity**: Low  
**Status**: ✅ Done — created `CONTRIBUTING.md`; added C++ MSBuild section to `installation.md`; added `APIMARK_CLANG_PATH` to clang prerequisites; fixed References section (removed internal links, now "None.").

Four issues in the user-facing documentation:

1. **Missing CONTRIBUTING.md** (P-F04): `README.md` links to
   `https://github.com/DemaConsulting/ApiMark/blob/main/CONTRIBUTING.md` which does not exist.

2. **Installation guide missing C++ MSBuild section** (P-F02): `docs/user_guide/installation.md`
   MSBuild Package section covers only C# projects. No guidance for `.vcxproj` integration.

3. **Installation guide missing `APIMARK_CLANG_PATH` environment variable** (P-F03): The clang
   prerequisites section covers `--clang-path` and `ApiMarkClangPath` but omits the
   `APIMARK_CLANG_PATH` environment variable (priority 2 in the discovery chain).

4. **User guide introduction References section** (P-F06): Lists internal companion sections
   (Installation, CLI Reference, MSBuild Integration) in the References section. Per the
   documentation standard, References must contain only external specifications.

**Source findings**: P-F02, P-F03, P-F04, P-F06

---

## Discussion Order

Suggested order — highest impact / most decisions needed first:

| # | Group | Key decision needed |
| :- | :---- | :------------------ |
| 1 | GRP-01 | Update verification introduction (mostly mechanical, just approval to proceed) |
| 2 | GRP-02 | Add missing children to reqstream YAML files (mechanical) |
| 3 | GRP-03 | Write 8 missing requirements — need to agree on wording |
| 4 | GRP-04 | Correct PathHelpers description (1-line fix, just confirm wording) |
| 5 | GRP-05 | Fix IContext parameter in unit designs (need to check actual method signature first) |
| 6 | GRP-06 | **Resolve `--includes` contradiction — need a decision on what the intended behavior is** |
| 7 | GRP-07 | Fix Cpp design gaps (documentation additions) |
| 8 | GRP-08 | Complete MSBuild External Interfaces C++ contract |
| 9 | GRP-09 | Add 3 missing Cpp verification scenarios |
| 10 | GRP-10 | Fix MSBuild verification gaps |
| 11 | GRP-11 | Complete Tool Context design |
| 12 | GRP-12 | Add Core verification scenarios |
| 13 | GRP-13 | Split compound requirements (or accept them) |
| 14 | GRP-14 | Documentation quality fixes (largely mechanical) |
| 15 | GRP-15 | User documentation gaps |
