# VhdlGenerator

<!-- All sections below are MANDATORY. -->

## Responsibility

VhdlGenerator is the public entry point for VHDL API documentation generation.
It implements `IApiGenerator`, accepts `VhdlGeneratorOptions`, enumerates all
configured VHDL source files, delegates parsing to `VhdlAstParser`, and returns
a `VhdlEmitter` ready to produce Markdown output.

## Interface

```csharp
public sealed class VhdlGenerator : IApiGenerator
{
    public VhdlGenerator(VhdlGeneratorOptions options);
    public IApiEmitter Parse(IContext context);
}
```

The constructor validates that `options` is not null and that `LibraryName` is
non-empty; it throws `ArgumentNullException` or `ArgumentException` respectively.
`Parse` enumerates source files, calls `VhdlAstParser.Parse` for each, and
returns a `VhdlEmitter` holding the results.

## Algorithm

1. Validate options and LibraryName at construction time.
2. In `Parse`:
   a. Collect all explicit SourceFiles entries (non-whitespace).
   b. For each SourceDirectory entry, enumerate `*.vhd` and `*.vhdl` files recursively.
   c. Call `VhdlAstParser.Parse(filePath)` for each collected file.
   d. Construct and return `new VhdlEmitter(options, fileModels)`.

## Design Decisions

- Source file enumeration uses `Directory.GetFiles(dir, "*.vhd", SearchOption.AllDirectories)`
  and also `*.vhdl` to cover both common VHDL file extensions.
- Missing source directories emit a warning via `context.WriteError` rather than
  throwing, so partial configurations degrade gracefully.
- The constructor performs eager validation so that misconfigured generators fail
  at construction time, not at parse time.

## Dependencies

- `VhdlAstParser` (internal): used to parse each .vhd file.
- `VhdlEmitter` (internal): constructed and returned from `Parse`.
- `ApiMark.Core.IApiGenerator`: the interface this class implements.
