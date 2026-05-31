---
name: ReviewMark Usage
description: Follow these standards when configuring file reviews with ReviewMark.
---

# ReviewMark Usage Standard

## Required Standards

Read these standards first before applying this standard:

- **`software-items.md`** - Software categorization (System/Subsystem/Unit/OTS/Shared Package)

## Purpose

ReviewMark manages file review status enforcement and formal review processes. It tracks which files need
review, organizes them into review-sets, and generates review plans and reports.

## Key Commands

- **Lint Configuration**: `dotnet reviewmark --lint`
- **Elaborate Review-Set**: `dotnet reviewmark --elaborate {review-set}`
- **Generate Plan**: `dotnet reviewmark --plan docs/code_review_plan/generated/plan.md --enforce`

> **Note**: `--enforce` causes the plan to fail with a non-zero exit code if any repository
> files are not covered by a review-set. Uncovered files indicate a gap in review-set
> configuration that should be addressed.

## Repository Structure

Required repository items for ReviewMark operation:

- `.reviewmark.yaml` - Configuration for review-sets, file-patterns, and review evidence-source.
- `docs/code_review_plan/generated/` - Generated review plan (build output, do not edit)
- `docs/code_review_report/generated/` - Generated review report (build output, do not edit)

# Review Definition Structure

Configure reviews in `.reviewmark.yaml` at repository root:

```yaml
# Patterns identifying all files that require review
needs-review:
  # Include source code (adjust file extensions for your repo)
  - "**/*.cs"           # C# source files
  - "**/*.cpp"          # C++ source files
  - "**/*.hpp"          # C++ header files
  - "!**/bin/**"        # Generated source in build outputs
  - "!**/obj/**"        # Generated source in build intermediates

  # Include requirement files
  - "requirements.yaml"        # Root requirements file
  - "docs/reqstream/**/*.yaml" # Requirements files

  # Include critical documentation files
  - "README.md"                                 # Root level README
  - "docs/user_guide/**/*.md"                   # User guide
  - "docs/design/**/*.md"                       # Design documentation
  - "docs/verification/**/*.md"                 # Verification design documentation

# Source of review evidence
evidence-source:
  type: none

# Review-sets (each focuses on a single compliance question)
reviews:
  - id: Purpose
    title: Review of user-facing capabilities and system promises
    paths:
      - "README.md"
      - "docs/user_guide/**/*.md"
      - "docs/reqstream/{system-name}.yaml"
      - "docs/design/introduction.md"
      - "docs/design/{system-name}.md"
```

# Review-Set Design Principles

When constructing review-sets, follow these principles to maintain manageable scope and effective compliance evidence:

- **Hierarchical Scope**: Higher-level reviews exclude lower-level implementation details, relying instead on design
  documents to describe what components they use. System reviews exclude subsystem/unit details, subsystem reviews
  exclude unit source code, only unit reviews include actual implementation.
- **Single Focus**: Each review-set proves one specific compliance question (user promises, system architecture,
  design consistency, etc.)
- **Context Management**: Keep file counts manageable to prevent context overflow while maintaining complete coverage
  through the hierarchy

# Review-Set Organization

**Naming conventions**: Placeholders in documentation, requirements, design, and
verification file paths are kebab-case (e.g., `{system-name}`). Placeholders in
source and test file paths may use the casing conventional for the project's
source language or repository layout (e.g., `{SystemName}`). Review-set name
placeholders are always PascalCase (e.g., `{SystemName}`).

## `Purpose` Review (only one per repository)

Reviews user-facing capabilities and system promises:

- **Purpose**: Proves that the systems provide the capabilities the user is being told about
- **Title**: "Review that Advertised Features Match System Design"
- **Scope**: Excludes subsystem and unit files, relying on system-level design documents
  to describe what subsystems and units they use
- **File Path Patterns**:
  - README: `README.md`
  - User guide: `docs/user_guide/**/*.md`
  - System requirements: `docs/reqstream/{system-name}.yaml`
  - Design introduction: `docs/design/introduction.md`
  - System design: `docs/design/{system-name}.md`

## `{SystemName}-Architecture` Review (one per system)

Reviews system architecture and operational validation:

- **Purpose**: Proves that the system is designed and tested to satisfy its requirements
- **Title**: "Review that {SystemName} Architecture Satisfies Requirements"
- **Scope**: Excludes subsystem and unit files, relying on system-level design to describe
  what subsystems and units it uses
- **File Path Patterns**:
  - System requirements: `docs/reqstream/{system-name}.yaml`
  - Design introduction: `docs/design/introduction.md`
  - System design: `docs/design/{system-name}.md`
  - Verification introduction: `docs/verification/introduction.md`
  - System verification design: `docs/verification/{system-name}.md`
  - System integration tests: `test/{SystemName}.Tests/{SystemName}Tests.{ext}`

## `{SystemName}-Design` Review (one per system)

Reviews architectural and design consistency:

- **Purpose**: Proves the system design is consistent and complete
- **Title**: "Review that {SystemName} Design is Consistent and Complete"
- **Scope**: Only brings in top-level requirements and relies on brevity of design documentation
- **File Path Patterns**:
  - System requirements: `docs/reqstream/{system-name}.yaml`
  - Platform requirements: `docs/reqstream/{system-name}/platform-requirements.yaml`
  - Design introduction: `docs/design/introduction.md`
  - System design: `docs/design/{system-name}.md`
  - System design files: `docs/design/{system-name}/**/*.md`
  - OTS overview: `docs/design/ots.md` _(only if OTS items exist)_
  - Shared Package overview: `docs/design/shared.md` _(only if Shared Package items exist)_

## `{SystemName}-Verification` Review (one per system)

Reviews verification completeness and consistency:

- **Purpose**: Proves the system verification design is consistent and covers all requirements
- **Title**: "Review that {SystemName} Verification is Consistent and Complete"
- **Scope**: Only brings in top-level requirements and all verification docs for the system
- **File Path Patterns**:
  - System requirements: `docs/reqstream/{system-name}.yaml`
  - Verification introduction: `docs/verification/introduction.md`
  - System verification: `docs/verification/{system-name}.md`
  - System verification files: `docs/verification/{system-name}/**/*.md`
  - OTS overview: `docs/verification/ots.md` _(only if OTS items exist)_
  - Shared Package overview: `docs/verification/shared.md` _(only if Shared Package items exist)_

## `{SystemName}-AllRequirements` Review (one per system)

Reviews requirements quality and traceability:

- **Purpose**: Proves the requirements are consistent and complete
- **Title**: "Review that All {SystemName} Requirements are Complete"
- **Scope**: Only brings in requirements files to keep review manageable
- **File Path Patterns**:
  - Root requirements: `requirements.yaml`
  - System requirements: `docs/reqstream/{system-name}.yaml`
  - Subsystem/unit requirements: `docs/reqstream/{system-name}/**/*.yaml`

## `{SystemName}-{SubsystemName}[-{SubsystemName}...]` Review (one per subsystem at any depth)

Reviews subsystem architecture and interfaces:

- **Purpose**: Proves that the subsystem is designed and tested to satisfy its requirements
- **Title**: "Review that {SystemName} {SubsystemName} Satisfies Subsystem Requirements"
- **Scope**: Excludes units under the subsystem, relying on subsystem design to describe
  what units it uses
- **File Path Patterns**:
  - Requirements: `docs/reqstream/{system-name}[/{subsystem-name}...]/{subsystem-name}.yaml`
  - Design: `docs/design/{system-name}[/{subsystem-name}...]/{subsystem-name}.md`
  - Verification design: `docs/verification/{system-name}[/{subsystem-name}...]/{subsystem-name}.md`
  - Tests: `test/{SystemName}.Tests[/{SubsystemName}...]/{SubsystemName}Tests.{ext}`

## `{SystemName}-{SubsystemName}[-{SubsystemName}...]-{UnitName}` Review (one per unit)

Reviews individual software unit implementation:

- **Purpose**: Proves the unit is designed, implemented, and tested to satisfy its requirements
- **Title**: "Review that {SystemName} {SubsystemName} {UnitName} Implementation is Correct"
- **Scope**: Complete unit review including all artifacts
- **File Path Patterns**:
  - Requirements: `docs/reqstream/{system-name}[/{subsystem-name}...]/{unit-name}.yaml`
  - Design: `docs/design/{system-name}[/{subsystem-name}...]/{unit-name}.md`
  - Verification design: `docs/verification/{system-name}[/{subsystem-name}...]/{unit-name}.md`
  - Source (C# example): `src/{SystemName}[/{SubsystemName}...]/{UnitName}.cs`
  - Tests (C# example): `test/{SystemName}.Tests[/{SubsystemName}...]/{UnitName}Tests.cs`
  - Source (snake_case C++ example): `src/{system_name}[/{subsystem_name}...]/{unit_name}.cpp`
  - Tests (snake_case C++ example): `test/{system_name}_tests[/{subsystem_name}...]/{unit_name}_tests.cpp`

## `OTS-{OtsName}` Review (one per OTS item)

Reviews OTS item integration design, requirements, and verification evidence:

- **Purpose**: Proves that the OTS item provides the required functionality and is correctly integrated
- **Title**: "Review that {OtsName} Provides Required Functionality"
- **Scope**: No local source code; review covers integration design, requirements, and verification evidence
- **File Path Patterns**:
  - OTS requirements: `docs/reqstream/ots/{ots-name}.yaml`
  - OTS integration design: `docs/design/ots/{ots-name}.md`
  - OTS verification: `docs/verification/ots/{ots-name}.md`
  - Tests (if applicable): `test/OtsSoftwareTests/...` (C#) or `test/ots_software_tests/...`
    (Python/other) — fixed repo-level name, no system prefix

## `Shared-{PackageName}` Review (one per Shared Package)

Reviews Shared Package integration design, requirements, and verification evidence:

- **Purpose**: Proves that the Shared Package provides the required advertised features and is correctly integrated
- **Title**: "Review that {PackageName} Provides Required Features"
- **Scope**: No local source code; review covers integration design, requirements, and verification evidence
- **File Path Patterns**:
  - Shared Package requirements: `docs/reqstream/shared/{package-name}.yaml`
  - Shared Package integration design: `docs/design/shared/{package-name}.md`
  - Shared Package verification: `docs/verification/shared/{package-name}.md`

**Note**: File path patterns use `{ext}` as a placeholder for language-specific
extensions (`.cs`, `.cpp`/`.hpp`, `.py`, etc.). Adapt to your repository's languages.

# Quality Checks

Before submitting ReviewMark configuration, verify:

- [ ] `.reviewmark.yaml` exists at repository root with proper structure
- [ ] Review-set organization follows the standard hierarchy patterns
- [ ] Each review-set focuses on a single compliance question (single focus principle)
- [ ] File patterns use correct glob syntax and match intended files
- [ ] Review-set file counts remain manageable (context management principle)
