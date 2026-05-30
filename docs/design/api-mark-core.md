# ApiMarkCore

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

## Architecture

ApiMarkCore is a shared-contracts system. It defines the interfaces and output
conventions that all other systems depend on. There is no system-level executable
logic — the system exists to give callers a single, stable definition of the
generation interface and the markdown-writing interfaces.

```mermaid
flowchart TD
    IApiGenerator
    IMarkdownWriterFactory
    IMarkdownWriter
    FileMarkdownWriterFactory --> |implements| IMarkdownWriterFactory
    FileMarkdownWriter --> |implements| IMarkdownWriter
    IMarkdownWriterFactory --> |creates| IMarkdownWriter
```

ApiMarkDotNet implements IApiGenerator and calls IMarkdownWriterFactory to create
per-file IMarkdownWriter instances. ApiMarkMsbuild and ApiMarkTool consume
IApiGenerator. No system depends on ApiMarkCore beyond these interfaces.

## External Interfaces

**IApiGenerator (provided)**: Public interface contract for any language generator.

- *Type*: In-process .NET public API.
- *Role*: Provider — ApiMarkCore publishes this interface; ApiMarkDotNet implements
  it; ApiMarkMsbuild and ApiMarkTool consume it.
- *Contract*: `void Generate(IMarkdownWriterFactory factory)` — writes the complete
  Markdown tree for a configured software component using the supplied factory. The
  output MUST include a file named `api.md` as the fixed entrypoint.
- *Constraints*: The implementing class creates output directories as needed;
  callers supply a valid, configured factory.

**IMarkdownWriterFactory (provided)**: Factory interface for creating per-file Markdown writers.

- *Type*: In-process .NET public API.
- *Role*: Provider — ApiMarkCore publishes this interface; callers inject it into
  Generate; language generators call it to open individual output files.
- *Contract*: `IMarkdownWriter CreateMarkdown(string subFolder, string name)` —
  creates and returns a writer for the file at `subFolder/name.md`. Pass an empty
  string for subFolder to create a root-level file.
- *Constraints*: The caller is responsible for disposing each returned IMarkdownWriter.
  The factory creates output directories as needed.

**IMarkdownWriter (provided)**: Per-file Markdown writing interface.

- *Type*: In-process .NET public API (IDisposable).
- *Role*: Provider — ApiMarkCore publishes this interface; language generators call
  its write methods to append structured content; implementations flush and close
  the underlying file on Dispose.
- *Contract*: WriteHeading, WriteSignature, WriteParagraph, WriteTable,
  WriteCodeBlock, WriteLink methods — see IMarkdownWriter Unit Design for full
  signatures.
- *Constraints*: Each method appends content to the current output file in call
  order; callers invoke methods in document order and dispose the writer when done.

## Dependencies

N/A — ApiMarkCore has no dependencies on other systems, OTS items, or shared
packages.

## Risk Control Measures

N/A — not a safety-classified software item.

## Data Flow

ApiMarkCore does not process data at runtime. Its contribution to the overall data
flow is:

1. Language generators write Markdown content by calling IMarkdownWriter methods in
   document order.
2. Callers (ApiMarkMsbuild and ApiMarkTool) invoke IApiGenerator.Generate to
   trigger generation for a configured component.

## Design Constraints

- Platform: targets .NET 8 as a class library; no platform-specific code.
- No in-memory document model: Core defines only interfaces and their file-system
  implementations; language-specific generators own all in-memory state.
- Stable API surface: changes to IApiGenerator, IMarkdownWriterFactory, or
  IMarkdownWriter method signatures require corresponding updates in all
  implementing systems.
