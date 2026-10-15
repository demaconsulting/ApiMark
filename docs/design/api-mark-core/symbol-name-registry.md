## SymbolNameRegistry

![SymbolNameRegistry Structure](ApiMarkCoreView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

SymbolNameRegistry is the single source of truth for the file and folder name of every generated page.
Emitters register every symbol that needs a file, the registry resolves all names once so that none
collide, and only then do writers and link resolvers read the approved names. A name is escaped exactly once,
which removes the double-escaping bugs of earlier releases. `FileNameRules` holds the character-level
escaping and legality check, and `LinkPath` builds relative link paths. The numbered rules are in
`docs/design/api-mark-core/file-naming-rules.md`.

### Data Model

`SymbolNameRegistry` holds a dictionary of entries keyed by an ordinal key. Each entry records its parent
key, raw name, `SymbolKind` (`Reserved`, `Namespace`, `Type`, `Member`), and, after `Resolve`, its escaped
base name, tier, final name, and path. `FileNameRules` and `LinkPath` are `public static` classes without
state.

### Key Methods

**SymbolNameRegistry.Register** (`public`): Adds a symbol (`key`, `parentKey`, `rawName`, `kind`). Throws
`ArgumentNullException` for a null key or raw name, `ArgumentException` for an empty key, and
`InvalidOperationException` for a duplicate key, a missing parent, or a call after `Resolve`.

**SymbolNameRegistry.Resolve** (`public`): Escapes every raw name once, then for each directory grants
names in tier order (rules NR-14 to NR-16) and gives displaced entries a `-N` suffix. Parents are resolved
before children. May be called once.

**SymbolNameRegistry.GetName / GetPath / GetDirectory / GetAllPaths** (`public`): Read the
approved names. They throw `InvalidOperationException` before `Resolve` and for an unregistered key.

**SymbolNameRegistry.Contains** (`public`): A registration-presence check that does not require
`Resolve`. It can be called during registration (for example to skip an overload that is already
registered) and after resolution, and returns `false` for an unregistered key instead of throwing.

**FileNameRules.Escape** (`public static`): Escapes a raw name into a legal segment (NR-06 to NR-09) and
reports whether it was transformed. **FileNameRules.IsLegalSegment** (`public static`): Reports whether a
segment is legal on Windows and Linux.

**LinkPath.Relative / LinkPath.Escape** (`public static`): Build the forward-slash relative path between two
pages and percent-encode link-hostile characters in a destination (NR-19).

### Design Decisions

- Registering everything first and resolving once makes names independent of emit order and visibility.
- Reading before `Resolve` and registering after it are errors, so no page can use an unapproved name.
- Escaping lives only in the registry's `Resolve`; no other production code calls `FileNameRules.Escape`.
- No hash suffixes: names stay readable and the order of resolution (NR-14) is deterministic.

### Error Handling

Misuse (null or empty arguments, duplicate keys, wrong call order, unknown keys) throws
`ArgumentException`, `ArgumentNullException`, or `InvalidOperationException`. `Escape` never fails for a
non-null name.

### Dependencies

N/A - depends only on the .NET base class library.

### Callers

- **ApiMark.DotNet.DotNetNames**, **ApiMark.Cpp.CppNames**, **ApiMark.Vhdl.VhdlNames** - build and resolve the
  registry per parsed model; the emitters and link resolvers read it.
- **FileMarkdownWriterFactory** - validates each segment with `FileNameRules.IsLegalSegment`.
