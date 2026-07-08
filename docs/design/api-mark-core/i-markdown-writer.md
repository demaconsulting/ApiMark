## IMarkdownWriter

![IMarkdownWriter Structure](../generated/ApiMarkCoreView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

IMarkdownWriter is the per-file Markdown writing contract. It is obtained from
IMarkdownWriterFactory.CreateMarkdown and used by language generators to append
structured content to a single output file. Callers invoke write methods in
document order and dispose the writer when finished; the implementation flushes
and closes the underlying file on Dispose.

### Data Model

`IMarkdownWriter` is an interface with no fields or properties of its own. The file-system
implementation, `FileMarkdownWriter`, holds:

- `_writer` (`StreamWriter`, read-only): the underlying stream writer opened over the output file;
  ownership transfers to `FileMarkdownWriter` on construction and is disposed when the writer is
  disposed.
- `_disposed` (`bool`): tracks whether `Dispose` has been called; prevents double-disposal and
  guards each write method against use-after-dispose.

### Key Methods

**IMarkdownWriter.WriteHeading**: Writes a Markdown heading at the specified depth.

- *Parameters*: `int level` — heading depth (1–6); `string text` — heading text.
- *Returns*: `void`
- *Preconditions*: `level` must be between 1 and 6 inclusive; `text` must not be
  null or empty.
- *Postconditions*: A Markdown heading line (`# text` through `###### text`) is
  appended to the current output file.

**IMarkdownWriter.WriteSignature**: Writes a code-fenced API signature block.

- *Parameters*: `string language` — fence language tag (e.g. `csharp`);
  `string code` — the formatted signature text.
- *Returns*: `void`
- *Preconditions*: `language` and `code` must not be null.
- *Postconditions*: A fenced code block is appended to the current output file.

**IMarkdownWriter.WriteParagraph**: Writes a prose paragraph.

- *Parameters*: `string text` — paragraph content.
- *Returns*: `void`
- *Preconditions*: `text` must not be null.
- *Postconditions*: A blank-line-delimited paragraph is appended to the current
  output file.

**IMarkdownWriter.WriteTable**: Writes a Markdown table.

- *Parameters*: `string[] headers` — column header labels;
  `IEnumerable<string[]> rows` — row data, each inner array containing one cell
  per column.
- *Returns*: `void`
- *Preconditions*: `headers` must not be null and must contain at least one element;
  `rows` must not be null; each row must contain the same number of elements as
  `headers`; no element in `headers` or any row may be null.
- *Postconditions*: A pipe-delimited Markdown table is appended to the current
  output file. Literal pipe characters (`|`) in cell content are escaped as `\|`.

**IMarkdownWriter.WriteCodeBlock**: Writes a fenced code example block.

- *Parameters*: `string language` — fence language tag; `string code` — code text.
- *Returns*: `void`
- *Preconditions*: `language` and `code` must not be null.
- *Postconditions*: A fenced code block is appended to the current output file.

**IMarkdownWriter.WriteLink**: Writes a relative navigation link to another documentation file.

- *Parameters*: `string text` — display label; `string relativePath` — path to the
  linked file relative to the current output file.
- *Returns*: `void`
- *Preconditions*: `text` must not be null or empty; `relativePath` must not be null.
- *Postconditions*: A Markdown inline link of the form `[text](relativePath)` is
  appended to the current output file.

**IMarkdownWriter.Dispose** (from IDisposable): Flushes and closes the underlying
output file.

- *Postconditions*: All buffered content is written to the file; the file is closed
  and the writer must not be used after disposal.

### Error Handling

`IMarkdownWriter` itself defines no error-handling contract; it is an interface.

Implementations must throw `ArgumentNullException` or `ArgumentException` when
required preconditions are violated (null arguments, empty heading text, out-of-range
heading level, mismatched row length, or an empty headers array).

`FileMarkdownWriter` does not catch I/O errors. Any `IOException` raised by the
underlying `StreamWriter` (for example, if the output directory is not writable or
the disk is full) propagates directly to the caller. Callers are responsible for
ensuring the output path is accessible before requesting a writer from
`FileMarkdownWriterFactory`.

Each write method guards against use-after-dispose by calling
`ObjectDisposedException.ThrowIf` at the start of the method body; any call made
after `Dispose` throws `ObjectDisposedException`.

### Dependencies

N/A - IMarkdownWriter is an interface defined in ApiMarkCore; it has no dependencies
on other units, OTS items, or shared packages.

### Callers

- **DotNetGenerator** — calls IMarkdownWriter write methods to emit Markdown content
  for each type and member discovered in the assembly. Writers are obtained from
  the IMarkdownWriterFactory passed to `IApiEmitter.Emit` and disposed after
  each file is complete.
- **CppGenerator** — calls IMarkdownWriter write methods to emit Markdown content
  for each namespace, type, and member discovered in the public headers. Writers
  are obtained from the IMarkdownWriterFactory passed to `IApiEmitter.Emit`
  and disposed after each file is complete.
- **VhdlGenerator** — calls IMarkdownWriter write methods to emit Markdown content
  for each entity and package discovered in the VHDL source files. Writers are
  obtained from the IMarkdownWriterFactory passed to `IApiEmitter.Emit` and
  disposed after each file is complete.
