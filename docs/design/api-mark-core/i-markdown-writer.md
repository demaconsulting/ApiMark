## IMarkdownWriter

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

IMarkdownWriter is the per-file Markdown writing contract. It is obtained from
IMarkdownWriterFactory.CreateMarkdown and used by language generators to append
structured content to a single output file. Callers invoke write methods in
document order and dispose the writer when finished; the implementation flushes
and closes the underlying file on Dispose.

### Data Model

N/A — IMarkdownWriter is an interface with no fields or properties of its own.
Implementing classes manage the underlying file stream or string builder internally.

### Key Methods

**IMarkdownWriter.WriteHeading**: Writes a Markdown heading at the specified depth.

- *Parameters*: `int level` — heading depth (1–4); `string text` — heading text.
- *Returns*: `void`
- *Preconditions*: `level` must be between 1 and 4 inclusive.
- *Postconditions*: A Markdown heading line (`# text` through `#### text`) is
  appended to the current output file.

**IMarkdownWriter.WriteSignature**: Writes a code-fenced API signature block.

- *Parameters*: `string language` — fence language tag (e.g. `csharp`);
  `string code` — the formatted signature text.
- *Returns*: `void`
- *Postconditions*: A fenced code block is appended to the current output file.

**IMarkdownWriter.WriteParagraph**: Writes a prose paragraph.

- *Parameters*: `string text` — paragraph content.
- *Returns*: `void`
- *Postconditions*: A blank-line-delimited paragraph is appended to the current
  output file.

**IMarkdownWriter.WriteTable**: Writes a Markdown table.

- *Parameters*: `string[] headers` — column header labels;
  `IEnumerable<string[]> rows` — row data, each inner array containing one cell
  per column.
- *Returns*: `void`
- *Postconditions*: A pipe-delimited Markdown table is appended to the current
  output file.

**IMarkdownWriter.WriteCodeBlock**: Writes a fenced code example block.

- *Parameters*: `string language` — fence language tag; `string code` — code text.
- *Returns*: `void`
- *Postconditions*: A fenced code block is appended to the current output file.

**IMarkdownWriter.WriteLink**: Writes a relative file reference as prose text.

- *Parameters*: `string text` — display label; `string relativePath` — path to the
  linked file relative to the current output file.
- *Returns*: `void`
- *Postconditions*: A reference entry is appended to the current output file.

**IMarkdownWriter.Dispose** (from IDisposable): Flushes and closes the underlying
output file.

- *Postconditions*: All buffered content is written to the file; the file is closed
  and the writer must not be used after disposal.

### Error Handling

IMarkdownWriter itself defines no error-handling contract; it is an interface.
Implementing classes are responsible for handling I/O errors (for example,
IOException when the output directory is not writable) and documenting their error
behavior in their own unit designs.

### Dependencies

N/A — IMarkdownWriter is an interface defined in ApiMarkCore; it has no dependencies
on other units, OTS items, or shared packages.

### Callers

- **DotNetGenerator** — calls IMarkdownWriter write methods to emit Markdown content
  for each type and member discovered in the assembly. Writers are obtained from
  the IMarkdownWriterFactory passed to IApiGenerator.Generate and disposed after
  each file is complete.
