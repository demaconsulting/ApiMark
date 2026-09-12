## ExternalXmlDocResolver

![ExternalXmlDocResolver Structure](ApiMarkDotNetView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

ExternalXmlDocResolver locates and lazily parses the XML documentation files of
externally referenced assemblies (e.g. NuGet package dependencies) so that
`<inheritdoc />` elements can resolve against base types/members defined
outside the assembly currently being documented. It is the component that
gives `XmlDocReader`'s external member lookup fallback something to call: for
each configured reference assembly path, it resolves the conventional sibling
`.xml` documentation file (falling back to a `ref/`↔`lib/` folder-segment swap
when the sibling file is absent), parses it on first use, and caches both the
per-file member index and per-member-ID lookup results (including misses) for
the lifetime of the instance.

### Data Model

**_referenceAssemblyPaths** (private `IReadOnlyList<string>`): The configured
reference assembly paths, searched in order for each lookup. Supplied by the
constructor and never mutated afterward.

**_docsByReferencePath** (private `Dictionary<string, Dictionary<string, XElement>?>`):
Cache of parsed member indexes keyed by reference assembly path. A `null`
value means "no XML documentation file could be found or parsed for this
reference assembly path", cached so repeated misses do not re-probe the file
system.

**_memberCache** (private `Dictionary<string, XElement?>`): Cache of resolved
member elements keyed by member ID, spanning all configured reference assembly
paths. A `null` value means "not found in any configured reference assembly's
XML documentation", cached so repeated misses do not re-scan every path.

### Key Methods

**ExternalXmlDocResolver constructor**: Filters out blank (null, empty, or
whitespace-only) entries from the supplied reference assembly paths — because
`Path.GetFullPath("")` resolves to the current working directory, which would
otherwise become a bogus, legitimate-looking reference path — then normalizes
and stores the remainder. Normalization performs some file-system access:
`FileSystemPathComparer.NormalizeCase` enumerates existing parent directories
via `Directory.EnumerateFileSystemEntries` to resolve the real on-disk casing
of each path, so two differently-cased spellings of the same file collapse to
a single cache key. This normalization is best-effort and never throws for a
not-yet-existing reference path or path segment — it gracefully falls back to
the as-supplied casing when a segment does not (yet) exist on disk. XML
*documentation parsing* itself remains fully deferred to first use (see
`TryGetMember`).

- *Parameters*: `IReadOnlyList<string> referenceAssemblyPaths` — paths to
  referenced assembly DLLs whose sibling XML documentation files should be
  searched, in order.
- *Exceptions*: Throws `ArgumentNullException` when `referenceAssemblyPaths`
  is `null`.

**TryGetMember**: Attempts to resolve a member ID against the XML
documentation files of the configured reference assembly paths.

- *Parameters*: `string memberId` — the XML doc member identifier (e.g.
  `T:MyNamespace.MyClass`) to resolve.
- *Returns*: The matching `<member>` element, or `null` when not found in any
  configured reference assembly.
- *Algorithm*: Checks `_memberCache` first (including cached misses). On a
  cache miss, iterates `_referenceAssemblyPaths` in order, calling
  `GetOrLoadMembers` for each and returning the first match; caches the final
  result (including `null`) before returning.

**GetOrLoadMembers** (private): Returns the cached member index for a
reference assembly path, parsing and caching it on first access via `LoadMembers`.
This is the "lazy parse once, reuse forever" mechanism — reference assembly
lists can be large (e.g. hundreds of transitive NuGet dependencies) and most of
their documentation is never needed, so eagerly parsing every configured path
up front would waste time and memory.

**LoadMembers** (private static): Locates the XML documentation file for a
reference assembly path via `ResolveXmlDocPath` and parses it into a member
index, using the same first-wins `GroupBy`/`ToDictionary` duplicate-handling
approach as `XmlDocReader`'s own constructor. Wraps `XDocument.Load` in a
narrow `catch` for `IOException`, `UnauthorizedAccessException`, and
`System.Xml.XmlException`, degrading to `null` on any of these — an
unreadable or corrupt external XML doc file must not fail the whole
documentation generation run.

**ResolveXmlDocPath** (private static): Determines the path to the XML
documentation file for a reference assembly DLL.

- Tries the conventional sibling `.xml` file first (`Path.ChangeExtension`).
- If absent, tries a `ref/`↔`lib/` folder-segment swap (via `SwapRefLibSegment`)
  and checks for a sibling `.xml` file at the swapped location.
- Returns `null` if neither location yields an existing file.

**SwapRefLibSegment** (private static): Swaps the last path segment named
exactly `ref` or `lib` (case-insensitively) for the other, mimicking the
folder layout convention used by many NuGet packages where compile-time
reference assemblies live under `ref/` and runtime assemblies (often bundled
with the actual XML documentation) live under `lib/`, or vice versa. Scans
from the end of the path (nearest the assembly file) backwards so that an
unrelated, earlier path segment that happens to be named `ref` or `lib` (for
example a user or drive folder such as `/home/lib/.nuget/packages/Pkg/ref/net8.0`)
is never matched in preference to the actual NuGet package-layout segment,
which is always the one closest to the assembly file itself. Returns
`null` when no `ref`/`lib` segment is present.

### Error Handling

An unreadable, missing, or corrupt XML documentation file for a given
reference assembly path degrades to "no documentation available for this
path" (`null` cached in `_docsByReferencePath`) rather than throwing, so a
single problematic reference assembly cannot fail the entire documentation
generation run. `TryGetMember` never throws for an unresolved member ID —
it returns `null`, consistent with `XmlDocReader`'s own miss semantics.

### Dependencies

- **System.Xml.Linq** — used to parse and navigate each reference assembly's
  XML documentation file.

### Callers

- **DotNetGenerator.Parse** — constructs an `ExternalXmlDocResolver` from
  `DotNetGeneratorOptions.ReferencePaths` when non-empty, and passes its
  `TryGetMember` method as the external member lookup delegate to the
  `XmlDocReader` constructor.
- **XmlDocReader.ResolveMemberElement** — invokes the injected
  `_externalMemberLookup` delegate (typically `ExternalXmlDocResolver.TryGetMember`)
  when a member ID is absent from its own local index.

### Known Limitation

The `ref/`↔`lib/` swap only handles a single matching path segment named
exactly `ref` or `lib`; it does not attempt to reconcile differing
target-framework sub-folders (e.g. `ref/net8.0/` vs `lib/netstandard2.0/`)
beyond that single segment swap, and it does not search arbitrary additional
locations. A NuGet package that ships its XML documentation in neither the
sibling location nor the swapped `ref/`/`lib/` location will simply not have
its external members resolved — the corresponding `<inheritdoc />` content
remains absent, with no error.

### External Interfaces

`ExternalXmlDocResolver` is a `public sealed class` with a public constructor
(`ExternalXmlDocResolver(IReadOnlyList<string> referenceAssemblyPaths)`) and a
public `TryGetMember(string memberId)` lookup method, so it is part of
`ApiMark.DotNet`'s public API surface, not an internal implementation detail.
Its intended consumers are:

- **`DotNetGenerator.Parse`** — the primary production consumer, which
  constructs an instance from `DotNetGeneratorOptions.ReferencePaths` and
  wires its `TryGetMember` method into `XmlDocReader` as the external member
  lookup delegate (see *Collaborators* above).
- **Test code** (`ExternalXmlDocResolverTests`) — which instantiates the
  class directly to validate lookup, caching, and `ref`/`lib` fallback
  behavior in isolation from the rest of the parsing pipeline.
- **Other `IApiGenerator` implementations or external tooling**, should they
  need to resolve member documentation across NuGet package boundaries using
  the same conventions as `ApiMark.DotNet`, since nothing about the type ties
  it to `DotNetGenerator` internals.

Being public is a deliberate design choice, not an oversight: it keeps the
class independently testable without relying on `InternalsVisibleTo`, and
allows it to be reused as a standalone building block by any caller that
needs `<inheritdoc/>`-style cross-assembly XML doc resolution.
