# Introduction

This document contains the build notes for ApiMark.

## Purpose

This document provides a record of the changes, new features, and bug fixes included
in this release of ApiMark. It also records the versions of all tools used in
the build pipeline, providing traceability between the software artifacts and the
environment that produced them.

## Scope

This build notes report covers:

- Version information and commit details for this release
- Changes and new features implemented since the previous version
- Bugs fixed in this release
- Versions of all tools used in the build and compliance pipeline

### Build Prerequisites

To build ApiMark, the following tools must be installed:

- **.NET SDK 8.0 or later** — required for building and testing the C# solution
- **Node.js** — required for npm packages such as `markdownlint-cli2`
- **Python 3** — required for `yamllint` and `yamlfix`
- **PowerShell 7+** (`pwsh`) — required for build and fix scripts

After installing prerequisites, restore all dependencies:

```powershell
npm install                           # Node.js tools
pip install -r pip-requirements.txt  # Python tools
dotnet tool restore                   # .NET tools
```

Then build and test the solution:

```powershell
pwsh ./build.ps1
```

## References

[ApiMark releases](https://github.com/DemaConsulting/ApiMark/releases)
