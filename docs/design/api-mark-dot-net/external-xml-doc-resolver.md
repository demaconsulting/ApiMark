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

**_referencePathsByAssemblyName** (private `Dictionary<string, List<string>>`,
`OrdinalIgnoreCase`): Reference assembly paths grouped by their simple file
name (no extension), precomputed once in the constructor from
`_referenceAssemblyPaths`. Lets the declaring-assembly-hint fast path in
`TryGetMember(string, string?)` resolve a hint to its candidate path(s) in
O(1) instead of scanning `_referenceAssemblyPaths` on every call. A list is
kept per name (rather than a single path) because two configured reference
paths can share the same simple file name (e.g. the same assembly resolved
from two different target-framework sub-folders); every path sharing the
hinted name is probed, in configured order, before falling back to the full
scan.

**_docsByReferencePath** (private `Dictionary<string, Dictionary<string, XElement>?>`):
Cache of parsed member indexes keyed by reference assembly path. A `null`
value means "no XML documentation file could be found or parsed for this
reference assembly path", cached so repeated misses do not re-probe the file
system.

**_memberCache** (private `Dictionary<(string Hint, string MemberId), XElement?>`):
Cache of resolved member elements keyed by a composite of the
declaring-assembly hint supplied to `TryGetMember(string, string?)` (or the
empty string when none was supplied) and the member ID, spanning all
configured reference assembly paths. A `null` value means "not found in any
configured reference assembly's XML documentation", cached so repeated
misses do not re-scan every path. The hint is included in the key (rather
than caching by member ID alone) because XML documentation member IDs do not
carry assembly identity: in the rare case where two unrelated configured
assemblies declare a member with an identical ID, a lookup for one hint must
never be served the other hint's cached result.

### Key Methods

**ExternalXmlDocResolver constructor**: Filters out blank (null, empty, or
whitespace-only) entries from the supplied reference assembly paths — because
`Path.GetFullPath("")` resolves to the current working directory, which would
otherwise become a bogus, legitimate-looking reference path — then normalizes
and stores the remainder. Normalization performs some file-system access:
`ApiMark.Core.PathHelpers.NormalizeCase` enumerates existing parent directories
via `Directory.EnumerateFileSystemEntries` to resolve the real on-disk casing
of each path, so two differently-cased spellings of the same file collapse to
a single cache key. This normalization is best-effort and never throws for a
not-yet-existing reference path or path segment — it gracefully falls back to
the as-supplied casing when a segment does not (yet) exist on disk. A single
`Dictionary<string, string[]>` directory-entry cache, scoped to this
constructor call, is shared across every `NormalizeCase` call so that
reference paths sharing common ancestor directories (e.g. many NuGet package
assemblies under the same package-cache root) do not each independently
re-enumerate those same shared directories; the cache is never retained
beyond the constructor call, so results can never become stale across
independently constructed `ExternalXmlDocResolver` instances. XML
*documentation parsing* itself remains fully deferred to first use (see
`TryGetMember`). After normalization, the constructor also groups the
resulting paths into `_referencePathsByAssemblyName` by simple file name
(`OrdinalIgnoreCase`), so the hinted lookup path below never needs to scan
`_referenceAssemblyPaths` directly.

- *Parameters*: `IReadOnlyList<string> referenceAssemblyPaths` — paths to
  referenced assembly DLLs whose sibling XML documentation files should be
  searched, in order.
- *Exceptions*: Throws `ArgumentNullException` when `referenceAssemblyPaths`
  is `null`.

**TryGetMember(memberId)**: Attempts to resolve a member ID against the XML
documentation files of the configured reference assembly paths, with no
declaring-assembly hint. Equivalent to calling
`TryGetMember(memberId, declaringAssemblyHint: null)`.

- *Parameters*: `string memberId` — the XML doc member identifier (e.g.
  `T:MyNamespace.MyClass`) to resolve.
- *Returns*: The matching `<member>` element, or `null` when not found in any
  configured reference assembly.

**TryGetMember(memberId, declaringAssemblyHint)**: Attempts to resolve a
member ID against the XML documentation files of the configured reference
assembly paths, using an optional declaring-assembly hint to probe the
expected path(s) first before falling back to a full search.

- *Parameters*: `string memberId` — the XML doc member identifier to resolve.
  `string? declaringAssemblyHint` — the simple name (no extension) of the
  assembly expected to declare `memberId` (built from Mono.Cecil metadata by
  `DotNetGenerator.BuildInheritanceChain`), or `null`/empty when unknown.
- *Returns*: The matching `<member>` element, or `null` when not found in any
  configured reference assembly.
- *Algorithm*: Checks `_memberCache` first, keyed by `(declaringAssemblyHint
  ?? "", memberId)` (including cached misses). On a cache miss, when a
  non-empty hint is supplied and matches an entry in
  `_referencePathsByAssemblyName`, each matching path is probed (via
  `GetOrLoadMembers`) in configured order before any other path is touched —
  this is the fast path that avoids probing an entire auto-harvested
  `ReferencePaths` set (which can number in the hundreds) for the common case
  where the declaring assembly is already known. If the hint is absent, does
  not match any configured path, or the member is not found in any hinted
  path, resolution falls back to the same full, in-order scan of every
  configured reference path used by the parameterless overload, so a wrong
  or missing hint never causes a real match to be missed. The final result
  (including `null`) is cached before returning.

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

### Known Limitations

XML documentation member IDs carry no assembly identity (see `XmlDocReader`'s
own known limitation for the analogous local-vs-external case). `_memberCache`
is keyed by `(declaringAssemblyHint, memberId)`, so two calls that supply
different (correct) hints for a member ID that happens to be identical across
two unrelated assemblies can never poison each other's cached result — but
within a single hint value (including the "no hint" empty-string key used by
the parameterless overload and any hinted call whose hint does not match a
configured path), resolution still falls back to the full, in-order scan of
`_referenceAssemblyPaths`, with the first configured path whose XML
documentation file contains the ID winning (see `TryGetMember`). If two
different referenced assemblies happen to define a type or member with an
identical XML doc ID — for example, two different versions of the same NuGet
package both referenced as separate assemblies, or aliased/type-forwarded
types — and neither lookup supplies a hint that distinguishes them, a target
that should resolve against the second assembly could still silently receive
the first assembly's documentation instead. This is considered an acceptable,
narrow risk: it requires an unusual dependency graph (duplicate or colliding
assemblies) that is uncommon in practice, and fully closing it for the
no-hint case would require threading the resolved declaring-assembly identity
through every external lookup call site — a larger design change than this
known-limitation note. `DotNetGenerator.TryAddAssemblyHint` has the analogous
limitation on the hint-computation side: it also uses first-wins semantics
per target ID.

### Dependencies

- **System.Xml.Linq** — used to parse and navigate each reference assembly's
  XML documentation file.
- **ApiMark.Core.PathHelpers** — `NormalizeCase`/`Comparer` resolve each
  configured reference assembly path to its actual on-disk casing so that
  differently-cased spellings of the same file collapse to a single cache
  key, regardless of platform or file-system case sensitivity.

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
(`ExternalXmlDocResolver(IReadOnlyList<string> referenceAssemblyPaths)`) and
two public lookup overloads — `TryGetMember(string memberId)` and
`TryGetMember(string memberId, string? declaringAssemblyHint)` — so it is
part of `ApiMark.DotNet`'s public API surface, not an internal implementation
detail. Its intended consumers are:

- **`DotNetGenerator.Parse`** — the primary production consumer, which
  constructs an instance from `DotNetGeneratorOptions.ReferencePaths` and
  wires the hinted `TryGetMember(string, string?)` overload into
  `XmlDocReader` as the external member lookup delegate, supplying each
  target's precomputed declaring-assembly hint (see *Collaborators* above).
- **Test code** (`ExternalXmlDocResolverTests`) — which instantiates the
  class directly to validate lookup, caching, hint fast-path, and `ref`/`lib`
  fallback behavior in isolation from the rest of the parsing pipeline.
- **Other `IApiGenerator` implementations or external tooling**, should they
  need to resolve member documentation across NuGet package boundaries using
  the same conventions as `ApiMark.DotNet`, since nothing about the type ties
  it to `DotNetGenerator` internals.

Being public is a deliberate design choice, not an oversight: it keeps the
class independently testable without relying on `InternalsVisibleTo`, and
allows it to be reused as a standalone building block by any caller that
needs `<inheritdoc/>`-style cross-assembly XML doc resolution.
