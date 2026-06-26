## FileMarkdownWriter

### Verification Approach

`FileMarkdownWriter` is `internal sealed` and is exercised through
`FileMarkdownWriterFactory` in `test/ApiMark.Core.Tests/FileMarkdownWriterTests.cs`
and `test/ApiMark.Core.Tests/IMarkdownWriterTests.cs`. Tests create a temporary
directory, write content through the factory, then read the resulting file and assert
on its contents. Disposal behavior is verified by confirming that the file handle is
released after `Dispose()` is called.

### Test Environment

N/A - standard .NET test runner is sufficient. Tests use isolated temporary directories
and clean up in `IDisposable.Dispose` to prevent test pollution.

### Acceptance Criteria

- All `FileMarkdownWriterTests` and relevant `IMarkdownWriterTests` test cases pass with
  zero failures.
- `WriteHeading` at level 1 produces `# {text}` and at level 3 produces `### {text}`.
- `WriteHeading` throws `ArgumentOutOfRangeException` for level 0 and level 7.
- All write methods throw `ObjectDisposedException` when called after `Dispose`.
- `WriteSignature` produces a fenced code block with the supplied language tag and code;
  throws `ArgumentNullException` for null language or code.
- `WriteParagraph` writes the supplied text to the file; throws `ArgumentNullException`
  for null text.
- `WriteTable` produces a pipe-delimited GFM table with a header row, separator row, and
  data rows; pipe characters in cell values are escaped as `\|`; throws
  `ArgumentNullException` for null headers or null rows; throws `ArgumentException` for
  an empty headers array, any null element in the headers array, any null row element in the
  rows sequence, any null cell element within a row, or any row whose column count differs
  from the header count.
- `WriteCodeBlock` produces a fenced code block equivalent to `WriteSignature`; throws
  `ArgumentNullException` for null language or code.
- `WriteLink` writes an inline Markdown link `[text](path)`; throws `ArgumentNullException`
  for null text or null relativePath; throws `ArgumentException` for empty text.
- `WriteHeading` throws `ArgumentException` for empty text in addition to
  `ArgumentOutOfRangeException` for out-of-range levels.
- After `Dispose` is called the file handle is released and the file content is
  accessible to other readers.

### Test Scenarios

**WriteHeading at level 1 produces single-# ATX heading**: Verifies that
`WriteHeading(1, "My Heading")` writes `# My Heading` to the file. Tested by
`FileMarkdownWriter_WriteHeading_Level1_WritesCorrectMarkdown`.

**WriteHeading at level 3 produces triple-# ATX heading**: Verifies that
`WriteHeading(3, "My Heading")` writes `### My Heading` to the file. Tested by
`FileMarkdownWriter_WriteHeading_Level3_WritesCorrectMarkdown`.

**WriteSignature produces fenced code block with language tag**: Verifies that
`WriteSignature("csharp", "public void DoWork();")` emits a fenced block opening
with ` ```csharp ` and the code on the next line. Tested by
`FileMarkdownWriter_WriteSignature_ValidArgs_WritesCodeFence`.

**WriteParagraph writes paragraph text**: Verifies that the paragraph body text appears
verbatim in the output file. Tested by
`FileMarkdownWriter_WriteParagraph_ValidText_WritesParagraphText`.

**WriteTable produces pipe-delimited GFM table**: Verifies that the header row,
separator row, and data rows are all written with correct pipe delimiters. Tested by
`FileMarkdownWriter_WriteTable_ValidArgs_WritesPipeTable`.

**WriteTable escapes pipe characters in cell values**: Verifies that a cell value
containing a literal `|` character is written as `\|` in the output, preventing
table structure breakage. Tested by `FileMarkdownWriter_WriteTable_CellWithPipe_EscapesPipe`.

**WriteCodeBlock produces fenced code block**: Verifies that `WriteCodeBlock` produces
the same fenced-block output as `WriteSignature`. Tested by
`FileMarkdownWriter_WriteCodeBlock_ValidArgs_WritesCodeFence`.

**WriteLink writes inline Markdown link**: Verifies that `WriteLink("Back to Index", "../api.md")`
produces `[Back to Index](../api.md)` in the file. Tested by
`FileMarkdownWriter_WriteLink_ValidArgs_WritesMarkdownLink`.

**Dispose flushes content and releases file handle**: Verifies that after `Dispose()` is
called the file is non-empty and can be opened with exclusive read access by another
caller, confirming that content was flushed and the handle was released. Tested by
`FileMarkdownWriter_Dispose_AfterWrite_FlushesAndClosesFile`.

**WriteHeading rejects level below 1**: Verifies that `WriteHeading(0, ...)` throws
`ArgumentOutOfRangeException`, enforcing the CommonMark lower bound. Tested by
`FileMarkdownWriter_WriteHeading_ZeroLevel_ThrowsArgumentOutOfRangeException`.

**WriteHeading rejects level above 6**: Verifies that `WriteHeading(7, ...)` throws
`ArgumentOutOfRangeException`, enforcing the CommonMark upper bound. Tested by
`FileMarkdownWriter_WriteHeading_SevenLevel_ThrowsArgumentOutOfRangeException`.

**Any write method after Dispose throws ObjectDisposedException**: Verifies that every
write method throws `ObjectDisposedException` when called after `Dispose`, confirming that
the disposed guard is applied uniformly across the entire writer API. Tested by:
`FileMarkdownWriter_WriteHeading_AfterDispose_ThrowsObjectDisposedException`,
`FileMarkdownWriter_WriteSignature_AfterDispose_ThrowsObjectDisposedException`,
`FileMarkdownWriter_WriteParagraph_AfterDispose_ThrowsObjectDisposedException`,
`FileMarkdownWriter_WriteTable_AfterDispose_ThrowsObjectDisposedException`,
`FileMarkdownWriter_WriteCodeBlock_AfterDispose_ThrowsObjectDisposedException`,
`FileMarkdownWriter_WriteLink_AfterDispose_ThrowsObjectDisposedException`.

**WriteSignature rejects null language**: Verifies that `WriteSignature(null!, ...)` throws
`ArgumentNullException`. Tested by
`FileMarkdownWriter_WriteSignature_NullLanguage_ThrowsArgumentNullException`.

**WriteSignature rejects null code**: Verifies that `WriteSignature("csharp", null!)` throws
`ArgumentNullException`. Tested by
`FileMarkdownWriter_WriteSignature_NullCode_ThrowsArgumentNullException`.

**WriteParagraph rejects null text**: Verifies that `WriteParagraph(null!)` throws
`ArgumentNullException`. Tested by
`FileMarkdownWriter_WriteParagraph_NullText_ThrowsArgumentNullException`.

**WriteTable rejects null headers**: Verifies that `WriteTable(null!, ...)` throws
`ArgumentNullException`. Tested by
`FileMarkdownWriter_WriteTable_NullHeaders_ThrowsArgumentNullException`.

**WriteTable rejects null rows**: Verifies that `WriteTable([], null!)` throws
`ArgumentNullException`, enforcing the null contract on the rows parameter. Tested by
`FileMarkdownWriter_WriteTable_NullRows_ThrowsArgumentNullException`.

**WriteCodeBlock rejects null language**: Verifies that `WriteCodeBlock(null!, ...)` throws
`ArgumentNullException`. Tested by
`FileMarkdownWriter_WriteCodeBlock_NullLanguage_ThrowsArgumentNullException`.

**WriteCodeBlock rejects null code**: Verifies that `WriteCodeBlock("csharp", null!)` throws
`ArgumentNullException`. Tested by
`FileMarkdownWriter_WriteCodeBlock_NullCode_ThrowsArgumentNullException`.

**WriteLink rejects null text**: Verifies that `WriteLink(null!, ...)` throws
`ArgumentNullException`. Tested by
`FileMarkdownWriter_WriteLink_NullText_ThrowsArgumentNullException`.

**WriteLink rejects null relativePath**: Verifies that `WriteLink("Back", null!)` throws
`ArgumentNullException`. Tested by
`FileMarkdownWriter_WriteLink_NullRelativePath_ThrowsArgumentNullException`.

**WriteTable rejects empty headers**: Verifies that `WriteTable([], [])` throws
`ArgumentException`, enforcing that a headerless table is not valid GFM syntax. Tested by
`FileMarkdownWriter_WriteTable_EmptyHeaders_ThrowsArgumentException`.

**WriteTable rejects mismatched row column count**: Verifies that calling `WriteTable`
with a row whose cell count differs from the header count throws `ArgumentException`,
preventing silent generation of a structurally malformed pipe table. Tested by
`FileMarkdownWriter_WriteTable_MismatchedRowLength_ThrowsArgumentException`.

**WriteHeading rejects empty text**: Verifies that `WriteHeading(1, "")` throws
`ArgumentException`, enforcing that a heading with no label is not valid Markdown. Tested by
`FileMarkdownWriter_WriteHeading_EmptyText_ThrowsArgumentException`.

**WriteLink rejects empty text**: Verifies that `WriteLink("", "api.md")` throws
`ArgumentException`, enforcing that a link with no visible label is not valid navigation
markup. Tested by `FileMarkdownWriter_WriteLink_EmptyText_ThrowsArgumentException`.

**WriteTable rejects null row element**: Verifies that passing a rows sequence containing
a null element (e.g. `[["a","b"], null, ["c","d"]]`) throws `ArgumentException` before
any output is written, preventing a `NullReferenceException` in rendering logic. Empty
cell content (e.g. `""`) remains valid. Tested by
`FileMarkdownWriter_WriteTable_NullRowElement_ThrowsArgumentException`.

**WriteTable rejects null header element**: Verifies that passing a headers array containing
a null element (e.g. `["Name", null, "Description"]`) throws `ArgumentException` before any
output is written, preventing a `NullReferenceException` in rendering logic. Tested by
`FileMarkdownWriter_WriteTable_NullHeaderElement_ThrowsArgumentException`.

**WriteTable rejects null cell element**: Verifies that passing a row containing a null cell
(e.g. `[["value", null]]`) throws `ArgumentException` before any output is written,
preventing a `NullReferenceException` in rendering logic. Tested by
`FileMarkdownWriter_WriteTable_NullCellElement_ThrowsArgumentException`.
