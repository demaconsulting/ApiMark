# Review Findings

Formal reviews were performed across 21 review-sets covering all five system areas. Each review-set
was executed by an independent formal-review agent with a clean context.

---

## Summary

| System | Architecture | AllRequirements | Design | Verification | Findings |
| :----- | :----------- | :-------------- | :----- | :----------- | :------- |
| Purpose | — | — | — | — | 9 findings (2 High, 4 Med, 2 Low) |
| Core | FAIL | FAIL | FAIL | FAIL | 15 findings (4 High, 6 Med, 3 Low) |
| DotNet | FAIL | FAIL | FAIL | FAIL | 12 findings (2 High, 5 Med, 5 Low) |
| Cpp | FAIL | FAIL | FAIL | FAIL | 16 findings (4 High, 9 Med, 5 Low) |
| MSBuild | FAIL | FAIL | FAIL | FAIL | 13 findings (8 High, 4 Med, 1 Low) |
| Tool | FAIL | FAIL | FAIL | FAIL | 15 findings (0 High, 10 Med, 5 Low) |

---

## Purpose Review-Set

**Result**: FAILED — 9 findings (2 High, 4 Medium, 2 Low)

### Findings

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| P-F01 | High | `requirements.yaml`, `docs/verification/introduction.md` | C++ requirements (`api-mark-cpp.yaml`, `cpp-generator.yaml`, `ots/clang.yaml`) are included in requirements scope, but `docs/verification/introduction.md` explicitly places C++ in its "Out of scope" clause. Requirements exist with no planned verification strategy. |
| P-F02 | Medium | `docs/user_guide/installation.md` | MSBuild Package installation section covers only C# (`.csproj`). No guidance for C++ (`.vcxproj`) integration despite README and `msbuild-integration.md` covering it fully. |
| P-F03 | Medium | `docs/user_guide/installation.md` | `APIMARK_CLANG_PATH` environment variable (discovery priority 2, documented in `cli-reference.md`) is absent from the Prerequisites section. Users relying on the installation guide alone will not learn about this CI-friendly mechanism. |
| P-F04 | Medium | `README.md` | README links to `CONTRIBUTING.md` which does not exist in the repository. |
| P-F05 | High | `README.md`, `docs/design/introduction.md`, `requirements.yaml` vs. `docs/verification/introduction.md` | Direct contradiction on C++ delivery status: README lists C++ as a delivered feature; design introduction includes `ApiMarkCpp` in full scope; requirements include C++ files; yet the verification introduction explicitly places C++ "Out of scope." Reviewers cannot determine whether C++ is shipped or planned. |
| P-F06 | Low | `docs/user_guide/introduction.md` | References section lists internal companion sections (Installation, CLI Reference, MSBuild Integration). Per the documentation standard, the References section must contain only external specifications; internal companions must be mentioned in prose. |
| P-F07 | Medium | `requirements.yaml`, `docs/design/introduction.md`, `docs/verification/introduction.md` | `IContext` and `PathHelpers` have requirements and design entries, but are absent from the verification introduction Scope. No verification strategy is documented for these two Core units. |
| P-F08 | Low | `docs/verification/introduction.md` | Companion Artifact Structure omits `src/ApiMark.Cpp/`, reinforcing the C++ status contradiction identified in P-F05. |
| P-F09 | (dup) | — | Duplicate of P-F05 — same cross-file C++ status contradiction surfacing from a different angle. Resolved by P-F05 remediation. |

**Root cause**: The C++ delivery status was never aligned across requirements, design, and verification documents. C++ is implemented and in scope but the verification introduction was never updated to include it.

---

## Core Review-Sets

**Result**: All 4 FAILED — 16 findings (4 High, 6 Medium, 3 Low)

### Architecture (ApiMark-Core-Architecture)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| C-A01 | High | `docs/reqstream/api-mark-core.yaml` | `ApiMarkCore-IMarkdownWriterFactory-ProvideMarkdownWriterFactoryInterface` `children` list omits `RejectInvalidOutputDirectory` and `EnsureOutputDirectory`. Both exist in the unit file but are disconnected — ReqStream decomposition chain is broken. |
| C-A02 | High | `docs/design/api-mark-core.md` | PathHelpers External Interfaces description states it "Rejects null, rooted, or `..`-containing segments." This is factually incorrect. The correct rule is that segments are accepted when the combined result stays within the base; only combinations that escape the base are rejected. This misrepresents the security invariant. |
| C-A03 | High | `docs/verification/introduction.md` | `IContext` and `PathHelpers` are absent from both the Scope section and the Companion Artifact Structure (all three artifact columns). Both units have dedicated requirements, design, and verification files. |
| C-A04 | High | `docs/verification/introduction.md` | Companion Artifact Structure omits `src/ApiMark.Cpp/` and all ApiMarkCpp artifacts. ApiMarkCpp is in full scope in the design introduction. |
| C-A05 | Medium | `docs/reqstream/api-mark-core/i-markdown-writer-factory.yaml` | `RejectInvalidOutputDirectory` and `EnsureOutputDirectory` test the concrete class `FileMarkdownWriterFactory` but it is not declared as a unit in the Software Structure. Requirements IDs imply they belong to the interface unit but linked tests are implementation-specific. |
| C-A06 | Low | `docs/verification/api-mark-core.md` | IContext contract has no system-level verification scenario. `ApiMarkCore-IContext-ProvideOutputChannel` references `ApiMarkCore_ContextContract_WrittenMessages_AreAccessibleForAssertion` but the system verification document has no corresponding scenario or acceptance criterion. |

### AllRequirements (ApiMark-Core-AllRequirements)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| C-R01 | High | `docs/reqstream/api-mark-core.yaml` | (Same as C-A01) `children` list of `IMarkdownWriterFactory-ProvideMarkdownWriterFactoryInterface` breaks ReqStream traceability for `RejectInvalidOutputDirectory` and `EnsureOutputDirectory`. |
| C-R02 | Medium | `docs/reqstream/api-mark-core/i-markdown-writer-factory.yaml` | (Same as C-A05) `FileMarkdownWriterFactory` is not declared as a distinct unit; requirements testing its concrete behavior are co-located under the interface unit ID without declared rationale. |

### Design (ApiMark-Core-Design)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| C-D01 | High | `docs/design/api-mark-core.md` | (Same as C-A02) PathHelpers description in External Interfaces is factually wrong about `..`-containing segments. Must be corrected to: "Rejects combinations that resolve outside the base directory, and rejects null arguments." |
| C-D02 | Medium | `docs/design/api-mark-core/i-markdown-writer.md` | `WriteSignature`, `WriteParagraph`, `WriteTable`, `WriteCodeBlock`, and `WriteLink` all lack Preconditions entries. The design documentation standard requires preconditions for every key method. |
| C-D03 | Medium | `docs/design/api-mark-core/i-markdown-writer.md`, `i-markdown-writer-factory.md` | Callers sections list only `DotNetGenerator`. `CppGenerator` (declared in the Software Structure) is a current or anticipated caller of both interfaces and must be listed. |

### Verification (ApiMark-Core-Verification)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| C-V01 | Medium | `docs/verification/api-mark-core.md` | (Same as C-A06) IContext contract has no system-level Test Scenario or Acceptance Criterion despite the system requirement referencing a system-scoped test. |
| C-V02 | Medium | `docs/verification/api-mark-core/i-markdown-writer.md` | Six requirement-linked tests (`IMarkdownWriter_WriteX_ValidArgs_DoesNotThrow`) have no corresponding scenario descriptions. |
| C-V03 | Low | `docs/verification/api-mark-core/i-markdown-writer-factory.md` | `IMarkdownWriterFactory_HasCreateMarkdown_Method` is requirement-linked but has no scenario description in the verification document. |

---

## MSBuild Review-Sets

**Result**: All 4 FAILED — 13 findings (8 High, 4 Medium, 1 Low)

### Architecture (ApiMark-MSBuild-Architecture)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| M-A01 | High | `docs/reqstream/api-mark-msbuild.yaml` | `ApiMarkLibraryDescription` and `ApiMarkClangPath` properties are fully designed in `api-mark-task.md` with no backing requirements. Design-first violation of unidirectional flow. |
| M-A02 | High | `docs/reqstream/api-mark-msbuild.yaml` | Auto-population of `ApiMarkIncludePaths` from `AdditionalIncludeDirectories` is designed (Data Flow step 1), verified (test scenario), and implemented — but has no corresponding requirement. |
| M-A03 | High | `docs/design/api-mark-msbuild.md` | External Interfaces section documents only 4 of 10 C++ process arguments. Missing: `--library-name`, `--library-description`, `--defines`, `--cpp-standard`, `--api-headers`, `--clang-path`. Six corresponding MSBuild properties are also omitted from the task contract property list. |
| M-A04 | High | `docs/verification/introduction.md` | Scope and Companion Artifact Structure omit the entire **ApiMarkCpp** system, the **clang** OTS item, `src/ApiMark.Cpp/`, and `test/ApiMark.Cpp.Tests/`. All are present in `docs/design/introduction.md`. |

### AllRequirements (ApiMark-MSBuild-AllRequirements)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| M-R01 | High | `docs/reqstream/api-mark-msbuild.yaml`, `docs/reqstream/api-mark-msbuild/api-mark-task.yaml` | (Detailed from M-A01/M-A02) Four missing requirements: `ApiMarkLibraryDescription` forwarding, `ApiMarkClangPath` forwarding, include-path auto-population, DotNet XmlDocPath skip. |
| M-R02 | Low | `docs/reqstream/api-mark-msbuild/api-mark-task.yaml` | `ApiMarkMsbuild-ApiMarkTask-ForwardApiHeaders` bundles three independently testable behaviors into one requirement statement. Should be split or title rewritten to describe the observable outcome. |

### Design (ApiMark-MSBuild-Design)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| M-D01 | High | `docs/design/api-mark-msbuild.md` | (Same as M-A03) C++ interface contract in External Interfaces is incomplete — six arguments and six MSBuild properties missing. |
| M-D02 | Medium | `docs/design/api-mark-msbuild/api-mark-task.md` | Dependencies section cites "ToolTask helper APIs" under `Microsoft.Build.Utilities.Core`. `ToolTask` is a separate MSBuild base class; if `ApiMarkTask` extends `Task` and uses `System.Diagnostics.Process` directly, this reference is inaccurate. |
| M-D03 | Medium | `docs/design/api-mark-msbuild/api-mark-task.md` | Key Methods states "`ApiMarkOutputDir` must be a writable path" as a precondition but the design describes no writability validation. The stated precondition is not backed by described logic. |
| M-D04 | High | `docs/design/api-mark-msbuild/api-mark-task.md` | `ApiMarkLibraryDescription` and `ApiMarkClangPath` are fully specified in Data Model and Key Methods with no backing requirements. |

### Verification (ApiMark-MSBuild-Verification)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| M-V01 | High | `docs/verification/api-mark-msbuild/api-mark-task.md` | Acceptance Criteria omits the `ApiMarkApiHeaders` → individual `--api-headers` flags criterion. Unit-level requirement `ForwardApiHeaders` must have its acceptance criterion in the unit verification document. |
| M-V02 | High | `docs/verification/api-mark-msbuild/api-mark-task.md`, `docs/verification/api-mark-msbuild.md` | `ApiMarkTask_Execute_WithDotNetProject_GeneratesDocumentation` is named in requirement `SpawnToolForDotNet` but has no scenario description in either the unit or system verification documents. |
| M-V03 | Medium | `docs/verification/api-mark-msbuild/api-mark-task.md` vs. `docs/verification/api-mark-msbuild.md` | Unit verification marks `ApiMarkTask_Execute_WithCppProject_GeneratesDocumentation` as "not yet implemented"; system verification presents it as a fully realized scenario. Documents are mutually inconsistent. |
| M-V04 | Medium | `docs/verification/api-mark-msbuild/api-mark-task.md` | `ApiMarkTask_Cpp_ApiHeaders_ForwardedAsIndividualFlags` satisfies a unit-level requirement; its scenario description exists only in the system verification document. Unit requirements must be traceable through unit-level docs. |

---

## Tool Review-Sets

**Result**: All 4 FAILED — 15+ findings

### Architecture (ApiMark-Tool-Architecture)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| T-A01 | Medium | `docs/design/api-mark-tool.md` | Dependencies section omits `ApiMarkCpp` / `CppGenerator`. The Data Flow section explicitly names CppGenerator for the `cpp` subcommand. |
| T-A02 | Medium | `docs/design/api-mark-tool.md`, `docs/design/introduction.md` | `DemaConsulting.TestResults` is cited in `api-mark-tool.md` Dependencies but does not appear in the Software Structure in `docs/design/introduction.md`. |
| T-A03 | Medium | `docs/verification/introduction.md` | Scope and Companion Artifact Structure omit `ApiMarkCpp` and `CppGenerator` entirely. `docs/design/introduction.md` explicitly includes them. No documented verification strategy for the Cpp system. |
| T-A04 | Low | `docs/verification/introduction.md` | Only Mono.Cecil is listed under OTS items; clang OTS item (present in design introduction) is absent. |
| T-A05 | Medium | `docs/verification/introduction.md` | Companion Artifact Structure omits `i-context.yaml` and `path-helpers.yaml` (and their design/verification counterparts) for `ApiMarkCore` units IContext and PathHelpers. |

### AllRequirements (ApiMark-Tool-AllRequirements)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| T-R01 | Medium | `docs/reqstream/api-mark-tool/cli.yaml` | `ApiMarkTool-Cli-Context-ParseIncludes` and `ApiMarkTool-Cli-Context-ParseApiHeaders` are defined in `context.yaml` but not listed in the `children` of `ApiMarkTool-Cli-ProvideCliArgumentParsing` in `cli.yaml`. Decomposition chain is severed. |
| T-R02 | Medium | `docs/reqstream/api-mark-tool/cli/context.yaml` | No requirements for parsing `--library-name`, `--library-description`, `--defines`, or `--cpp-standard`. These options are consumed by `Program.CreateGenerator` but have no Context requirement coverage. |
| T-R03 | Medium | `docs/reqstream/api-mark-tool/cli/context.yaml` | `ApiMarkTool-Cli-Context-ParseIncludes` states "repeated --includes flags" but the linked test uses comma-separated parsing. The requirement text and test verification contradict each other. |

### Design (ApiMark-Tool-Design)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| T-D01 | Medium | `docs/design/api-mark-tool/cli/context.md` | Data Model table does not include properties for `--library-name`, `--library-description`, `--defines`, or `--cpp-standard`. `program.md` Key Methods (CreateGenerator) reads these from the Context, making the two unit designs contradictory. |
| T-D02 | Medium | `docs/design/api-mark-tool/cli.md` | Design section enumerates recognized options but omits `--library-name`, `--library-description`, `--defines`, and `--cpp-standard`. The External Interfaces of `api-mark-tool.md` lists them. |
| T-D03 | Low | `docs/design/api-mark-tool/program.md` | Dependencies section omits `CppGenerator`. The Key Methods section (CreateGenerator) explicitly constructs it for the `cpp` language path. |

### Verification (ApiMark-Tool-Verification)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| T-V01 | Medium | `docs/verification/api-mark-tool/cli/context.md` | Acceptance Criteria states "`--includes` splits on commas." The requirement says "repeated --includes flags." The design says "each --includes flag appends a single directory path." Three-way contradiction on observable behavior. |
| T-V02 | Medium | `docs/verification/api-mark-tool.md` | System-level Acceptance Criteria has no positive end-to-end scenario for the `cpp` subcommand despite `ApiMarkTool-Program-SupportCppOptions` being a requirement. |
| T-V03 | Low | `docs/verification/api-mark-tool/cli/context.md` | Four requirement-linked tests (`Context_Create_WithRepeatedIncludes_AccumulatesAllPaths`, `Context_Create_WithSingleInclude_SetsSinglePath`, `Context_Create_WithApiHeadersExclusionPattern_ForwardsVerbatim`, `Context_Create_WithNoArguments_HasEmptyApiHeaders`) are absent from documented scenarios. |
| T-V04 | Low | `docs/verification/api-mark-tool/program.md` | `Program_Main_CppWithApiHeadersFlag_FlagIsAccepted` is linked from `ApiMarkTool-Program-SupportCppOptions` but has no scenario description in the program verification document. |

**Cross-set root causes (Tool)**:

| Root Cause | Affects |
| :--------- | :------ |
| Context data model not updated when cpp-specific options were added to Program | T-R02, T-D01, T-D02, T-V01, T-V02 |
| `--includes` behavior ambiguous (repeated flags vs. comma-split) | T-R03, T-V01 |
| ApiMarkCpp absent from verification introduction | T-A03, T-A04 |
| `DemaConsulting.TestResults` undocumented in Software Structure | T-A02 |
| Broken decomposition chain for ParseIncludes/ParseApiHeaders | T-R01 |

---

## DotNet Review-Sets

**Result**: All 4 FAILED — 12 findings (2 High, 5 Medium, 5 Low)

**Overall root defect**: Five unit requirements in `api-mark-dot-net.yaml` have no parent system requirement (breaking the ReqStream decomposition chain), plus an inconsistency in the `Generate` method signature between system and unit design documents.

### Architecture (ApiMark-DotNet-Architecture)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| DN-A01 | High | `docs/reqstream/api-mark-dot-net.yaml` | Five unit requirements are not referenced as `children` of any system requirement: `EmitCombinedMemberPageForCaseInsensitiveCollisions`, `ApiMdListsAllNamespacesWithTypeCount`, `ShowDirectInheritanceInTypeSignature`, `EmitIntraDocLinksInTableCells`, `EmitExternalTypesSection`. ReqStream decomposition chain is incomplete. |
| DN-A02 | Medium | `docs/design/api-mark-dot-net.md` | External Interfaces section lists `IApiGenerator (provided)` and `Mono.Cecil (consumed)` but omits `IMarkdownWriterFactory` and `IContext` as consumed interfaces, despite the quoted contract reading `Generate(IMarkdownWriterFactory factory, IContext context)`. |
| DN-A03 | Medium | `docs/verification/introduction.md` | Scope and Companion Artifact Structure omit `IContext` and `PathHelpers` units, and also `src/ApiMark.Cpp/`, `test/ApiMark.Cpp.Fixtures/`, and `test/ApiMark.Cpp.Tests/`. (Recurring cross-system issue.) |
| DN-A04 | Medium | `docs/verification/api-mark-dot-net.md` | System verification Test Scenarios references four unit-level test methods. Per the standard, system-level verification must reference only system-level tests. |
| DN-A05 | Low | `docs/design/introduction.md` | `XmlDocReader.cs` appears in the Folder Layout but is absent from the Software Structure. It has non-trivial responsibilities and is referenced as a dependency in the unit design. |

### AllRequirements (ApiMark-DotNet-AllRequirements)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| DN-R01 | High | `docs/reqstream/api-mark-dot-net.yaml` | (Same as DN-A01) Five unit requirements have no parent system requirement; decomposition chain is broken. |
| DN-R02 | Medium | `docs/reqstream/api-mark-dot-net/dot-net-generator.yaml` | `IncludeObsoleteApisWhenRequested` specifies two independent testable criteria in one statement (include-when-enabled and exclude-when-disabled). Should be split. |
| DN-R03 | Medium | `docs/reqstream/api-mark-dot-net/dot-net-generator.yaml` | `DocumentOperatorOverloads` bundles at least three independently testable behaviors (grouping, C# operator symbols as headings, `~ReturnType` suffix resolution). Should be decomposed. |
| DN-R04 | Medium | `docs/reqstream/api-mark-dot-net/dot-net-generator.yaml` | `EmitDetailPageForEveryMember` states "every visible member always receives its own dedicated page" but references `EmitCombinedMemberPageForCaseInsensitiveCollisions` as an exception with no formal parent-child link. |
| DN-R05 | Low | `docs/reqstream/api-mark-dot-net/dot-net-generator.yaml` | `ReadAssemblyMetadataWithoutLoading` includes "without loading them into the current process" — a HOW implementation constraint rather than a pure observable WHAT behavior. |

### Design (ApiMark-DotNet-Design)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| DN-D01 | High | `docs/design/api-mark-dot-net/dot-net-generator.md` | `DotNetGenerator.Generate` Key Methods lists only `IMarkdownWriterFactory factory` as a parameter. The system design states `Generate(IMarkdownWriterFactory factory, IContext context)`. One document is incorrect — the actual method signature must be checked and both documents aligned. |
| DN-D02 | Medium | `docs/design/api-mark-dot-net/dot-net-generator.md` | `XmlDocReader` is documented only as a dependency entry, not as a unit with its own design. It has a dedicated source file with non-trivial logic (XML parsing, O(1) member-key indexing). |
| DN-D03 | Low | `docs/design/api-mark-dot-net/dot-net-generator.md` | `ExternalTypeInfo` (a data record) is documented under Key Methods rather than Data Model. |

### Verification (ApiMark-DotNet-Verification)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| DN-V01 | Medium | `docs/verification/api-mark-dot-net.md` | (Same as DN-A04) System verification document references unit-level test methods. Unit-level scenarios should live in the unit verification document. |
| DN-V02 | Low | `docs/verification/api-mark-dot-net/dot-net-generator.md` | Scenario for `ShowDirectInheritanceInTypeSignature` covers only one of two linked tests; `DotNetGenerator_Generate_EnumTypeSignature_HasNoBaseClass` is absent. |
| DN-V03 | Low | `docs/verification/api-mark-dot-net/dot-net-generator.md` | Acceptance Criteria does not mention type inheritance signature verification or intra-doc link generation despite test scenarios covering both. |

---

## Cpp Review-Sets

**Result**: All 4 FAILED — 16 findings (4 High, 9 Medium, 5 Low)

### Architecture (ApiMark-Cpp-Architecture)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| X-A01 | Medium | `docs/reqstream/api-mark-cpp.yaml` | System requirement lists only 5 of 17 unit requirements as children; 12 are unlinked: `EmitCombinedMemberPageForCaseInsensitiveCollisions`, `FilterByVisibility`, `IncludeDeprecatedApisWhenRequested`, `RenderDoxygenDocComments`, `ApiMdListsAllNamespacesWithTypeCount`, `GroupOperatorOverloadsOnSinglePage`, `ShowDirectInheritanceInTypeSignature`, `EmitIntraDocLinksInTableCells`, `ApplyGitignoreStylePatternSelectionToHeaders`, `EmitExternalTypesSection`, `ShowDeletedFunctionsWithDeletedNotation`, `DocumentTypeAliases`. |
| X-A02 | Medium | `docs/design/api-mark-cpp.md` | External Interfaces states "all paths in PublicIncludeRoots must exist on disk" as a constructor precondition, but `DirectoryNotFoundException` is thrown by `Generate`. Test name confirms Generate-time validation. System design is misleading about when validation occurs. |
| X-A03 | High | `docs/verification/introduction.md` | ApiMarkCpp system is entirely absent from Scope and Companion Artifact Structure. The out-of-scope note ("Planned future language implementations") is stale — C++ is implemented. All ApiMarkCpp artifacts, `src/ApiMark.Cpp/`, and `test/ApiMark.Cpp.Tests/` must be added. |
| X-A04 | Medium | `docs/verification/introduction.md` | clang OTS item is absent from the verification introduction despite being a primary OTS dependency of ApiMarkCpp (listed in design introduction). |
| X-A05 | Low | `docs/design/api-mark-cpp/cpp-generator.md` | Grammar error on line 3: "If a section do not apply" should be "does not apply." |

### AllRequirements (ApiMark-Cpp-AllRequirements)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| X-R01 | Medium | `docs/reqstream/api-mark-cpp.yaml` | (Same as X-A01) Incomplete decomposition chain — 12 of 17 unit requirements not linked from the parent system requirement. |
| X-R02 | Low | `docs/reqstream/api-mark-cpp/cpp-generator.yaml` | `RenderDoxygenDocComments` is compound (`@brief`, `@param`, `@return` combined). Should be split or have separate tests per tag type. |
| X-R03 | Low | `docs/reqstream/api-mark-cpp/cpp-generator.yaml` | `ShowDirectInheritanceInTypeSignature` is compound (base class rendering + `final` keyword). Should be split into two requirements with separate test coverage. |

### Design (ApiMark-Cpp-Design)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| X-D01 | Medium | `docs/design/api-mark-cpp/cpp-generator.md` | `ClangAstParser.Parse` is not documented as a Key Method. `CppAstModel` types (`CppCompilationResult`, `CppNamespaceDecl`, `CppClass`, `CppFunction`, `CppField`, `CppTypeAlias`) are referenced in narrative but absent from the Data Model section. |
| X-D02 | Medium | `docs/design/api-mark-cpp/cpp-generator.md` | `IContext context` parameter is documented in the system design Generate signature but is entirely absent from the unit design Key Methods table. How CppGenerator uses IContext is not described. |
| X-D03 | Medium | `docs/design/api-mark-cpp/cpp-generator.md` | Constructor preconditions claim directory-existence validation; Error Handling section says `DirectoryNotFoundException` is thrown by Generate. These are directly contradictory. |
| X-D04 | Low | `docs/design/api-mark-cpp/cpp-generator.md` | (Same as X-A05) Grammar error: "do not apply" → "does not apply." |

### Verification (ApiMark-Cpp-Verification)

| ID | Severity | File(s) | Description |
| :-- | :------- | :------ | :---------- |
| X-V01 | High | `docs/verification/api-mark-cpp/cpp-generator.md` | `EmitIntraDocLinksInTableCells` requirement links to `CppGenerator_Generate_IntraLibraryReturnType_EmitsMarkdownLinkInReturnsCell`. This test has no scenario description in either verification document. No Acceptance Criteria entry covers intra-library link generation. |
| X-V02 | High | `docs/verification/api-mark-cpp/cpp-generator.md` | `ApplyGitignoreStylePatternSelectionToHeaders` links to 5 tests; none are documented as verification scenarios. No Acceptance Criteria entry covers pattern-selection behavior. |
| X-V03 | High | `docs/verification/api-mark-cpp/cpp-generator.md` | `EmitExternalTypesSection` links to `CppTypeLinkResolver_Linkify_UnknownNamespacedType_TracksExternalType`. No scenario description or Acceptance Criteria entry exists in either verification document. |
| X-V04 | Medium | `docs/verification/api-mark-cpp.md` | System verification is missing scenarios for 7 newer requirements: `ApiMdListsAllNamespacesWithTypeCount`, `GroupOperatorOverloadsOnSinglePage`, `EmitCombinedMemberPageForCaseInsensitiveCollisions`, `ShowDirectInheritanceInTypeSignature` (partial), `EmitIntraDocLinksInTableCells`, `ApplyGitignoreStylePatternSelectionToHeaders`, `EmitExternalTypesSection`. |
| X-V05 | Low | `docs/verification/api-mark-cpp.md` | System verification contains many unit-level scenarios (constructor null-argument checks, per-feature behaviors) blurring the system/unit verification boundary. |

---

## Cross-System Recurring Themes

The following issues recur across multiple systems and share a common root cause:

| Theme | Systems Affected |
| :---- | :--------------- |
| **C++ delivery-status contradiction** — `docs/verification/introduction.md` explicitly places C++ "Out of scope" while design, requirements, README, and implementation all include it. The entire ApiMarkCpp system is invisible to the verification introduction. | Purpose, Core, DotNet, Cpp, MSBuild, Tool (all six) |
| **Missing requirements for implemented behavior** — Features are designed, implemented, and tested without a backing requirement (unidirectional flow violated). | MSBuild (`ApiMarkLibraryDescription`, `ApiMarkClangPath`, include-path auto-population); Tool (cpp-specific Context properties) |
| **Incomplete ReqStream decomposition chains** — Unit requirements not listed as children of their parent system requirement, preventing traceability validation. | Core (`IMarkdownWriterFactory`), Cpp (12 of 17 unit requirements), DotNet (5 unit requirements), Tool (`ParseIncludes`, `ParseApiHeaders`) |
| **`IContext` and `PathHelpers` absent from verification introduction scope and companion artifacts** — Both units exist with requirements, design, and verification files but are invisible to the introduction document. | Core, Purpose, DotNet, Tool |
| **`IContext context` parameter missing from unit-level Generate design** — System design documents it in the method signature; unit designs omit it entirely. | DotNet, Cpp |
| **PathHelpers security invariant described incorrectly** in system-level design document (`..`-containing segments stated as rejected, when the actual rule is escape-prevention). | Core |
| **`DemaConsulting.TestResults` absent from Software Structure** — Cited as a dependency in unit design but has no entry in `docs/design/introduction.md`. | Tool |
| **`--includes` mechanism ambiguous** — Requirement says "repeated flags," design says "appends single path," acceptance criterion says "comma-split." Three-way contradiction. | Tool |
