## IApiEmitter

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

IApiEmitter is the second stage of the two-stage generation pipeline. An
`IApiEmitter` instance holds all parsed symbol data collected by
`IApiGenerator.Parse` and is responsible for writing the complete Markdown
output tree when its `Emit` method is called. Separating parse from emit allows
the caller to choose the output format and heading depth at emit time rather
than at parse time, and enables future scenarios where the same parsed model is
emitted into multiple formats in one pass.

### Data Model

N/A — IApiEmitter is an interface with no fields or properties of its own.
Parsed symbol state is held by the private nested emitter class that each
language module returns from `IApiGenerator.Parse`.

### Key Methods

**IApiEmitter.Emit**: Writes the full Markdown documentation tree for the
previously parsed component using the supplied factory and configuration.

- *Parameters*:
  - `IMarkdownWriterFactory factory` — factory used to create per-file Markdown
    writers for each output file; must not be null.
  - `EmitConfig config` — output format and heading-depth settings; must not be
    null.
  - `IContext context` — output channel for informational and error messages;
    must not be null.
- *Returns*: `void`
- *Preconditions*: The emitter was returned by a successful call to
  `IApiGenerator.Parse`; `factory`, `config`, and `context` must be non-null,
  configured instances.
- *Postconditions*: The output contains a complete Markdown documentation tree.
  For `OutputFormat.GradualDisclosure`, a file named `api.md` MUST be created
  via `factory.CreateMarkdown("", "api")` as the fixed top-level entrypoint and
  all namespace, type, and member pages are created as separate files. For
  `OutputFormat.SingleFile`, a single file named `api.md` is created via
  `factory.CreateMarkdown("", "api")` containing all content.

### Error Handling

IApiEmitter itself defines no error-handling contract; it is an interface.
Implementing classes are responsible for throwing appropriate exceptions (for
example, `ArgumentNullException` when `factory` is null) and documenting their
error behavior in their own unit designs.

### Dependencies

- **EmitConfig** — IApiEmitter.Emit receives an `EmitConfig` instance that
  controls the output format and heading depth.
- **IMarkdownWriterFactory** — IApiEmitter.Emit receives an
  `IMarkdownWriterFactory` and calls `CreateMarkdown` to obtain each
  `IMarkdownWriter`.
- **IContext** — IApiEmitter.Emit receives an `IContext` for diagnostic output.

### Callers

- **Program** — calls `IApiEmitter.Emit` with a `FileMarkdownWriterFactory`
  built from CLI options and an `EmitConfig` built from the parsed `Context`.
- **ApiMarkTask** — spawns ApiMark.Tool as a child process, within which
  `IApiEmitter.Emit` is called. ApiMarkTask does not call IApiEmitter directly.
