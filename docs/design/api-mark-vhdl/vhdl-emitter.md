# VhdlEmitter

<!-- All sections below are MANDATORY. -->

## Responsibility

VhdlEmitter implements `IApiEmitter`, validates the mandatory `factory` argument,
and dispatches to `VhdlEmitterGradualDisclosure` or `VhdlEmitterSingleFile` based
on `config.Format`.

## Interface

```csharp
internal sealed class VhdlEmitter : IApiEmitter
{
    internal const string DescriptionColumnHeader = "Description";
    internal const string NoDescriptionPlaceholder = "*No description provided.*";
    internal VhdlEmitter(VhdlGeneratorOptions options, IReadOnlyList<VhdlFileModel> fileModels);
    internal VhdlGeneratorOptions Options { get; }
    public void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context);
    internal static string? GetSummary(VhdlDocComment? doc);
}
```

## Algorithm

1. `Emit` validates `factory` is not null (throws `ArgumentNullException`).
2. If `config.Format == OutputFormat.SingleFile`, delegate to `new VhdlEmitterSingleFile(this, _fileModels).Emit(factory, config, context)`.
3. Otherwise, delegate to `new VhdlEmitterGradualDisclosure(this, _fileModels).Emit(factory, config, context)`.

## Design Decisions

- Shared constants (`DescriptionColumnHeader`, `NoDescriptionPlaceholder`) are defined
  on `VhdlEmitter` so both format-specific emitters can reference them without duplication.
- `GetSummary` is a static helper that safely extracts `doc?.Summary` for use in table cells.
