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

            var summary = VhdlEmitter.GetSummary(entity.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder;
            writer.WriteParagraph(summary);

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
                    // Emit architecture summary as bold-name paragraph
                    var archSummary = VhdlEmitter.GetSummary(arch.Doc);
                    writer.WriteParagraph(!string.IsNullOrEmpty(archSummary)
                        ? $"**{arch.Name}**: {archSummary}"
                        : $"**{arch.Name}**");

                    // Emit extended architecture details as a follow-on paragraph if present
                    var archDetails = arch.Doc?.Details;
                    if (!string.IsNullOrEmpty(archDetails))
                    {
                        writer.WriteParagraph(archDetails);
                    }
                }
            }
        }

        // Emit package detail pages and per-subprogram detail files
        foreach (var pkg in allPackages)
        {
            using var writer = factory.CreateMarkdown("", VhdlEmitter.SanitizeFileName(pkg.Name));
            writer.WriteHeading(1, pkg.Name);

            // Emit package summary paragraph
            var summary = VhdlEmitter.GetSummary(pkg.Doc);
            writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : VhdlEmitter.NoDescriptionPlaceholder);

            // Emit extended package details as a second paragraph if present
            var pkgDetails = pkg.Doc?.Details;
            if (!string.IsNullOrEmpty(pkgDetails))
            {
                writer.WriteParagraph(pkgDetails);
            }

            // Emit types in paragraph-per-type format: bold name, em-dash, definition, then summary
            if (pkg.Types.Count > 0)
            {
                writer.WriteHeading(2, "Types");
                foreach (var t in pkg.Types)
                {
                    // First paragraph: bold name, em-dash, backtick-wrapped definition
                    writer.WriteParagraph($"**{t.Name}** — `{t.Definition}`");

                    // Second paragraph: summary or placeholder
                    var typeSummary = VhdlEmitter.GetSummary(t.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder;
                    writer.WriteParagraph(typeSummary);

                    // Optional extended details paragraph
                    var typeDetails = t.Doc?.Details;
                    if (!string.IsNullOrEmpty(typeDetails))
                    {
                        writer.WriteParagraph(typeDetails);
                    }
                }
            }

            // Emit constants in paragraph-per-constant format: bold name, colon, type, optional value, then summary
            if (pkg.Constants.Count > 0)
            {
                writer.WriteHeading(2, "Constants");
                foreach (var c in pkg.Constants)
                {
                    // First paragraph: bold name, colon, backtick-wrapped type, equals and backtick-wrapped value if present
                    var constHeader = string.IsNullOrEmpty(c.Value)
                        ? $"**{c.Name}** : `{c.TypeName}`"
                        : $"**{c.Name}** : `{c.TypeName}` = `{c.Value}`";
                    writer.WriteParagraph(constHeader);

                    // Second paragraph: summary or placeholder
                    var constSummary = VhdlEmitter.GetSummary(c.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder;
                    writer.WriteParagraph(constSummary);

                    // Optional extended details paragraph
                    var constDetails = c.Doc?.Details;
                    if (!string.IsNullOrEmpty(constDetails))
                    {
                        writer.WriteParagraph(constDetails);
                    }
                }
            }

            // Emit components in paragraph-per-component format: bold name, then summary
            if (pkg.Components.Count > 0)
            {
                writer.WriteHeading(2, "Components");
                foreach (var c in pkg.Components)
                {
                    // First paragraph: bold component name
                    writer.WriteParagraph($"**{c.Name}**");

                    // Second paragraph: summary or placeholder
                    var compSummary = VhdlEmitter.GetSummary(c.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder;
                    writer.WriteParagraph(compSummary);

                    // Optional extended details paragraph
                    var compDetails = c.Doc?.Details;
                    if (!string.IsNullOrEmpty(compDetails))
                    {
                        writer.WriteParagraph(compDetails);
                    }
                }
            }

            // Emit subprograms as linked paragraphs pointing to per-subprogram detail files
            if (pkg.Subprograms.Count > 0)
            {
                writer.WriteHeading(2, "Subprograms");
                foreach (var s in pkg.Subprograms)
                {
                    // Paragraph: linked name and kind label
                    var detailFile = $"{VhdlEmitter.SanitizeFileName(pkg.Name)}_{VhdlEmitter.SanitizeFileName(s.Name)}.md";
                    var kindText = s.Kind == VhdlSubprogramKind.Function ? "Function" : "Procedure";
                    writer.WriteParagraph($"**[{s.Name}]({detailFile})** — *{kindText}*");

                    // Summary paragraph
                    var subSummary = VhdlEmitter.GetSummary(s.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder;
                    writer.WriteParagraph(subSummary);
                }
            }

            // Emit one detail file per subprogram with full documentation
            foreach (var s in pkg.Subprograms)
            {
                var detailFileName = $"{VhdlEmitter.SanitizeFileName(pkg.Name)}_{VhdlEmitter.SanitizeFileName(s.Name)}";
                using var subWriter = factory.CreateMarkdown("", detailFileName);
                subWriter.WriteHeading(1, s.Name);

                // Attribution: kind and owning package name
                var kindText = s.Kind == VhdlSubprogramKind.Function ? "Function" : "Procedure";
                subWriter.WriteParagraph($"*{kindText} in `{pkg.Name}`*");

                // Summary paragraph
                var subSummary = VhdlEmitter.GetSummary(s.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder;
                subWriter.WriteParagraph(subSummary);

                // Optional extended details paragraph
                var subDetails = s.Doc?.Details;
                if (!string.IsNullOrEmpty(subDetails))
                {
                    subWriter.WriteParagraph(subDetails);
                }

                // Parameters table if doc params are present
                if (s.Doc?.Params is { Count: > 0 } subParams)
                {
                    subWriter.WriteHeading(2, "Parameters");
                    var headers = new[] { "Name", "Description" };
                    var rows = subParams.Select(p => new[] { p.Name, p.Description });
                    subWriter.WriteTable(headers, rows);
                }

                // Returns paragraph if doc returns text is present
                var returns = s.Doc?.Returns;
                if (!string.IsNullOrEmpty(returns))
                {
                    subWriter.WriteHeading(2, "Returns");
                    subWriter.WriteParagraph(returns);
                }

                // Signature section is always emitted as a code-span paragraph
                subWriter.WriteHeading(2, "Signature");
                subWriter.WriteParagraph($"`{s.Signature}`");
            }
        }
    }
}
