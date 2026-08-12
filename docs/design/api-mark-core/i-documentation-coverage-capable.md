## IDocumentationCoverageCapable

![IDocumentationCoverageCapable Structure](ApiMarkCoreView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

IDocumentationCoverageCapable is an optional capability interface implemented
by language generators that support documentation-coverage enforcement —
scanning the API surface parsed by the most recent `Parse` call for
declarations missing a documentation summary. `ApiMark.Tool`'s `Program.cs`
detects the capability via `generator is IDocumentationCoverageCapable`
instead of downcasting to a specific language generator type, so the
`--enforce-docs`/`--enforce-docs-severity` CLI wiring (and the
`ApiMarkEnforceDocs`/`ApiMarkEnforceDocsSeverity` MSBuild properties, for the
languages that support MSBuild) stays language-agnostic as new languages
adopt the capability.

Implemented by `DotNetGenerator` (ApiMarkDotNet), `CppGenerator` (ApiMarkCpp),
and `VhdlGenerator` (ApiMarkVhdl). Each implementation owns parsing and
validating its own `enforceTier` vocabulary: .NET and C++ interpret it as a
three-tier visibility (`Public`, `PublicAndProtected`, `All`) mapped onto
their respective accessibility models; VHDL has no visibility concept at all,
so it accepts the same three-word vocabulary purely for CLI consistency but
treats all three values identically.

### Data Model

**DocumentationCoverageResult** (public sealed class): The immutable result
of a documentation-coverage scan. Exposes `UndocumentedItems` (the list of
violations), `CheckedCount` (total declarations examined, documented or not),
`UndocumentedCount`, and `HasViolations`.

**UndocumentedApiItem** (public sealed record): A single violation — `Kind`
(`string`) and `DisplayName` (`string`). `Kind` is a plain, language-owned
display label (e.g. `"Type"`, `"Function"`, `"Entity"`) rather than a shared
enum, since each language checker defines its own vocabulary of declaration
kinds and the sole consumer of `Kind` (`Program.cs`) interpolates it directly
into a display string without switching on it.

### Key Methods

**IDocumentationCoverageCapable.CheckDocumentationCoverage**: Scans the API
surface parsed by the most recent `Parse` call for declarations missing a
documentation summary.

- *Parameters*: `string? enforceTier` — the raw enforcement tier string
  supplied by the caller (typically the CLI `--enforce-docs` value), e.g.
  `"Public"`. May be null or empty, in which case implementations fall back
  to their own construction-time configured tier.
- *Returns*: `DocumentationCoverageResult` — describing every undocumented
  declaration found and the total number of declarations checked.
- *Preconditions*: the implementing class must have already had `Parse`
  called successfully.
- *Postconditions*: does not mutate any previously parsed state.

### Error Handling

IDocumentationCoverageCapable itself defines no error-handling contract; it
is an interface. Implementing classes throw `ArgumentException` when
`enforceTier` (or their own fallback configuration) is not a value recognized
by their enforcement vocabulary, and `InvalidOperationException` when called
before `Parse` has completed successfully or when no enforcement tier is
configured by either the parameter or the implementation's own options.

### Dependencies

- **DocumentationCoverageResult / UndocumentedApiItem** — the shared result
  types returned by `CheckDocumentationCoverage`.

### Callers

- **Program** (`ApiMark.Tool`) — after calling `IApiGenerator.Parse`, checks
  whether the returned generator also implements
  `IDocumentationCoverageCapable`; when `--enforce-docs` is configured, calls
  `CheckDocumentationCoverage` and reports the resulting
  `DocumentationCoverageResult` via `Program.ReportDocumentationCoverage`,
  setting the process exit code when severity is `Error` and violations were
  found.
- **ApiMarkTask** (`ApiMark.MSBuild`) — forwards `ApiMarkEnforceDocs` and
  `ApiMarkEnforceDocsSeverity` MSBuild properties as `--enforce-docs`/
  `--enforce-docs-severity` CLI arguments when spawning `ApiMark.Tool` for the
  `dotnet` and `cpp` languages. VHDL enforcement has no MSBuild wiring today
  — `ApiMarkTask` has no VHDL language support at all, so VHDL enforcement is
  reachable only through the `ApiMark.Tool` CLI directly.
