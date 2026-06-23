## FileMarkdownWriterFactory

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

FileMarkdownWriterFactory is the production file-system implementation of
`IMarkdownWriterFactory`. It creates Markdown output files under a configured
root directory, building the required directory tree on demand so that language
generators never need to manage physical folders themselves.

### Data Model

`FileMarkdownWriterFactory` is a `public sealed class` with one private field:

- `_outputDirectory` (`string`, private readonly): the root output directory
  supplied at construction time. Validated at construction; never null or whitespace.

### Key Methods

**FileMarkdownWriterFactory(string outputDirectory)**: Constructs the factory.

- *Parameters*: `string outputDirectory` — absolute or relative path to the root
  output directory. The directory is created on first use if it does not already
  exist. Must not be null, empty, or whitespace.
- *Preconditions*: `outputDirectory` is not null, empty, or whitespace.
- *Postconditions*: `_outputDirectory` is set to the supplied value.
- *Throws*: `ArgumentException` when `outputDirectory` is null, empty, or whitespace.

**FileMarkdownWriterFactory.CreateMarkdown(string subFolder, string name)**:
Creates a new `FileMarkdownWriter` targeting `{outputDirectory}/{subFolder}/{name}.md`.

- *Parameters*:
  - `string subFolder` — subfolder path relative to the output root. Pass an
    empty string or whitespace to create a file directly under the output root.
    Forward slashes are acceptable; path separators are normalized internally.
  - `string name` — file name without the `.md` extension. Must not be null,
    empty, or whitespace.
- *Returns*: A new `FileMarkdownWriter` ready for write calls. The caller is
  responsible for disposing the returned writer.
- *Preconditions*: `name` is not null, empty, or whitespace.
- *Algorithm*:
  1. Reject `name` if null or whitespace; throw `ArgumentException`.
  2. Compute the target directory: when `subFolder` is null or whitespace, use
     `_outputDirectory` directly; otherwise call `PathHelpers.SafePathCombine`
     to combine `_outputDirectory` with `subFolder`.
  3. Call `Directory.CreateDirectory(targetDirectory)` to ensure the directory
     tree exists before writing.
  4. Compose the full file path by calling `PathHelpers.SafePathCombine` with
     `targetDirectory` and `name + ".md"`.
  5. Construct a `StreamWriter` over the file path using `UTF8Encoding` with
     `encoderShouldEmitUTF8Identifier: false` (UTF-8 without BOM) and `append: false`.
  6. Wrap the stream writer in a new `FileMarkdownWriter` and return it.
- *Throws*: `ArgumentException` when `name` is null, empty, or whitespace.

### Error Handling

- Constructor throws `ArgumentException` immediately when `outputDirectory` is
  null, empty, or whitespace, so callers receive a clear error at configuration
  time rather than a deferred I/O failure.
- `CreateMarkdown` throws `ArgumentException` when `name` is null, empty, or
  whitespace so the misuse is immediately attributable.
- `PathHelpers.SafePathCombine` is used for both the subfolder and the file-name
  combination. It rejects path segments that resolve outside the base directory,
  preventing directory-traversal vulnerabilities.
- `Directory.CreateDirectory` is idempotent and does not throw when the directory
  already exists.
- I/O exceptions from `StreamWriter` construction (e.g., access-denied) propagate
  to the caller unchanged.

### Dependencies

- **PathHelpers** — `SafePathCombine` is used to combine the output root with the
  caller-supplied subFolder and the file name, preventing directory-traversal attacks.

### Callers

- **DotNetGenerator** — injects `FileMarkdownWriterFactory` as the `IMarkdownWriterFactory`
  in production; a test double is substituted in unit tests.
- **CppGenerator** — same injection pattern as DotNetGenerator.
- **VhdlGenerator** — same injection pattern as DotNetGenerator.
- **Program** — constructs `FileMarkdownWriterFactory` from the configured output
  directory and passes it to the language generator.
