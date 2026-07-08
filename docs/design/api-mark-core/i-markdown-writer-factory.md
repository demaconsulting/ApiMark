## IMarkdownWriterFactory

![IMarkdownWriterFactory Structure](ApiMarkCoreView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

This document covers the `IMarkdownWriterFactory` interface contract and the
`FileMarkdownWriterFactory` concrete implementation.

IMarkdownWriterFactory is the factory contract for creating per-file Markdown
writers. It is passed to `IApiEmitter.Emit` so that language generators
can open individual output files without knowing where or how those files are
stored. The file-system implementation (`FileMarkdownWriterFactory`) writes to
disk; test doubles capture writes in memory.

### Data Model

`IMarkdownWriterFactory` is an interface with no fields or properties of its own.
`FileMarkdownWriterFactory` holds one private field:

- `_outputDirectory: string` — absolute or relative path to the root output directory,
  set during construction and immutable thereafter. The directory need not exist at
  construction time; it is created on the first `CreateMarkdown` call.

### Key Methods

**IMarkdownWriterFactory.CreateMarkdown**: Creates a writer for a single output file.

- *Parameters*: `string subFolder` — subfolder path relative to the output root;
  pass an empty string for a root-level file. `string name` — file name without
  extension.
- *Preconditions*: `name` must not be null, empty, or whitespace.
- *Returns*: `IMarkdownWriter` — a new writer positioned at the start of the file.
- *Postconditions*: The output directory (including any subFolder) is created if
  it does not exist. The returned writer is ready for write calls. The caller is
  responsible for disposing the returned writer.

**FileMarkdownWriterFactory constructor**: Initializes the file-system factory.

- *Parameters*: `string outputDirectory` — absolute or relative path to the root
  output directory.
- *Preconditions*: `outputDirectory` must not be null, empty, or whitespace.
- *Postconditions*: `_outputDirectory` is stored; the directory is not created at
  construction time.
- *Throws*: `ArgumentException` when `outputDirectory` is null, empty, or whitespace.

**FileMarkdownWriterFactory.CreateMarkdown**: Combines `_outputDirectory` with `subFolder`
using `PathHelpers.SafePathCombine` (path-traversal checked), calls
`Directory.CreateDirectory` on the resulting target path, then opens a UTF-8
`StreamWriter` at `{targetDirectory}/{name}.md` and wraps it in a `FileMarkdownWriter`.

### Error Handling

`IMarkdownWriterFactory` itself defines no error-handling contract; it is an
interface. `FileMarkdownWriterFactory` handles errors as follows:

- **Constructor**: throws `ArgumentException` when `outputDirectory` is null,
  empty, or whitespace so that callers receive a clear diagnostic rather than an
  obscure I/O failure at file-creation time.
- **CreateMarkdown**: throws `ArgumentException` when `name` is null, empty, or
  whitespace. Path segments are combined via `PathHelpers.SafePathCombine`; if
  `subFolder` or `name` resolves outside the output root (e.g. via `../` traversal
  or a rooted path), an `ArgumentException` is thrown. I/O exceptions from
  `Directory.CreateDirectory` or the `StreamWriter` constructor (for example,
  `UnauthorizedAccessException` when the output root is not writable) are propagated
  to the caller unchanged.

### Dependencies

The `IMarkdownWriterFactory` interface itself has no dependencies; the following apply to `FileMarkdownWriterFactory`:

| Dependency | Role |
| --- | --- |
| `PathHelpers` | Used via `SafePathCombine` to safely resolve the output file path |
| `FileMarkdownWriter` | Constructed and returned as the concrete writer for each output file |

### Callers

- **DotNetGenerator** — receives an IMarkdownWriterFactory passed to `IApiEmitter.Emit`
  and calls CreateMarkdown once per output file produced during generation.
- **CppGenerator** — receives an IMarkdownWriterFactory passed to `IApiEmitter.Emit`
  and calls CreateMarkdown once per output file produced during generation.
- **VhdlGenerator** — receives an IMarkdownWriterFactory passed to `IApiEmitter.Emit`
  and calls CreateMarkdown once per output file produced during generation.
- **ApiMarkTask** — spawns ApiMark.Tool as a child process, within which Program
  creates the `FileMarkdownWriterFactory`. ApiMarkTask does not use IMarkdownWriterFactory
  directly.
- **Program** — constructs a `FileMarkdownWriterFactory(outputDirectory)` from CLI
  options and passes it to `IApiEmitter.Emit`.
