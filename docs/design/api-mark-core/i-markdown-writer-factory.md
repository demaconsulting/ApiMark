## IMarkdownWriterFactory

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

IMarkdownWriterFactory is the factory contract for creating per-file Markdown
writers. It is injected into IApiGenerator.Generate so that language generators
can open individual output files without knowing where or how those files are
stored. The file-system implementation (`FileMarkdownWriterFactory`) writes to
disk; test doubles capture writes in memory.

### Data Model

N/A — IMarkdownWriterFactory is an interface with no fields or properties of its
own. Implementing classes hold the output root (e.g. a directory path or an
in-memory dictionary) internally.

### Key Methods

**IMarkdownWriterFactory.CreateMarkdown**: Creates a writer for a single output file.

- *Parameters*: `string subFolder` — subfolder path relative to the output root;
  pass an empty string for a root-level file. `string name` — file name without
  extension.
- *Returns*: `IMarkdownWriter` — a new writer positioned at the start of the file.
- *Postconditions*: The output directory (including any subFolder) is created if
  it does not exist. The returned writer is ready for write calls. The caller is
  responsible for disposing the returned writer.

### Error Handling

IMarkdownWriterFactory itself defines no error-handling contract; it is an
interface. Implementing classes are responsible for handling I/O errors (for
example, UnauthorizedAccessException when the output root is not writable) and
documenting their error behavior in their own unit designs.

### Dependencies

N/A — IMarkdownWriterFactory is an interface defined in ApiMarkCore; it has no
dependencies on other units, OTS items, or shared packages.

### Callers

- **DotNetGenerator** — receives an IMarkdownWriterFactory via IApiGenerator.Generate
  and calls CreateMarkdown once per output file produced during generation.
- **ApiMarkTask** — constructs a `FileMarkdownWriterFactory(outputDirectory)` and
  passes it to the generator's Generate method.
- **Program** — constructs a `FileMarkdownWriterFactory(outputDirectory)` from CLI
  options and passes it to the generator's Generate method.
