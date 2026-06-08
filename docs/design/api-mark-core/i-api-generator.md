## IApiGenerator

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

IApiGenerator is the contract every language-specific generator must implement.
It decouples ApiMarkTool from any concrete language module. A caller constructs a
generator, optionally configures it at construction time, and then calls Generate —
the caller never needs to know which language it is processing. ApiMarkMsbuild
invokes the generator indirectly by spawning ApiMarkTool as a child process.

### Data Model

N/A — IApiGenerator is an interface with no fields or properties of its own.
Language-specific options are passed at construction time of the implementing class
and are not exposed through this interface.

### Key Methods

**IApiGenerator.Generate**: Generates the full Markdown documentation tree for a
configured software component.

- *Parameters*: `IMarkdownWriterFactory factory` — the factory used to create
  per-file markdown writers for each output file; `IContext context` — the output
  channel used to emit informational and error messages during generation.
- *Returns*: `void`
- *Preconditions*: The implementing class must have been constructed with valid
  options; `factory` and `context` must be non-null, configured instances.
- *Postconditions*: The output contains a complete Markdown tree. A file named
  `api.md` MUST be created via `factory.CreateMarkdown("", "api")` as the fixed
  top-level entrypoint. The output directory is created by the factory if it does
  not already exist.

### Error Handling

IApiGenerator itself defines no error-handling contract; it is an interface.
Implementing classes are responsible for throwing appropriate exceptions (for
example, FileNotFoundException when input files are missing) and documenting
their error behavior in their own unit designs.

### Dependencies

N/A — IApiGenerator is an interface defined in ApiMarkCore; it has no dependencies
on other units, OTS items, or shared packages.

### Callers

- **ApiMarkTask** — spawns ApiMark.Tool as a child process, within which the
  appropriate IApiGenerator implementation is constructed and Generate is called.
  ApiMarkTask does not call IApiGenerator directly.
- **Program** — constructs the appropriate IApiGenerator for the requested language
  subcommand and calls Generate with a `FileMarkdownWriterFactory` built from CLI
  options.
