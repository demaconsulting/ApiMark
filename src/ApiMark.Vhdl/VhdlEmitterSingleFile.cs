using ApiMark.Core;
using ApiMark.Vhdl.VhdlAst;

namespace ApiMark.Vhdl;

/// <summary>Single-file emitter for VHDL API documentation.</summary>
internal sealed class VhdlEmitterSingleFile
{
    private readonly VhdlEmitter _emitter;
    private readonly IReadOnlyList<VhdlFileModel> _fileModels;

    /// <summary>Initializes a new VhdlEmitterSingleFile.</summary>
    /// <param name="emitter">The parent VhdlEmitter providing shared options and helpers.</param>
    /// <param name="fileModels">Parsed file models to emit.</param>
    internal VhdlEmitterSingleFile(VhdlEmitter emitter, IReadOnlyList<VhdlFileModel> fileModels)
    {
        _emitter = emitter;
        _fileModels = fileModels;
    }

    /// <summary>Emits all VHDL documentation into a single api.md file.</summary>
    /// <param name="factory">Factory for creating Markdown writers.</param>
    /// <param name="config">Emit configuration (format and heading depth).</param>
    /// <param name="context">Output channel for progress and error messages.</param>
    internal void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
    {
        // Suppress unused parameter warning
        _ = context;

        var depth = config.HeadingDepth;

        var allEntities = _fileModels.SelectMany(f => f.Entities).ToList();
        var allArchitectures = _fileModels.SelectMany(f => f.Architectures).ToList();
        var allPackages = _fileModels.SelectMany(f => f.Packages).ToList();

        using var writer = factory.CreateMarkdown("", "api");
        writer.WriteHeading(depth, $"{_emitter.Options.LibraryName} API Reference");

        if (!string.IsNullOrWhiteSpace(_emitter.Options.Description))
        {
            writer.WriteParagraph(_emitter.Options.Description);
        }

        // Entities section
        if (allEntities.Count > 0)
        {
            writer.WriteHeading(depth + 1, "Entities");
            foreach (var entity in allEntities)
            {
                writer.WriteHeading(depth + 2, entity.Name);

                var summary = VhdlEmitter.GetSummary(entity.Doc);
                writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : VhdlEmitter.NoDescriptionPlaceholder);

                var details = entity.Doc?.Details;
                if (!string.IsNullOrEmpty(details))
                {
                    writer.WriteParagraph(details);
                }

                if (entity.Generics.Count > 0)
                {
                    writer.WriteHeading(depth + 3, "Generics");
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
                    writer.WriteHeading(depth + 3, "Ports");
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

                var archsForEntity = allArchitectures
                    .Where(a => string.Equals(a.EntityName, entity.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (archsForEntity.Count > 0)
                {
                    writer.WriteHeading(depth + 3, "Architectures");
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
        }

        // Packages section
        if (allPackages.Count > 0)
        {
            writer.WriteHeading(depth + 1, "Packages");
            foreach (var pkg in allPackages)
            {
                writer.WriteHeading(depth + 2, pkg.Name);

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
                    writer.WriteHeading(depth + 3, "Types");
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
                    writer.WriteHeading(depth + 3, "Constants");
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
                    writer.WriteHeading(depth + 3, "Components");
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

                // Emit subprograms as individual sub-sections at depth+3 — each subprogram replaces the old table row
                foreach (var s in pkg.Subprograms)
                {
                    writer.WriteHeading(depth + 3, s.Name);

                    // Summary paragraph
                    var subSummary = VhdlEmitter.GetSummary(s.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder;
                    writer.WriteParagraph(subSummary);

                    // Optional extended details paragraph
                    var subDetails = s.Doc?.Details;
                    if (!string.IsNullOrEmpty(subDetails))
                    {
                        writer.WriteParagraph(subDetails);
                    }

                    // Parameters table at depth+4 if the subprogram has parsed formal parameters
                    if (s.Parameters.Count > 0)
                    {
                        writer.WriteHeading(depth + 4, "Parameters");
                        var headers = new[] { "Name", "Mode", "Type", VhdlEmitter.DescriptionColumnHeader };
                        var rows = s.Parameters.Select(p => new[]
                        {
                            p.Name,
                            p.Mode,
                            p.TypeName,
                            s.Doc?.Params.FirstOrDefault(pd => pd.Name == p.Name)?.Description ?? VhdlEmitter.NoDescriptionPlaceholder,
                        });
                        writer.WriteTable(headers, rows);
                    }

                    // Returns section at depth+4 if this is a function (ReturnType is non-null)
                    if (s.ReturnType != null)
                    {
                        writer.WriteHeading(depth + 4, "Returns");
                        writer.WriteParagraph(s.Doc?.Returns ?? VhdlEmitter.NoDescriptionPlaceholder);
                    }

                    // Signature section at depth+4 is always emitted as a code-span paragraph
                    writer.WriteHeading(depth + 4, "Signature");
                    writer.WriteParagraph($"`{s.Signature}`");
                }
            }
        }
    }
}
