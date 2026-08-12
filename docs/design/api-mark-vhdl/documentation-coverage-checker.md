## DocumentationCoverageChecker

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

`DocumentationCoverageChecker` scans the file models parsed by
`VhdlGenerator` for entities, ports, generics, packages, and package-level
exported declarations (types, constants, components, subprograms) that lack a
documentation summary. It powers the opt-in `--enforce-docs` CLI flag for the
`vhdl` language.

**Known v1 simplifications (deliberate, not bugs)**:

- **Public-interface-only scope.** VHDL has no accessibility/visibility
  concept analogous to C#/C++ access modifiers, so this checker has no
  visibility tier parameter at all — it always checks the same set of
  declarations regardless of the caller-supplied `--enforce-docs` value.
  `VhdlGenerator.CheckDocumentationCoverage` accepts `Public`,
  `PublicAndProtected`, and `All` purely for CLI vocabulary consistency with
  the `dotnet` and `cpp` subcommands, then discards the parsed value — all
  three behave identically.
- **Architecture internals are not checked.** `VhdlArchitectureDecl` carries
  only its own `Doc` — internal signals, variables, and processes are not
  parsed into the AST model today, so there is nothing for this checker to
  walk beyond the architecture declaration itself. This checker DOES check
  the architecture's own summary (an architecture is a named, referenceable
  declaration analogous to a class), but does NOT check anything inside it.
  Enforcing documentation on architecture-internal signals/processes is a
  deferred future enhancement.
- **CLI-only — no MSBuild support.** `ApiMarkTask` (MSBuild) has no VHDL
  language support at all today; VHDL documentation-coverage enforcement is
  reachable only through the `ApiMark.Tool` CLI directly.

### Data Model

Reuses the shared `ApiMark.Core.DocumentationCoverageResult` and
`ApiMark.Core.UndocumentedApiItem` types (see the IDocumentationCoverageCapable
design document under the ApiMarkCore subsystem).
`Kind` values produced by this checker: `Entity`, `Generic`, `Port`,
`Architecture`, `Package`, `Type`, `Constant`, `Component`, `Subprogram`.

**"Documented" bar (v1)**: A declaration is considered documented when its
`VhdlDocComment.Summary` is a non-null, non-whitespace string, mirroring the
.NET and C++ checkers.

### Key Methods

**Check** (internal static): Scans `fileModels` for entities (and their
generics/ports), architectures (own summary only), and packages (and their
types/constants/components/subprograms) that lack a non-empty documentation
summary.

- *Parameters*: `IReadOnlyList<VhdlFileModel> fileModels` — the file models
  collected by `VhdlGenerator.Parse`. May contain fewer entries than the
  number of source files scanned, since `Parse` tolerates and logs per-file
  parse failures without adding a model for the failed file.
- *Preconditions*: `fileModels` must be non-null (may be empty).
- *Postconditions*: returns a `DocumentationCoverageResult` describing every
  undocumented declaration found and the total number of declarations
  checked. Does not mutate `fileModels`.
- *Algorithm*: for each file model, checks each `Entity` (and its `Generics`
  and `Ports`) via `CheckEntity`, each `Architecture`'s own summary directly,
  and each `Package` (and its `Types`, `Constants`, `Components`,
  `Subprograms`) via `CheckPackage`.

**CheckEntity** (private): Checks a single entity for a missing summary, then
checks each of its generics and ports.

**CheckPackage** (private): Checks a single package for a missing summary,
then checks each of its types, constants, components, and subprograms.

### Error Handling

`Check` does not throw for missing documentation — every undocumented item is
simply recorded in the result rather than raising an exception. A `null`
`Doc` and an empty/whitespace-only `Doc.Summary` are treated identically as
undocumented.

### Dependencies

- **VhdlAstModel** — `VhdlFileModel`, `VhdlEntityDecl`, `VhdlArchitectureDecl`,
  `VhdlPackageDecl`, `VhdlGenericDoc`, `VhdlPortDoc`, `VhdlTypeDecl`,
  `VhdlConstantDecl`, `VhdlComponentDecl`, `VhdlSubprogramDecl`,
  `VhdlDocComment`.
- **ApiMark.Core** — `DocumentationCoverageResult`, `UndocumentedApiItem`.

### Callers

- **VhdlGenerator.CheckDocumentationCoverage** — the sole caller. Must be
  invoked after `Parse()` has completed. Throws `InvalidOperationException`
  when called before `Parse()` or when no enforcement tier is configured;
  throws `ArgumentException` for an unrecognized tier value (validated using
  the same `Public`/`PublicAndProtected`/`All` vocabulary as .NET/C++ purely
  for a consistent CLI experience, even though the parsed value itself is
  discarded).
- **Program.ReportDocumentationCoverage** (indirectly, via
  `VhdlGenerator.CheckDocumentationCoverage`) — reports the resulting
  `DocumentationCoverageResult` to the console and sets the process exit
  code when severity is `Error` and violations were found.

### External Interfaces

N/A — this is an internal class with no external interfaces exposed beyond
its assembly. It is surfaced to end users only indirectly, through the
`--enforce-docs`/`--enforce-docs-severity` CLI options (CLI-only — no MSBuild
support for VHDL today).
