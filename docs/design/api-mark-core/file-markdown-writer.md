## FileMarkdownWriter

![FileMarkdownWriter Structure](../generated/ApiMarkCoreView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

FileMarkdownWriter is the production file-system implementation of `IMarkdownWriter`.
It writes Markdown content to an underlying `StreamWriter` opened over an output file.
Instances are created exclusively by `FileMarkdownWriterFactory` and are not directly
constructable by external callers. Each instance owns its `StreamWriter` and flushes
and closes it on disposal.

### Data Model

`FileMarkdownWriter` is an `internal sealed class` with two private fields:

- `_writer` (`StreamWriter`, private readonly): the underlying stream writer this
  instance writes Markdown content to. Ownership is transferred at construction;
  the writer is disposed when this instance is disposed.
- `_disposed` (`bool`, private): tracks whether `Dispose` has been called, to
  prevent double-disposal and to enforce the use-after-dispose contract.

### Key Methods

**FileMarkdownWriter.WriteHeading(int level, string text)**: Writes an ATX Markdown
heading at the specified level.

- *Parameters*: `int level` — heading level in the range 1–6. `string text` — heading
  text to display.
- *Algorithm*: Writes a string of `level` `#` characters followed by a space and
  `text`, then emits a blank line to separate the heading from the next block.
- *Throws*: `ObjectDisposedException` if disposed. `ArgumentOutOfRangeException`
  if `level` is less than 1 or greater than 6.

**FileMarkdownWriter.WriteSignature(string language, string code)**: Writes a fenced
code block representing an API signature.

- *Parameters*: `string language` — language identifier for the fence label.
  `string code` — signature text.
- *Algorithm*: Writes `` ``` ``language``, then `code`, then ` ``` `, then a blank line.
- *Throws*: `ObjectDisposedException` if disposed.

**FileMarkdownWriter.WriteParagraph(string text)**: Writes a prose paragraph.

- *Parameters*: `string text` — paragraph body.
- *Algorithm*: Writes `text` followed by a blank line to close the Markdown paragraph.
- *Throws*: `ObjectDisposedException` if disposed.

**FileMarkdownWriter.WriteTable(string[] headers, IEnumerable<string[]> rows)**: Writes
a pipe-delimited GFM table.

- *Parameters*: `string[] headers` — column header labels. `IEnumerable<string[]> rows`
  — data rows; each row must have the same number of cells as `headers`.
- *Algorithm*: Writes the header row, a separator row of `---` cells, and each data row,
  all using pipe delimiters. Pipe characters in header and cell values are escaped as `\|`.
  Emits a blank line after the last row.
- *Throws*: `ObjectDisposedException` if disposed.

**FileMarkdownWriter.WriteCodeBlock(string language, string code)**: Writes a fenced
code block containing a usage example.

- *Algorithm*: Delegates to `WriteSignature` — the two methods produce identical output;
  the distinction exists at the API level for semantic clarity.
- *Throws*: `ObjectDisposedException` if disposed.

**FileMarkdownWriter.WriteLink(string text, string relativePath)**: Writes an inline
Markdown navigation link.

- *Parameters*: `string text` — visible link label. `string relativePath` — relative
  path to the target file, written verbatim into the link href.
- *Algorithm*: Writes `[text](relativePath)` followed by a blank line.
- *Throws*: `ObjectDisposedException` if disposed.

**FileMarkdownWriter.Dispose()**: Flushes pending content and releases the underlying
`StreamWriter` and its file handle.

- *Algorithm*: If `_disposed` is already true, returns immediately (no-op). Otherwise
  sets `_disposed = true`, calls `_writer.Flush()`, and calls `_writer.Dispose()`.
- Safe to call multiple times; subsequent calls after the first are no-ops.

### Error Handling

- All write methods call `ObjectDisposedException.ThrowIf(_disposed, this)` at entry
  so that use-after-dispose is detected immediately and attributed to the correct call site.
- `WriteHeading` validates the level range using `ArgumentOutOfRangeException.ThrowIfNegativeOrZero`
  and `ArgumentOutOfRangeException.ThrowIfGreaterThan(level, 6)`. Values below 1 and above 6 are
  not valid ATX heading levels in CommonMark.
- `WriteSignature`, `WriteParagraph`, `WriteCodeBlock`, and `WriteLink` call
  `ArgumentNullException.ThrowIfNull` on every string parameter immediately after the
  disposed guard. `WriteTable` guards both `headers` and `rows` with
  `ArgumentNullException.ThrowIfNull`. Rejecting nulls at the API boundary surfaces
  caller defects with a clear diagnostic rather than a deferred `NullReferenceException`
  buried in rendering logic.
- `Dispose` guards against double-disposal by checking `_disposed` before touching
  `_writer`, because `StreamWriter` is not resilient to multiple dispose calls.
- I/O exceptions from `_writer` writes propagate to the caller unchanged.

### Dependencies

N/A - FileMarkdownWriter depends only on `System.IO.StreamWriter`, which is a BCL type
and is not tracked as a separate software item.

### Callers

- **FileMarkdownWriterFactory** — the only creator of `FileMarkdownWriter` instances.
  Constructs a `StreamWriter` configured for UTF-8 without BOM, wraps it in a new
  `FileMarkdownWriter`, and returns it as `IMarkdownWriter` to the caller.
