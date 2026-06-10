# Contributing to ApiMark

Thank you for your interest in contributing to ApiMark.

## Development Setup

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) 8.0 or later
- [LLVM/Clang](https://releases.llvm.org/) (for C++ support)
- [PowerShell](https://github.com/PowerShell/PowerShell) 7.0 or later

### Getting Started

1. Fork the repository and clone your fork:

   ```bash
   git clone https://github.com/<your-username>/ApiMark.git
   cd ApiMark
   ```

2. Build and run all tests:

   ```pwsh
   pwsh ./build.ps1
   ```

3. Run the auto-formatter before committing:

   ```pwsh
   pwsh ./fix.ps1
   ```

## Submitting Changes

- Open an issue before starting significant work so the approach can be agreed.
- Keep pull requests focused — one logical change per PR.
- All tests must pass and lint must be clean before a PR is reviewed.
- Write or update tests for any behavior change.
- Update documentation (design docs, verification docs, requirements) to match
  any implementation changes.

## Code Style

Code formatting is enforced by `.editorconfig` and `dotnet format`. Run
`pwsh ./fix.ps1` to apply all auto-fixers before committing.

## Reporting Bugs

Open a [GitHub Issue](https://github.com/DemaConsulting/ApiMark/issues) with
a minimal reproduction case and the full error output.

## License

By contributing you agree that your contributions will be licensed under the
[MIT License](https://github.com/DemaConsulting/ApiMark/blob/main/LICENSE).
