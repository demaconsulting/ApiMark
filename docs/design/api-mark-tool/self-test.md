## SelfTest

![SelfTest Structure](SelfTestView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Overview

The SelfTest subsystem runs in-process self-validation tests to verify that
ApiMarkTool is functioning correctly in its deployment environment. When
`--validate` is passed to the CLI, Program dispatches to `Validation.Run`,
which executes a fixed set of functional tests and reports pass/fail results
through the Context. Operators and CI pipelines can use this to confirm
tool integrity without requiring external test infrastructure or the full
source tree.

### Interfaces

**Provided**:

- `Validation.Run(Context context)` — static entry point. Accepts the
  current program context, runs all self-tests, writes results through
  `context.WriteLine` and `context.WriteError`, and optionally serializes
  results to a `.trx` or `.xml` file specified by `context.ResultsFile`.

**Consumed**:

- `Context` (from the Cli subsystem) — used for output routing and to
  read `context.ResultsFile` and `context.HeadingDepth`.
- `DemaConsulting.TestResults` — used to build the test results collection
  and serialize it to TRX or JUnit XML format.

### Design

The SelfTest subsystem contains one unit: `Validation` (see Validation Unit
Design).

Each self-test creates a child `Context` instance with `--silent` and
`--log <tempFile>` so that the tool's own dispatch path runs in isolation
and output is captured to a temporary file for inspection. Tests never
pass `--validate` to the child context in order to avoid recursion.

`context.WriteError` is called—setting `context.ExitCode` to `1`—when any
self-test fails, when the results file extension is unsupported, or when the
results file cannot be written. The subsystem produces no side effects beyond
console output, log file writes, and an optional results file.
