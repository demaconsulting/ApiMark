## DotNetEmitterSingleFile

### Verification Approach

`DotNetEmitterSingleFile` is integration-tested by parsing a fixture assembly
and calling `Emit` with `OutputFormat.SingleFile` and an
`InMemoryMarkdownWriterFactory`. Tests verify that exactly one writer is created,
that it is keyed `api`, and that its content contains the expected assembly,
namespace, and type headings. No internal production components are mocked beyond
the in-memory factory.

### Test Environment

Tests require the compiled fixture assembly, its XML documentation file, and
the `InMemoryMarkdownWriterFactory` from `ApiMark.Core.TestHelpers`. No external
service or network dependency is needed.

### Acceptance Criteria

- All `DotNetEmitterSingleFile` tests pass with zero failures.
- Exactly one Markdown writer is created.
- The single writer is keyed `api`.
- The output file contains an assembly-level heading.
- The output file contains a namespace-level heading.
- The output file contains a type-level heading for the fixture type.
- When `HeadingDepth` is set to a non-default value, all heading levels in the output are offset accordingly.
- When the assembly carries an `AssemblyDescriptionAttribute`, its value is emitted as a paragraph after the assembly-level heading.
- When a namespace has a NamespaceDoc XML summary, that summary is emitted as a paragraph below the namespace heading.
- When a namespace has NamespaceDoc remarks and example parts, they are emitted after the summary (remarks as a paragraph, example code as a fenced code block).
- A type whose `<remarks>` contains a `<list type="table">` renders the list as a Markdown table in single-file output.
- A compact bullet list of member names and summaries is emitted before the per-member heading sections within each type section.
- Constructor members appear before all other members; remaining members are ordered alphabetically.
- Delegate types do not emit compiler-generated member sections (Invoke, BeginInvoke, EndInvoke).
- Nested types include a parent-context notice paragraph (e.g., "Nested type of `OuterClass`.").
- Parameter type cells in tables contain plain text, not Markdown links.

### Test Scenarios

**Creates exactly one writer**: Verifies that the single-file emitter produces
exactly one Markdown writer. This scenario is tested by
`DotNetEmitterSingleFile_Emit_ValidModel_CreatesExactlyOneWriter`.

**Creates only the api writer**: Verifies that the single writer produced is
keyed `api`. This scenario is tested by
`DotNetEmitterSingleFile_Emit_ValidModel_CreatesApiFileOnly`.

**Api file contains an assembly-level heading**: Verifies that the output file
includes a heading containing the fixture assembly name. This scenario is tested
by `DotNetEmitterSingleFile_Emit_ValidModel_ApiFileContainsAssemblyHeading`.

**Api file contains a namespace-level heading**: Verifies that the output file
includes a heading containing the fixture namespace name. This scenario is tested
by `DotNetEmitterSingleFile_Emit_ValidModel_ApiFileContainsNamespaceHeading`.

**Api file contains a type-level heading for SampleClass**: Verifies that the
output file includes a heading for `SampleClass`. This scenario is tested by
`DotNetEmitterSingleFile_Emit_ValidModel_ApiFileContainsTypeHeading`.

**Non-default HeadingDepth offsets all heading levels**: Verifies that when
`HeadingDepth` is configured to a non-default value, the heading levels in the
output are offset accordingly so the document integrates correctly into a larger
compound document. This scenario is tested by
`DotNetEmitterSingleFile_Emit_NonDefaultHeadingDepth_OffsetsHeadings`.

**Assembly description paragraph follows assembly heading**: Verifies that when
the assembly carries an `AssemblyDescriptionAttribute`, its value is emitted as a
paragraph immediately after the assembly-level heading. This scenario is tested by
`DotNetEmitterSingleFile_Emit_AssemblyWithDescription_EmitsDescriptionParagraph`.

**NamespaceDoc summary follows namespace heading**: Verifies that a namespace
carrying a NamespaceDoc carrier class has its XML summary emitted as a paragraph
below the namespace heading. This scenario is tested by
`DotNetEmitterSingleFile_Emit_NamespaceWithDoc_EmitsNamespaceSummary`.

**NamespaceDoc remarks follow the namespace summary**: Verifies that a namespace
carrying a NamespaceDoc carrier with `<remarks>` has that remarks text emitted as a
paragraph after the summary. This scenario is tested by
`DotNetEmitterSingleFile_Emit_NamespaceWithDoc_EmitsNamespaceRemarks`.

**NamespaceDoc example is emitted as a code block**: Verifies that a namespace
carrying a NamespaceDoc carrier with `<example><code>` has that example emitted as a
fenced code block. This scenario is tested by
`DotNetEmitterSingleFile_Emit_NamespaceWithDoc_EmitsNamespaceExampleCodeBlock`.

**Remarks table list renders as a Markdown table**: Verifies that a type whose
`<remarks>` contains a `<list type="table">` renders the list as a Markdown pipe
table (header, separator, and rows) within the single-file output. This scenario is
tested by `DotNetEmitterSingleFile_Emit_TypeWithListRemarks_RendersTableInMarkdown`.

**Compact bullet list appears before per-member headings**: Verifies that within
each type section, a compact bullet list paragraph summarizing all members is
emitted before the individual H{depth+3} member heading sections. This scenario
is tested by
`DotNetEmitterSingleFile_Emit_TypeWithMembers_EmitsBulletListBeforeMemberHeadings`.

**Constructors appear before other members**: Verifies that for a type with both
a constructor and other members, the constructor heading appears before the other
member headings in the output. This scenario is tested by
`DotNetEmitterSingleFile_Emit_TypeWithConstructorAndMethods_ConstructorAppearsFirst`.

**Delegate types omit compiler-generated member sections**: Verifies that the
single-file emitter does not emit Invoke, BeginInvoke, or EndInvoke member sections
for delegate types, keeping the delegate section focused on signature and description.
This scenario is tested by
`DotNetEmitterSingleFile_Emit_DelegateType_NoMemberSectionsEmitted`.

**Nested types include a parent-context notice**: Verifies that for a nested type,
a paragraph of the form "Nested type of `OuterType`." is emitted immediately after
the type heading so that readers can identify the containing type relationship in
a flat document. This scenario is tested by
`DotNetEmitterSingleFile_Emit_NestedType_EmitsParentNotice`.

**Parameter type cells are plain text, not Markdown links**: Verifies that type
cells in parameter tables contain plain simplified type names without Markdown link
syntax (`[Name](path.md)`), since relative file links are meaningless inside a
single document. This scenario is tested by
`DotNetEmitterSingleFile_Emit_MethodWithParameter_TypeCellIsPlainText`.
