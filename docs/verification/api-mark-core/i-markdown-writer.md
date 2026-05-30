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
`MarkdownWriterContract_TestDoubleCompiles`.

**IMarkdownWriter extends IDisposable**: Verifies that an `IMarkdownWriter` reference
can be assigned to an `IDisposable` variable and that Dispose can be called through
it, confirming the interface inherits from IDisposable. Tested by
`MarkdownWriterContract_IsDisposable`.

**Structured Markdown operations are forwarded with correct values**: Verifies that
consumers can call heading, paragraph, table, code block, and link operations through
an `IMarkdownWriter` reference and that the recorded invocations carry the correct
level, text, and path arguments. Tested by
`MarkdownWriterContract_ForwardsStructuredBlocks`.

**Writer contract captures output in declaration order**: Verifies that successive
write operations through the `IMarkdownWriter` interface are recorded in the sequence
they were issued, so callers can assert on the exact content and ordering of generated
documentation blocks. Tested by `MarkdownWriterContract_RecordsOperationsInOrder`.
