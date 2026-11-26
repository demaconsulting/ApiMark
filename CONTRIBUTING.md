# Contributing to ApiMark

Thank you for your interest in contributing to ApiMark.

## Development Setup

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) 10.0 or later
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
   pwsh ./build.ps1 -All
   ```

3. Run the auto-formatter before committing:

   ```pwsh
   pwsh ./fix.ps1 -All
   ```

   While iterating, `pwsh ./build.ps1 -Changed`, `pwsh ./fix.ps1 -Changed`, and `pwsh ./lint.ps1 -Changed` limit
   the work to what your change affects. Each script requires a scope and prints its usage with `-Help`.

## Submitting Changes

- Open an issue before starting significant work so the approach can be agreed.
- Keep pull requests focused — one logical change per PR.
- All tests must pass and lint must be clean before a PR is reviewed.
- Write or update tests for any behavior change.
- Update documentation (design docs, verification docs, requirements) to match
  any implementation changes.

## Code Style

Code formatting is enforced by `.editorconfig` and `dotnet format`. Run
`pwsh ./fix.ps1 -All` to apply all auto-fixers before committing (`-Changed` while iterating).

## Help Cards

The built-in help (`apimark help`) is served from Markdown help cards in `src/ApiMark.Tool/Help/`, which the
DemaConsulting.HelpCards package embeds in the tool assembly. `Cli/HelpCommand.cs` is the only code that uses the
package.

- **One file per topic.** The path below `Help/`, without `.md`, is the card id. `_source.md` names the source
  (`name:`) and sets `schema: 2`.
- **Front matter.** Every card has a `title`, a `summary` (at most 80 characters for a topic, 100 otherwise), and
  exactly one purpose tag: `tutorial`, `reference`, or `troubleshooting`. Add `aliases` for topic names that users
  commonly guess, such as `examples`, so `help <name>` finds the card. Aliases are lower-case words, so `c++` and
  `c#` cannot be aliases.
- **New topics.** Add the id of every new top-level card to `HelpCommand.RootCardIds`, the one list of topics; a
  missing or extra topic fails the tests.
- **Examples.** Give every task that runs offline a runnable `command` example, one command per example: no pipes,
  redirection, or `&&`. Wrap `content` in single quotes and use double quotes inside the command, which bash,
  PowerShell, and cmd read alike. Quote a response file argument as `"@apimark.rsp"`, because PowerShell reads a
  bare `@` as splatting. State the result when it matters with `exit:` (default 0) and `expect:`, a short fragment
  that must appear somewhere in the console output; `expect` cannot check a file that the command writes, and an
  argument error ends with a .NET suffix, so stop the fragment before it. Keep each `description` to about 50
  characters.
- **C++ examples.** The C++ front end needs clang, which the build machines of contributors may not have. Make
  every C++ example that runs clang a `snippet` example (with `save-as`), so that the example runner does not run
  it; only C++ errors found before clang is searched for may be `command` examples.
- **Getting started.** Keep the install guidance for CI: the .NET version the tool needs (from
  `TargetFrameworks`), a local tool manifest, the global install as the alternative, and the complete GitHub
  Actions `ci-steps` snippet with `actions/setup-dotnet`, `dotnet tool restore`, and a `dotnet apimark` step. Mark
  the install and CI examples as `snippet` examples.
- **Input files.** Show complete files only for formats that users author, such as VHDL sources with doc comments,
  response files, and MSBuild properties, and describe what the tool produces (the Markdown pages, TRX, JUnit) in
  prose. Input files that the examples read but users do not author live in
  `test/ApiMark.Tool.Tests/TestData/HelpExamples/` (for example `counter.vhd`). The example-runner test in
  `IntegrationTests.cs` copies them into `invocation.WorkingDirectory` before each command, together with the
  self-validation sample assembly written by `Validation.WriteSampleAssembly` as `MyLib.dll` and `MyLib.xml`, which
  the .NET examples read. A file that the card saves from its own `source` example wins over a staged copy of the
  same name. A card cannot save a file whose name starts with a dot or ends in `.md`.
- **Width.** A printed card may not be wider than 120 characters. Links print as full commands (`[[#name]]` as
  about 50 characters) and `{{version}}` of a development build as about 46, so keep body lines near 75 characters
  with at most one link each.
- **Text.** Cards are linted and spell-checked like any Markdown file: no body line starting with `#`, no wide
  tables, and every fenced block names its language. Rephrase a word that TextLint does not know rather than adding
  it to the word list.

Run `pwsh ./build.ps1 -Changed` after changing a card. The `HelpCommand` unit tests validate every card, with
warnings treated as errors, and check its printed width; an integration test runs every command example against
the built tool and compares the result with the card.

## Reporting Bugs

Open a [GitHub Issue](https://github.com/DemaConsulting/ApiMark/issues) with
a minimal reproduction case and the full error output.

## License

By contributing you agree that your contributions will be licensed under the
[MIT License](https://github.com/DemaConsulting/ApiMark/blob/main/LICENSE).
