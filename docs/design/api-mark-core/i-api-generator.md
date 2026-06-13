## IApiGenerator

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

IApiGenerator is the first stage of the two-stage generation pipeline. It is
the contract every language-specific generator must implement. A caller
constructs a generator, optionally configures it at construction time, and then
calls `Parse` to obtain an `IApiEmitter` that holds all parsed symbol data.
The caller then calls `IApiEmitter.Emit` with the desired output format and
heading depth. This separation decouples symbol parsing from output formatting
and allows the same parsed model to be emitted into different formats.

ApiMarkMsbuild invokes the generator indirectly by spawning ApiMarkTool as a
child process. ApiMarkTool never shares an in-process parsed model across
multiple Emit calls.

### Data Model

N/A — IApiGenerator is an interface with no fields or properties of its own.
Language-specific options are passed at construction time of the implementing
class and are not exposed through this interface.

### Key Methods

**IApiGenerator.Parse**: Parses the configured software component and returns
an `IApiEmitter` that holds all symbol data needed to produce documentation.

- *Parameters*: `IContext context` — the output channel used to emit
  informational and error messages during parsing.
- *Returns*: `IApiEmitter` — a fully populated emitter ready to call `Emit`.
- *Preconditions*: The implementing class must have been constructed with valid
  options; `context` must be a non-null, configured instance.
- *Postconditions*: The returned `IApiEmitter` holds all parsed symbol data.
  No output files have been written; all I/O is deferred to `IApiEmitter.Emit`.

### Error Handling

IApiGenerator itself defines no error-handling contract; it is an interface.
Implementing classes are responsible for throwing appropriate exceptions (for
example, FileNotFoundException when input files are missing) and documenting
their error behavior in their own unit designs.

### Dependencies

- **IApiEmitter** — IApiGenerator.Parse returns an IApiEmitter instance.

### Callers

- **ApiMarkTask** — spawns ApiMark.Tool as a child process, within which the
  appropriate IApiGenerator implementation is constructed and Parse is called,
  followed by IApiEmitter.Emit. ApiMarkTask does not call IApiGenerator directly.
- **Program** — constructs the appropriate IApiGenerator for the requested
  language subcommand and calls Parse, then calls Emit on the returned emitter
  with a `FileMarkdownWriterFactory` and `EmitConfig` built from CLI options.
