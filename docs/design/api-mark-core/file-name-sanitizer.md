## FileNameSanitizer

![FileNameSanitizer Structure](ApiMarkCoreView.svg)

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

FileNameSanitizer is the single, auditable point that turns arbitrary declaration names
into file and folder name segments that are legal on both Windows and Linux. Every
language emitter calls it for every generated path segment, so the same name yields the
same segment on every platform and links always agree with the files they point to.

### Data Model

FileNameSanitizer is a `public static` class with no instance state. It holds three
immutable lookup sets: the characters replaced for portability, the Windows reserved
device names, and the hash length (eight hexadecimal characters). All members are pure
functions and are safe to call concurrently.

### Key Methods

**FileNameSanitizer.SanitizeSegment** (`public`): Converts a raw name to a legal segment.

- *Parameters*: `string rawName` — the raw name without separators or extension.
- *Returns*: `string` — a non-empty legal segment.
- *Rules*, applied in order:
  1. `<` becomes `{` and `>` becomes `}` (the brace notation of XML documentation
     identifiers); neither counts as a lossy substitution.
  2. Each character from `: " | ? * \ /`, control characters, DEL, `[ ] ( ) # % ^`,
     backtick, and space becomes `-` (lossy).
  3. Trailing dots become `-` (lossy). An empty result becomes `_`.
  4. When a lossy substitution occurred, `-` and the first eight lowercase hexadecimal
     characters of the SHA-256 of the UTF-8 original are appended so distinct originals
     stay distinct.
  5. When the part before the first dot is a reserved device name (compared
     case-insensitively, including the superscript digit variants), `-` is appended to
     that part: `CON` becomes `CON-` and `Con.Foo` becomes `Con-.Foo`.
- *Throws*: `ArgumentNullException` when `rawName` is null.

**FileNameSanitizer.IsLegalSegment** (`public`): Returns whether a segment is non-empty,
has none of `< > | ? * : " / \` or control characters, does not end in a dot or space,
and is not a reserved device name.

**FileNameSanitizer.Hash** (`public`): Returns the eight-character hash suffix used above.

### Design Decisions

- The mapping never calls `Path.GetInvalidFileNameChars`, which differs per operating
  system; fixed rules keep output identical everywhere.
- `-` is the replacement and reserved-name suffix because it cannot appear in a C#
  identifier; it is only otherwise produced by the generic-arity flattening (`Foo-2`).
  Hash suffixes are applied only when information is lost so ordinary names stay readable.
- Case-insensitive collisions between distinct names cannot be solved by a stateless
  function; callers that can see sibling names (the .NET emitter) handle them on top of
  this unit.

### Error Handling

`SanitizeSegment` throws `ArgumentNullException` for a null argument and otherwise never
throws. `IsLegalSegment` accepts null and returns `false`.

### Dependencies

N/A - FileNameSanitizer depends only on the .NET base class library
(`System.Security.Cryptography`, `System.Text`).

### Callers

- **FileMarkdownWriterFactory** — validates every sub-folder segment and file name with
  `IsLegalSegment`.
- **ApiMark.DotNet.DotNetEmitter**, **TypeLinkResolver** — sanitize member, type, and
  namespace names.
- **ApiMark.Cpp.CppEmitter** and **ApiMark.Vhdl.VhdlEmitter** — `SanitizeFileName`
  delegates to `SanitizeSegment`.
