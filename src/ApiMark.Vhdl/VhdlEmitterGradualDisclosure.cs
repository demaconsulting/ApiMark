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

    /// <summary>Emits gradual-disclosure Markdown output: one file per entity/package plus an api index, with architectures rendered inline on entity pages.</summary>
    /// <param name="factory">Factory for creating per-file Markdown writers.</param>
    /// <param name="config">Accepted for interface-signature consistency; not consumed — format selection is performed upstream by VhdlEmitter.</param>
    /// <param name="context">Accepted for interface-signature consistency; not consumed by this implementation.</param>
    internal void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
    {
        // Suppress unused parameter warning
        _ = config;
        _ = context;

        // Collect all entities, architectures (with source filename), packages across all files
        var allEntities = _fileModels
            .SelectMany(f => f.Entities.Select(e => (Entity: e, FileName: Path.GetFileName(f.FilePath))))
            .ToList();
        var allArchitectures = _fileModels
            .SelectMany(f => f.Architectures.Select(a => (Arch: a, FileName: Path.GetFileName(f.FilePath))))
            .ToList();
        var allPackages = _fileModels
            .SelectMany(f => f.Packages.Select(p => (Package: p, FileName: Path.GetFileName(f.FilePath))))
            .ToList();

        // Emit api.md index page
        EmitApiIndexPage(factory, allEntities.Select(t => t.Entity).ToList(), allPackages.Select(t => t.Package).ToList());

        // Emit entity detail pages
        foreach (var (entity, fileName) in allEntities)
        {
            EmitEntityPage(factory, entity, fileName, allArchitectures);
        }

        // Emit package detail pages and per-subprogram detail files
        foreach (var (pkg, fileName) in allPackages)
        {
            EmitPackagePage(factory, pkg, fileName);
        }
    }

    /// <summary>
    ///     Emits the api.md index page listing all entities and packages with navigation links
    ///     to their individual detail pages.
    /// </summary>
    /// <param name="factory">Factory for creating the Markdown writer.</param>
    /// <param name="allEntities">All entity declarations across all parsed files.</param>
    /// <param name="allPackages">All package declarations across all parsed files.</param>
    private void EmitApiIndexPage(
        IMarkdownWriterFactory factory,
        List<VhdlEntityDecl> allEntities,
        List<VhdlPackageDecl> allPackages)
    {
        using var writer = factory.CreateMarkdown("", "api");
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

    /// <summary>
    ///     Emits the detail page for a single VHDL entity, including its summary, generics,
    ///     ports, and the architectures that implement it.
    /// </summary>
    /// <param name="factory">Factory for creating the per-entity Markdown writer.</param>
    /// <param name="entity">The entity declaration to emit.</param>
    /// <param name="fileName">Base filename of the source file containing this entity declaration.</param>
    /// <param name="allArchitectures">
    ///     All architecture declarations across all parsed files; filtered to those
    ///     whose entity name matches <paramref name="entity"/>.
    /// </param>
    private static void EmitEntityPage(
        IMarkdownWriterFactory factory,
        VhdlEntityDecl entity,
        string fileName,
        List<(VhdlArchitectureDecl Arch, string FileName)> allArchitectures)
    {
        using var writer = factory.CreateMarkdown("", VhdlEmitter.SanitizeFileName(entity.Name));
        writer.WriteHeading(1, entity.Name);

        // Attribution: kind and source file
        writer.WriteParagraph($"*Entity declared in `{fileName}`*");

        var summary = VhdlEmitter.GetSummary(entity.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder;
        writer.WriteParagraph(summary);

        var details = entity.Doc?.Details;
        if (!string.IsNullOrEmpty(details))
        {
            writer.WriteParagraph(details);
        }

        writer.WriteHeading(2, "Generics");
        if (entity.Generics.Count > 0)
        {
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
        else
        {
            writer.WriteParagraph(VhdlEmitter.NoItemsPlaceholder);
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
            .Where(t => string.Equals(t.Arch.EntityName, entity.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (archsForEntity.Count > 0)
        {
            writer.WriteHeading(2, "Architectures");
            foreach (var (arch, archFileName) in archsForEntity)
            {
                // Emit architecture as: **name** (`filename`): summary  or  **name** (`filename`)
                var archSummary = VhdlEmitter.GetSummary(arch.Doc);
                writer.WriteParagraph(!string.IsNullOrEmpty(archSummary)
                    ? $"**{arch.Name}** (`{archFileName}`): {archSummary}"
                    : $"**{arch.Name}** (`{archFileName}`)");

                // Emit extended architecture details as a follow-on paragraph if present
                var archDetails = arch.Doc?.Details;
                if (!string.IsNullOrEmpty(archDetails))
                {
                    writer.WriteParagraph(archDetails);
                }
            }
        }
    }

    /// <summary>
    ///     Emits the package detail page for a single VHDL package and spawns a per-subprogram
    ///     detail file for each subprogram declared in the package.
    /// </summary>
    /// <param name="factory">Factory for creating Markdown writers for the package page and subprogram pages.</param>
    /// <param name="pkg">The package declaration to emit.</param>
    /// <param name="fileName">Base filename of the source file containing this package declaration.</param>
    private static void EmitPackagePage(IMarkdownWriterFactory factory, VhdlPackageDecl pkg, string fileName)
    {
        using var writer = factory.CreateMarkdown("", VhdlEmitter.SanitizeFileName(pkg.Name));
        writer.WriteHeading(1, pkg.Name);

        // Attribution: kind and source file
        writer.WriteParagraph($"*Package declared in `{fileName}`*");

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

        // Emit components in paragraph-per-component format: bold name followed by summary on same line
        if (pkg.Components.Count > 0)
        {
            writer.WriteHeading(2, "Components");
            foreach (var c in pkg.Components)
            {
                // Single paragraph: bold component name em-dashed with summary to avoid standalone-bold MD036
                var compSummary = VhdlEmitter.GetSummary(c.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder;
                writer.WriteParagraph($"**{c.Name}** — {compSummary}");

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
                // Paragraph: linked name and kind label — subfolder path so the reader can navigate
                var detailFile = $"{VhdlEmitter.SanitizeFileName(pkg.Name)}/{VhdlEmitter.SanitizeFileName(s.Name)}.md";
                var kindText = s.Kind == VhdlSubprogramKind.Function ? "Function" : "Procedure";
                writer.WriteParagraph($"**[{s.Name}]({detailFile})** — *{kindText}*");

                // Summary paragraph
                var subSummary = VhdlEmitter.GetSummary(s.Doc) ?? VhdlEmitter.NoDescriptionPlaceholder;
                writer.WriteParagraph(subSummary);
            }
        }

        // Emit one detail file per subprogram with full documentation — placed in a per-package subfolder
        foreach (var s in pkg.Subprograms)
        {
            EmitSubprogramDetailPage(factory, pkg, s);
        }
    }

    /// <summary>
    ///     Emits the per-subprogram detail file for a single VHDL subprogram within a package,
    ///     including attribution, summary, parameters, return type, and signature.
    /// </summary>
    /// <param name="factory">Factory for creating the per-subprogram Markdown writer.</param>
    /// <param name="pkg">The owning package declaration, used for attribution and folder naming.</param>
    /// <param name="s">The subprogram declaration to emit.</param>
    private static void EmitSubprogramDetailPage(
        IMarkdownWriterFactory factory,
        VhdlPackageDecl pkg,
        VhdlSubprogramDecl s)
    {
        var pkgFolder = VhdlEmitter.SanitizeFileName(pkg.Name);
        var subFileName = VhdlEmitter.SanitizeFileName(s.Name);
        using var subWriter = factory.CreateMarkdown(pkgFolder, subFileName);
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

        // Parameters table if the subprogram has parsed formal parameters
        if (s.Parameters.Count > 0)
        {
            subWriter.WriteHeading(2, "Parameters");
            var headers = new[] { "Name", "Type", VhdlEmitter.DescriptionColumnHeader };
            var rows = s.Parameters.Select(p => new[]
            {
                p.Name,
                VhdlEmitter.FormatParamType(p),
                s.Doc?.Params.FirstOrDefault(pd => string.Equals(pd.Name, p.Name, StringComparison.OrdinalIgnoreCase))?.Description ?? VhdlEmitter.NoDescriptionPlaceholder,
            });
            subWriter.WriteTable(headers, rows);
        }

        // Returns section if this is a function (ReturnType is non-null)
        if (s.ReturnType != null)
        {
            subWriter.WriteHeading(2, "Returns");
            subWriter.WriteParagraph(s.Doc?.Returns ?? VhdlEmitter.NoDescriptionPlaceholder);
        }

        // Signature section is always emitted as a fenced vhdl code block
        subWriter.WriteHeading(2, "Signature");
        subWriter.WriteSignature("vhdl", s.Signature);
    }
}
