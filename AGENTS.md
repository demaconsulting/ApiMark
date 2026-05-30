# Project Overview

> **Downstream customization required**: Replace the `TODO` values below with
> values specific to the target repository. These values are the canonical source
> for project-wide template placeholders used throughout the repository.

- **project-name**: ApiMark
- **organization**: DEMA Consulting
- **project-tagline**: Compact AI-friendly API reference documentation
- **description**: ApiMark generates compact, AI-friendly API reference documentation
  in Markdown from source code and its associated metadata (XML doc comments, header
  files, docstrings, etc.). The output is designed for gradual disclosure: an AI can
  read a lightweight index, drill into a namespace summary, and then read a full type
  page — consuming only as much context as the task requires.
- **languages**: C#
- **technologies**: .NET, MSBuild

# Project Structure

> **Downstream customization required**: Replace `{project}` and
> `{test-project}` with the actual source and test project folder names.

```text
├── docs/
│   ├── build_notes/
│   ├── code_quality/
│   ├── code_review_plan/
│   ├── code_review_report/
│   ├── design/
│   ├── requirements_doc/
│   ├── requirements_report/
│   ├── reqstream/
│   ├── user_guide/
│   └── verification/
├── src/
│   ├── ApiMark.Core/
│   ├── ApiMark.DotNet/
│   ├── ApiMark.MSBuild/
│   └── ApiMark.Tool/
└── test/
    ├── ApiMark.Core.Tests/
    ├── ApiMark.DotNet.Tests/
    └── ApiMark.Tool.Tests/
```

# Language and Spelling (ALL Agents)

Always use **US English** spelling in all output (code, comments, documentation,
commit messages, and reports).

# Reference Template

This repository follows a reference template for structure and file conventions.

- **Template URL**: `https://github.com/demaconsulting/Agents/raw/refs/heads/template`
- **Repository map**: `{template-url}/repository-map.md`
- **Template files**: `{template-url}/{file-path}` for files described in the map

# Codebase Navigation (ALL Agents)

When working with source code, design, or requirements artifacts, read
`docs/design/introduction.md` first. It provides the software structure,
folder layout, and companion artifact locations. Use it as the primary map
before searching the filesystem.

# Key Configuration Files

- **`.config/dotnet-tools.json`** - Local tool manifest for Continuous Compliance tools
- **`.editorconfig`** - Code formatting rules
- **`.clang-format`** - C/C++ formatting (if applicable)
- **`.cspell.yaml`** - Spell-check configuration and technical term dictionary
- **`.markdownlint-cli2.yaml`** - Markdown formatting rules
- **`.yamllint.yaml`** - YAML formatting configuration
- **`.reviewmark.yaml`** - File review definitions and tracking
- **`nuget.config`** - NuGet package sources (if .NET)
- **`package.json`** - Node.js dependencies for formatting tools
- **`requirements.yaml`** - Root requirements file with includes
- **`pip-requirements.txt`** - Python dependencies for yamllint and yamlfix
- **`fix.ps1`** - Applies all auto-fixers silently (dotnet format, markdown, YAML). Always exits 0.
- **`build.ps1`** - Builds the solution and runs all tests.

# Standards Application (ALL Agents Must Follow)

Before performing any work, agents must read and apply the relevant standards
from `.github/standards/`. Use this matrix to determine which to load:

- **Any code**: `coding-principles.md`
- **C# code**: `coding-principles.md`, `csharp-language.md`
- **C++ code**: `coding-principles.md`, `cpp-language.md`
- **Any tests**: `testing-principles.md`
- **C# tests**: `testing-principles.md`, `csharp-testing.md`
- **C++ tests**: `testing-principles.md`, `cpp-testing.md`
- **Requirements**: `requirements-principles.md`, `software-items.md`, `reqstream-usage.md`
- **Design docs**: `software-items.md`, `design-documentation.md`, `technical-documentation.md`
- **Verification docs**: `software-items.md`, `verification-documentation.md`, `technical-documentation.md`
- **Review configuration**: `software-items.md`, `reviewmark-usage.md`
- **Any documentation**: `technical-documentation.md`

Load only the standards relevant to your specific task scope.

# Agent Delegation Guidelines

The default agent should handle simple, straightforward tasks directly.
Delegate to specialized agents only for specific scenarios:

- **Pre-PR lint cleanup** (fix all lint issues before pull request) → Call the lint-fix agent
- **Light development work** (small fixes, simple features) → Call the developer agent
- **Light quality checking** (basic validation) → Call the quality agent
- **Formal feature implementation** (complex, multi-step) → Call the implementation agent
- **Formal bug resolution** (complex debugging, systematic fixes) → Call the implementation agent
- **Formal reviews** (compliance verification, detailed analysis) → Call the formal-review agent
- **Structural audit**: (repository layout vs. template) → Call the template-sync agent

# Agent Reporting (Specialized Agents Must Follow)

Specialized agents MUST generate a completion report:

1. Save to `.agent-logs/{agent-name}-{subject}-{unique-id}.md`
   where `{subject}` is a kebab-case task summary (max 5 words) and
   `{unique-id}` is a short unique suffix (e.g., 8-char hex or timestamp)
2. Start with `**Result**: (SUCCEEDED|FAILED)` as the first metadata field
3. Include the agent-specific report sections defined in each agent's prompt
4. Return the summary to the caller

Result semantics for orchestrator decision-making:

- **SUCCEEDED**: Work completed and all applicable quality gates met
- **FAILED**: Work could not be completed or quality gates not met
- **INCOMPLETE**: Work cannot proceed without information only the user can
  provide (implementation agent only)

# Formatting (After Making Changes)

After making changes, run the auto-fix pass. This applies all available fixers
silently and **always exits 0** - agents do not need to respond to its output.

```pwsh
pwsh ./fix.ps1
```

This automatically handles: `dotnet format`, markdown formatting, and YAML
formatting. Full lint compliance is a **pre-PR responsibility**, not an agent
responsibility - invoke the lint-fix agent once before submitting a pull request.

## CI Quality Tools

CI runs `lint.ps1` which checks: markdownlint-cli2, cspell, yamllint, dotnet format,
reqstream, versionmark, and reviewmark.

# Scope Discipline (ALL Agents Must Follow)

- **No generated file access**: Files inside any `generated/` folder are build
  outputs - do not read, lint, or modify them
- **Minimum necessary changes**: Only modify files directly required by the task
- **No speculative refactoring**: Do not refactor code adjacent to the change
  unless the task explicitly requests it
- **No drive-by fixes**: If you discover pre-existing issues in files you are
  reading but not modifying, document them in the report but do not fix them
- **Declare scope upfront**: Before making changes, determine which files will be
  modified. Any file outside this scope requires explicit justification.

# Protected Configuration Files

These files contain carefully designed configuration with documented intent
in header comments. Agents MUST NOT modify them unless the task explicitly
requires it and the modification preserves the documented design intent:

- `.reviewmark.yaml`, `.cspell.yaml`, `.editorconfig`
- `.markdownlint-cli2.yaml`, `.yamllint.yaml`
- `requirements.yaml`, `fix.ps1`, `lint.ps1`

# Continuous Compliance Overview

This repository follows the [Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance)
approach. Tools: **ReqStream** (requirements traceability), **ReviewMark** (file review enforcement),
**BuildMark** (tool versions), **VersionMark** (version tracking).
