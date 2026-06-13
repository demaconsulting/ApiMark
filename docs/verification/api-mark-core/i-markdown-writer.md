## IMarkdownWriter

### Verification Approach

`IMarkdownWriter` is verified via the `InMemoryMarkdownWriter` test double in
`ApiMark.Core.TestHelpers`. If the interface definition is incorrect or incomplete
the test double fails to compile, providing immediate compile-time feedback on
interface correctness. Runtime contract tests confirm that write operations are
forwarded with the correct arguments and in the correct order.

### Test Environment

N/A — standard test environment using the .NET test runner is sufficient.
Interface contract compliance is enforced at compile time through the test double
in `ApiMark.Core.TestHelpers`.

### Acceptance Criteria

- All `IMarkdownWriter` contract tests pass with zero failures.
- The interface extends `IDisposable`.
- The interface exposes the full set of structured Markdown operations required by
  generator implementations: headings, signatures, paragraphs, tables, code blocks,
  and links.
- An in-memory test double that implements `IMarkdownWriter` compiles without errors
  and can be used to capture and verify generator output.

### Test Scenarios

**Test double implements IMarkdownWriter without errors**: Verifies that
`InMemoryMarkdownWriter` compiles cleanly and can be instantiated, confirming that
the interface contract is implementable without hidden dependencies. Tested by
`InMemoryMarkdownWriter_Instantiate_AsInterface_Succeeds`.

**IMarkdownWriter extends IDisposable**: Verifies that an `IMarkdownWriter` reference
can be assigned to an `IDisposable` variable and that Dispose can be called through
it, confirming the interface inherits from IDisposable. Tested by
`IMarkdownWriter_IsDisposable_ExtendsIDisposable`.

**Dispose marks the writer as disposed**: Verifies that calling `Dispose` on an
`InMemoryMarkdownWriter` sets `IsDisposed` to `true`, confirming that the IDisposable
contract is correctly implemented by the test double. Tested by
`InMemoryMarkdownWriter_Dispose_Called_SetsIsDisposedFlag`.

**Structured Markdown operations are forwarded with correct values**: Verifies that
consumers can call heading, signature, paragraph, table, code block, and link operations through
an `IMarkdownWriter` reference and that the recorded invocations carry the correct
level, text, and path arguments. Tested by
`InMemoryMarkdownWriter_Write_AllMethods_RecordsOperations`.

**Writer contract captures output in declaration order**: Verifies that successive
write operations through the `IMarkdownWriter` interface are recorded in the sequence
they were issued, so callers can assert on the exact content and ordering of generated
documentation blocks. Tested by `InMemoryMarkdownWriter_Write_MultipleOps_RecordsInOrder`.

**WriteHeading produces correct ATX Markdown syntax at each level**: Verifies that
`FileMarkdownWriter.WriteHeading` emits `# Heading` for level 1 and `### Heading` for
level 3, confirming that the heading-depth-to-hash mapping is correct across the
supported range. Tested by `FileMarkdownWriter_WriteHeading_Level1_WritesCorrectMarkdown`
and `FileMarkdownWriter_WriteHeading_Level3_WritesCorrectMarkdown`.

**WriteSignature produces fenced code block with language tag**: Verifies that
`FileMarkdownWriter.WriteSignature` writes a code-fenced block opening with the
specified language identifier and contains the signature text. Tested by
`FileMarkdownWriter_WriteSignature_ValidArgs_WritesCodeFence`.

**WriteParagraph writes prose text to the output file**: Verifies that
`FileMarkdownWriter.WriteParagraph` writes the supplied text to the file so that
consumers can assert the paragraph content appears in the generated Markdown. Tested
by `FileMarkdownWriter_WriteParagraph_ValidText_WritesParagraphText`.

**WriteTable produces pipe-delimited GFM table with separator row**: Verifies that
`FileMarkdownWriter.WriteTable` emits a header row, a `| --- |` separator row, and
data rows in pipe-delimited format, confirming the output is valid GitHub-Flavoured
Markdown table syntax. Tested by `FileMarkdownWriter_WriteTable_ValidArgs_WritesPipeTable`.

**WriteCodeBlock produces fenced code block with language tag**: Verifies that
`FileMarkdownWriter.WriteCodeBlock` writes a code-fenced block with the correct
language identifier and code content. Tested by
`FileMarkdownWriter_WriteCodeBlock_ValidArgs_WritesCodeFence`.

**WriteLink produces inline Markdown link syntax**: Verifies that
`FileMarkdownWriter.WriteLink` emits `[text](relativePath)` inline link syntax with
the correct display label and relative path. Tested by
`FileMarkdownWriter_WriteLink_ValidArgs_WritesMarkdownLink`.

**All write operations recorded in order**: Verifies that all six section types
(heading, signature, paragraph, table, code block, and link) can be written to an
`InMemoryMarkdownWriter` and that `IsDisposed` is set after disposal, with each
operation type correctly identified in sequence. Tested by
`InMemoryMarkdownWriter_Write_AllOperations_RecordsInOrder`.

**Dispose flushes buffered content and releases the file handle**: Verifies that
calling `Dispose` on a `FileMarkdownWriter` flushes all buffered content to disk and
releases the file handle so another caller can open the file immediately after
disposal. Tested by `FileMarkdownWriter_Dispose_AfterWrite_FlushesAndClosesFile`.

**WriteHeading interface method is callable without error**: Verifies that
`IMarkdownWriter.WriteHeading` can be called with a valid level and text through the
interface without throwing, confirming the method is correctly declared and implemented
by the test double. Tested by `IMarkdownWriter_WriteHeading_ValidArgs_DoesNotThrow`.

**WriteSignature interface method is callable without error**: Verifies that
`IMarkdownWriter.WriteSignature` can be called with a valid language and signature
string through the interface without throwing. Tested by
`IMarkdownWriter_WriteSignature_ValidArgs_DoesNotThrow`.

**WriteParagraph interface method is callable without error**: Verifies that
`IMarkdownWriter.WriteParagraph` can be called with valid text through the interface
without throwing. Tested by `IMarkdownWriter_WriteParagraph_ValidText_DoesNotThrow`.

**WriteTable interface method is callable without error**: Verifies that
`IMarkdownWriter.WriteTable` can be called with valid headers and rows through the
interface without throwing. Tested by `IMarkdownWriter_WriteTable_ValidArgs_DoesNotThrow`.

**WriteCodeBlock interface method is callable without error**: Verifies that
`IMarkdownWriter.WriteCodeBlock` can be called with a valid language and code string
through the interface without throwing. Tested by
`IMarkdownWriter_WriteCodeBlock_ValidArgs_DoesNotThrow`.

**WriteLink interface method is callable without error**: Verifies that
`IMarkdownWriter.WriteLink` can be called with valid display text and a relative path
through the interface without throwing. Tested by
`IMarkdownWriter_WriteLink_ValidArgs_DoesNotThrow`.

**WriteTable pipe characters in cell content are escaped**: Verifies that
`FileMarkdownWriter.WriteTable` escapes literal pipe characters in cell content as `\|`
so that they do not break the pipe-delimited table structure. Tested by
`FileMarkdownWriter_WriteTable_CellWithPipe_EscapesPipe`.
