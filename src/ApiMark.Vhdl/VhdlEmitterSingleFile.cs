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
                        var archSummary = VhdlEmitter.GetSummary(arch.Doc);
                        writer.WriteParagraph(!string.IsNullOrEmpty(archSummary)
                            ? $"**{arch.Name}**: {archSummary}"
                            : $"**{arch.Name}**");
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
                var summary = VhdlEmitter.GetSummary(pkg.Doc);
                writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : VhdlEmitter.NoDescriptionPlaceholder);

                if (pkg.Types.Count > 0)
                {
                    writer.WriteHeading(depth + 3, "Types");
                    var headers = new[] { "Name", "Definition", VhdlEmitter.DescriptionColumnHeader };
                    var rows = pkg.Types.Select(t => new[]
                    {
                        t.Name,
                        t.Definition,
                        VhdlEmitter.GetSummary(t.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder,
                    });
                    writer.WriteTable(headers, rows);
                }

                if (pkg.Constants.Count > 0)
                {
                    writer.WriteHeading(depth + 3, "Constants");
                    var headers = new[] { "Name", "Type", "Value", VhdlEmitter.DescriptionColumnHeader };
                    var rows = pkg.Constants.Select(c => new[]
                    {
                        c.Name,
                        c.TypeName,
                        c.Value ?? string.Empty,
                        VhdlEmitter.GetSummary(c.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder,
                    });
                    writer.WriteTable(headers, rows);
                }

                if (pkg.Components.Count > 0)
                {
                    writer.WriteHeading(depth + 3, "Components");
                    var headers = new[] { "Name", VhdlEmitter.DescriptionColumnHeader };
                    var rows = pkg.Components.Select(c => new[]
                    {
                        c.Name,
                        VhdlEmitter.GetSummary(c.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder,
                    });
                    writer.WriteTable(headers, rows);
                }

                if (pkg.Subprograms.Count > 0)
                {
                    writer.WriteHeading(depth + 3, "Subprograms");
                    var headers = new[] { "Name", "Kind", "Signature", VhdlEmitter.DescriptionColumnHeader };
                    var rows = pkg.Subprograms.Select(s => new[]
                    {
                        s.Name,
                        s.Kind.ToString(),
                        s.Signature,
                        VhdlEmitter.GetSummary(s.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder,
                    });
                    writer.WriteTable(headers, rows);
                }
            }
        }
    }
}
