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
- When an explicit `LibraryDescription` option is supplied, its value is emitted as the
  paragraph instead of the compiled `AssemblyDescriptionAttribute` value.
- When `LibraryDescription` is null, empty, or consists only of whitespace, the compiled
  `AssemblyDescriptionAttribute` value is emitted as a fallback.
- When a namespace has a NamespaceDoc XML summary, that summary is emitted as a paragraph below the namespace heading.
- When a namespace has NamespaceDoc remarks and example parts, they are emitted after the summary (remarks as a paragraph, example code as a fenced code block).
- A type whose `<remarks>` contains a `<list type="table">` renders the list as a Markdown table in single-file output.
- A type whose `<summary>` contains a `<list type="number">` renders the list as real multi-line ordered Markdown items in single-file output.
- A member whose `<summary>` contains a `<list type="number">` renders the list as real multi-line ordered Markdown items in single-file output.
- A type with no `<summary>` but present `<remarks>` content does not show the "No description provided." placeholder; the remarks content is shown.
- A member with no `<summary>` but present `<remarks>` content does not show the "No description provided." placeholder; the remarks content is shown.
- A compact bullet list of member names and summaries is emitted before the per-member heading sections within each type section.
- Constructor members appear before all other members; remaining members are ordered alphabetically.
- Delegate types do not emit compiler-generated member sections (Invoke, BeginInvoke, EndInvoke).
- Nested types include a parent-context notice paragraph (e.g., "Nested type of `OuterClass`.").
- Parameter type cells in tables contain plain text, not Markdown links.
- A `<see cref>` to a visible in-assembly type renders as plain code-span text with no link, even though the same cref would resolve to a real link in gradual-disclosure mode.

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

**Explicit LibraryDescription overrides the compiled attribute**: Verifies that
when `DotNetGeneratorOptions.LibraryDescription` is supplied, its value is emitted
as the introductory paragraph instead of the assembly's compiled
`AssemblyDescriptionAttribute` value. This scenario is tested by
`DotNetEmitterSingleFile_Emit_LibraryDescriptionSupplied_OverridesAssemblyDescription`.

**Whitespace-only LibraryDescription falls back to the compiled attribute**: Verifies
that when `LibraryDescription` is set to an empty or whitespace-only string, the
compiled `AssemblyDescriptionAttribute` value is emitted instead, confirming the
fallback condition matches `string.IsNullOrWhiteSpace` rather than a narrower
null/empty-only check. This scenario is tested by
`DotNetEmitterSingleFile_Emit_LibraryDescriptionWhitespace_FallsBackToAssemblyDescription`.

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

**Summary numbered list renders as multi-line Markdown**: Verifies that a type
whose `<summary>` contains a `<list type="number">` renders the list as real
multi-line ordered Markdown items — matching the gradual-disclosure behavior —
confirming the single-file emitter's type sections also switched from the
single-line `GetSummary` to the multi-line `GetSummaryMarkdown`. This scenario
is tested by
`DotNetEmitterSingleFile_Emit_TypeWithListSummary_RendersNumberedListInMarkdown`.

**Member summary numbered list renders as multi-line Markdown**: Verifies the
same behavior at the member level — a member whose `<summary>` contains a
`<list type="number">` renders the list as real multi-line ordered Markdown
items, confirming `WriteSingleFileMemberSection` also switched from the
single-line `GetSummary` to the multi-line `GetSummaryMarkdown`. This scenario
is tested by
`DotNetEmitterSingleFile_Emit_MemberWithListSummary_RendersNumberedListInMarkdown`.

**Type with remarks-only content suppresses the placeholder and shows the
remarks**: Verifies that a type with a synthetic XML doc giving it only
`<remarks>` (no `<summary>`) does not show the "No description provided."
placeholder in its own section of the single-file output, and that its remarks
content is shown instead — confirming `WriteSingleFileTypeSections` applies the
same placeholder-suppression rule as the gradual-disclosure emitter's
member-detail and type-page bodies. This scenario is tested by
`DotNetEmitterSingleFile_Emit_TypeWithRemarksOnly_SuppressesPlaceholderAndShowsRemarks`.

**Member with remarks-only content suppresses the placeholder and shows the
remarks**: Verifies the same placeholder-suppression rule at the member level
— a member with only `<remarks>` (no `<summary>`) does not show the
"No description provided." placeholder in its own section, and its remarks
content is shown instead — confirming `WriteSingleFileMemberSection` applies
the identical rule as `WriteSingleFileTypeSections`. This scenario is tested by
`DotNetEmitterSingleFile_Emit_MemberWithRemarksOnly_SuppressesPlaceholderAndShowsRemarks`.

**Single-file mode never links a cref, even when the same target would be
linked in gradual-disclosure mode**: Verifies that a `<see cref>` pointing at
an in-assembly, visible type — the same fixture type that gradual-disclosure
output links in `CrefLinking_SeeCrefToVisibleType_RendersAsMarkdownLink` —
renders as plain code-span text with no link in single-file output, proving
`DotNetEmitterSingleFile` never constructs or passes a `CrefLinkContext` to
any `XmlDocReader` call site, so the new optional parameter always defaults
to `null` here. This scenario is tested by
`CrefLinking_SingleFileMode_SeeCrefToVisibleType_RemainsCodeSpanOnly`.
