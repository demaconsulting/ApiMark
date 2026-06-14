using ApiMark.Core;
using ApiMark.Vhdl.VhdlAst;

namespace ApiMark.Vhdl;

/// <summary>Gradual-disclosure emitter for VHDL API documentation.</summary>
internal sealed class VhdlEmitterGradualDisclosure
{
    private readonly VhdlEmitter _emitter;
    private readonly IReadOnlyList<VhdlFileModel> _fileModels;

    /// <summary>Initializes a new VhdlEmitterGradualDisclosure.</summary>
    /// <param name="emitter">The parent VhdlEmitter providing shared options and helpers.</param>
    /// <param name="fileModels">Parsed file models to emit.</param>
    internal VhdlEmitterGradualDisclosure(VhdlEmitter emitter, IReadOnlyList<VhdlFileModel> fileModels)
    {
        _emitter = emitter;
        _fileModels = fileModels;
    }

    /// <summary>Emits gradual-disclosure Markdown output: one file per entity/architecture/package plus an api index.</summary>
    /// <param name="factory">Factory for creating per-file Markdown writers.</param>
    /// <param name="config">Emit configuration (format and heading depth).</param>
    /// <param name="context">Output channel for progress and error messages.</param>
    internal void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
    {
        // Suppress unused parameter warning
        _ = config;
        _ = context;

        // Collect all entities, architectures, packages across all files
        var allEntities = _fileModels.SelectMany(f => f.Entities).ToList();
        var allArchitectures = _fileModels.SelectMany(f => f.Architectures).ToList();
        var allPackages = _fileModels.SelectMany(f => f.Packages).ToList();

        // Emit api.md index page
        using (var writer = factory.CreateMarkdown("", "api"))
        {
            writer.WriteHeading(1, $"{_emitter.Options.LibraryName} API Reference");

            if (!string.IsNullOrWhiteSpace(_emitter.Options.Description))
            {
                writer.WriteParagraph(_emitter.Options.Description);
            }

            if (allEntities.Count > 0)
            {
                writer.WriteHeading(2, "Entities");
                var headers = new[] { "Name", VhdlEmitter.DescriptionColumnHeader };
                var rows = allEntities.Select(e =>
                    new[] { $"[{e.Name}]({VhdlEmitter.SanitizeFileName(e.Name)}.md)", VhdlEmitter.GetSummary(e.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder });
                writer.WriteTable(headers, rows);
            }

            if (allArchitectures.Count > 0)
            {
                writer.WriteHeading(2, "Architectures");
                var headers = new[] { "Name", "Entity", VhdlEmitter.DescriptionColumnHeader };
                var rows = allArchitectures.Select(a =>
                {
                    var fileName = $"{VhdlEmitter.SanitizeFileName(a.Name)}_{VhdlEmitter.SanitizeFileName(a.EntityName)}_arch";
                    return new[] { $"[{a.Name}]({fileName}.md)", a.EntityName, VhdlEmitter.GetSummary(a.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder };
                });
                writer.WriteTable(headers, rows);
            }

            if (allPackages.Count > 0)
            {
                writer.WriteHeading(2, "Packages");
                var headers = new[] { "Name", VhdlEmitter.DescriptionColumnHeader };
                var rows = allPackages.Select(p =>
                    new[] { $"[{p.Name}]({VhdlEmitter.SanitizeFileName(p.Name)}.md)", VhdlEmitter.GetSummary(p.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder });
                writer.WriteTable(headers, rows);
            }
        }

        // Emit entity detail pages
        foreach (var entity in allEntities)
        {
            using var writer = factory.CreateMarkdown("", VhdlEmitter.SanitizeFileName(entity.Name));
            writer.WriteHeading(1, entity.Name);

            var summary = VhdlEmitter.GetSummary(entity.Doc);
            if (!string.IsNullOrEmpty(summary))
            {
                writer.WriteParagraph(summary);
            }

            var details = entity.Doc?.Details;
            if (!string.IsNullOrEmpty(details))
            {
                writer.WriteParagraph(details);
            }

            if (entity.Generics.Count > 0)
            {
                writer.WriteHeading(2, "Generics");
                var headers = new[] { "Name", "Type", "Default", VhdlEmitter.DescriptionColumnHeader };
                var rows = entity.Generics.Select(g => new[]
                {
                    g.Name,
                    g.TypeName,
                    g.DefaultValue ?? string.Empty,
                    VhdlEmitter.GetSummary(g.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder,
                });
                writer.WriteTable(headers, rows);
            }

            if (entity.Ports.Count > 0)
            {
                writer.WriteHeading(2, "Ports");
                var headers = new[] { "Name", "Direction", "Type", VhdlEmitter.DescriptionColumnHeader };
                var rows = entity.Ports.Select(p => new[]
                {
                    p.Name,
                    p.Direction,
                    p.TypeName,
                    VhdlEmitter.GetSummary(p.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder,
                });
                writer.WriteTable(headers, rows);
            }

            // List architectures that implement this entity
            var archsForEntity = allArchitectures
                .Where(a => string.Equals(a.EntityName, entity.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (archsForEntity.Count > 0)
            {
                writer.WriteHeading(2, "Architectures");
                foreach (var arch in archsForEntity)
                {
                    var archSummary = VhdlEmitter.GetSummary(arch.Doc);
                    writer.WriteParagraph(!string.IsNullOrEmpty(archSummary)
                        ? $"**{arch.Name}**: {archSummary}"
                        : $"**{arch.Name}**");
                }
            }
        }

        // Emit architecture detail pages
        foreach (var arch in allArchitectures)
        {
            var archFileName = $"{VhdlEmitter.SanitizeFileName(arch.Name)}_{VhdlEmitter.SanitizeFileName(arch.EntityName)}_arch";
            using var writer = factory.CreateMarkdown("", archFileName);
            writer.WriteHeading(1, arch.Name);
            writer.WriteParagraph($"Architecture of entity `{arch.EntityName}`.");

            var summary = VhdlEmitter.GetSummary(arch.Doc);
            if (!string.IsNullOrEmpty(summary))
            {
                writer.WriteParagraph(summary);
            }
        }

        // Emit package detail pages
        foreach (var pkg in allPackages)
        {
            using var writer = factory.CreateMarkdown("", VhdlEmitter.SanitizeFileName(pkg.Name));
            writer.WriteHeading(1, pkg.Name);

            var summary = VhdlEmitter.GetSummary(pkg.Doc);
            writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : VhdlEmitter.NoDescriptionPlaceholder);
        }
    }
}
