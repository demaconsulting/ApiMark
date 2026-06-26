using ApiMark.Core;
using Mono.Cecil;
using static ApiMark.DotNet.DotNetEmitter;

namespace ApiMark.DotNet;

/// <summary>
///     Writes all .NET API documentation into a single <c>api.md</c> file using heading
///     levels offset by <see cref="EmitConfig.HeadingDepth"/>.
/// </summary>
/// <remarks>
///     Created exclusively by <see cref="DotNetEmitter.Emit"/> when the requested output
///     format is <see cref="OutputFormat.SingleFile"/>.
/// </remarks>
internal sealed class DotNetEmitterSingleFile
{
    /// <summary>Parent emitter providing shared helper methods and the data model.</summary>
    private readonly DotNetEmitter _emitter;

    /// <summary>Pre-parsed assembly data.</summary>
    private readonly DotNetAstModel _model;

    /// <summary>
    ///     Initializes a new <see cref="DotNetEmitterSingleFile"/>.
    /// </summary>
    /// <param name="emitter">Parent emitter providing shared helpers.</param>
    /// <param name="model">Pre-parsed assembly data.</param>
    internal DotNetEmitterSingleFile(DotNetEmitter emitter, DotNetAstModel model)
    {
        _emitter = emitter;
        _model = model;
    }

    /// <summary>Dispatches to <see cref="EmitSingleFile"/>.</summary>
    /// <param name="factory">Factory for creating the single output writer.</param>
    /// <param name="config">Output configuration controlling heading depth.</param>
    /// <param name="context">Accepted for dispatch symmetry with other emitters; not used by the single-file emitter.</param>
    internal void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
    {
        EmitSingleFile(factory, config);
    }

    // =========================================================================
    // Single-file emitter
    // =========================================================================

    /// <summary>
    ///     Emits all API documentation into a single <c>api.md</c> file using heading levels
    ///     offset by <see cref="EmitConfig.HeadingDepth"/>.
    /// </summary>
    /// <remarks>
    ///     Structure: H{depth} assembly title, H{depth+1} namespace, H{depth+2} type
    ///     (with prototype and compact member bullet list), H{depth+3} individual members.
    ///     No gradual-disclosure navigation tables or path-convention appendix are emitted.
    ///     A <see cref="TypeLinkResolver"/> with <c>generateLinks: false</c> is used so that
    ///     parameter type cells contain plain text rather than relative file links that are
    ///     meaningless inside a single document.
    /// </remarks>
    private void EmitSingleFile(IMarkdownWriterFactory factory, EmitConfig config)
    {
        var depth = config.HeadingDepth;

        // All output goes into a single api.md writer — no per-namespace or per-type files
        using var writer = factory.CreateMarkdown("", "api");
        writer.WriteHeading(depth, _model.Assembly.Name.Name + " API Reference");

        // Emit the assembly description when the AssemblyDescriptionAttribute is present
        var assemblyDescription = _model.Assembly.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Reflection.AssemblyDescriptionAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;
        if (!string.IsNullOrWhiteSpace(assemblyDescription))
        {
            writer.WriteParagraph(assemblyDescription);
        }

        // Use a no-link resolver — intra-assembly file links are meaningless in a single document
        // because all content is inline and same-name anchors can collide across types
        var noLinkResolver = new TypeLinkResolver(_model.RootNamespaces, generateLinks: false);

        // Allocate a single throw-away accumulator for all Linkify calls — generateLinks is false so
        // the set is never populated, and no External Types section is emitted in single-file output
        var sharedExternalTypes = new SortedSet<ExternalTypeInfo>();

        foreach (var namespaceName in _model.AllNamespaces)
        {
            writer.WriteHeading(depth + 1, namespaceName);

            // Emit the namespace summary when one was supplied via the NamespaceDoc convention
            if (_model.NamespaceDescriptions.TryGetValue(namespaceName, out var nsSummary) &&
                !string.IsNullOrEmpty(nsSummary))
            {
                writer.WriteParagraph(nsSummary);
            }

            if (!_model.ByNamespace.TryGetValue(namespaceName, out var nsTypes) || nsTypes.Count == 0)
            {
                continue;
            }

            var namespaceFolderPath = GetNamespaceFolderPath(namespaceName, _model.RootNamespaces);

            foreach (var type in nsTypes)
            {
                WriteSingleFileTypeSections(writer, depth, namespaceName, namespaceFolderPath, type, noLinkResolver, sharedExternalTypes);
            }
        }
    }

    /// <summary>
    ///     Emits an H{depth+2} section for a single type, including the C# prototype, summary,
    ///     remarks, type-level example blocks, a compact member bullet list, and H{depth+3}
    ///     sections for each visible member. Recursively emits visible nested types afterward.
    /// </summary>
    /// <param name="writer">The shared single-file Markdown writer.</param>
    /// <param name="depth">Top-level heading depth from <see cref="EmitConfig.HeadingDepth"/>.</param>
    /// <param name="namespaceName">Fully qualified namespace name of <paramref name="type"/>.</param>
    /// <param name="namespaceFolderPath">File-system folder path for the namespace.</param>
    /// <param name="type">The type definition to document.</param>
    /// <param name="resolver">No-link type resolver for parameter type cells.</param>
    /// <param name="sharedExternalTypes">
    ///     Shared throw-away accumulator passed to all <see cref="TypeLinkResolver.Linkify"/> calls.
    ///     Never populated because <c>generateLinks</c> is <c>false</c>; reused across all members
    ///     to avoid allocating a new set per call.
    /// </param>
    private void WriteSingleFileTypeSections(
        IMarkdownWriter writer,
        int depth,
        string namespaceName,
        string namespaceFolderPath,
        TypeDefinition type,
        TypeLinkResolver resolver,
        SortedSet<ExternalTypeInfo> sharedExternalTypes)
    {
        writer.WriteHeading(depth + 2, StripArity(type.Name));

        // Note the parent class for nested types so readers have context
        if (type.IsNested)
        {
            var parentTypeName = StripArity(type.DeclaringType.Name);
            writer.WriteParagraph($"Nested type of `{parentTypeName}`.");
        }

        // Emit the C# declaration signature as a fenced code block
        var typeSignature = BuildTypeSignature(type, namespaceName);
        writer.WriteSignature("csharp", typeSignature);

        var typeMemberId = BuildTypeId(type);

        // Always emit a summary paragraph — use the placeholder when no doc is present
        var typeSummary = _model.XmlDocs.GetSummary(typeMemberId);
        writer.WriteParagraph(!string.IsNullOrEmpty(typeSummary) ? typeSummary : DotNetEmitter.NoDescriptionPlaceholder);

        var typeRemarks = _model.XmlDocs.GetRemarks(typeMemberId);
        if (!string.IsNullOrEmpty(typeRemarks))
        {
            writer.WriteParagraph(typeRemarks);
        }

        // Emit structured example blocks for this type
        foreach (var (isCode, content) in _model.XmlDocs.GetExampleParts(typeMemberId))
        {
            if (isCode)
            {
                writer.WriteCodeBlock("csharp", content);
            }
            else
            {
                writer.WriteParagraph(content);
            }
        }

        // Delegates carry all their information in the declaration; no member listing is needed
        if (IsDelegate(type))
        {
            return;
        }

        // Collect visible members: constructors first, then alphabetically
        var allMembers = _emitter.GetVisibleMembers(type)
            .OrderBy(m => m.Name == DotNetEmitter.ConstructorMethodName ? 0 : 1)
            .ThenBy(m => m.Name)
            .ToList();

        if (allMembers.Count > 0)
        {
            // Compact bullet list (no anchor links — names can collide across types in single file)
            var bulletLines = allMembers.Select(member =>
            {
                var memberId = BuildMemberId(member);
                var memberSummary = _model.XmlDocs.GetSummary(memberId) ?? DotNetEmitter.NoDescriptionPlaceholder;
                var memberDisplayName = GetMemberDisplayName(member);
                return $"- **{memberDisplayName}**: {memberSummary}";
            });
            writer.WriteParagraph(string.Join("\n", bulletLines));

            // H{depth+3} section for each member
            foreach (var member in allMembers)
            {
                var memberId = BuildMemberId(member);
                WriteSingleFileMemberSection(
                    writer,
                    depth,
                    member,
                    memberId,
                    _model.XmlDocs,
                    resolver,
                    namespaceName,
                    namespaceFolderPath,
                    sharedExternalTypes);
            }
        }

        // Recursively emit visible nested types
        WriteSingleFileNestedTypes(writer, depth, namespaceName, namespaceFolderPath, type, resolver, sharedExternalTypes);
    }

    /// <summary>
    ///     Emits an H{depth+3} section for a single member, including the C# signature,
    ///     summary, parameter table (for methods), returns, exceptions, and example blocks.
    /// </summary>
    /// <param name="writer">The shared single-file Markdown writer.</param>
    /// <param name="depth">Top-level heading depth from <see cref="EmitConfig.HeadingDepth"/>.</param>
    /// <param name="member">The member definition to document.</param>
    /// <param name="memberId">Pre-computed XML doc member identifier for <paramref name="member"/>.</param>
    /// <param name="xmlDocs">XML documentation reader for lookups.</param>
    /// <param name="resolver">No-link type resolver for parameter type cells.</param>
    /// <param name="namespaceName">Fully qualified namespace name for signature simplification.</param>
    /// <param name="namespaceFolderPath">File-system folder path for the namespace.</param>
    /// <param name="sharedExternalTypes">
    ///     Shared throw-away accumulator passed to all <see cref="TypeLinkResolver.Linkify"/> calls.
    ///     Never populated because <c>generateLinks</c> is <c>false</c>; reused across all parameters
    ///     and members to avoid allocating a new set per <c>Linkify</c> call.
    /// </param>
    private static void WriteSingleFileMemberSection(
        IMarkdownWriter writer,
        int depth,
        IMemberDefinition member,
        string memberId,
        XmlDocReader xmlDocs,
        TypeLinkResolver resolver,
        string namespaceName,
        string namespaceFolderPath,
        SortedSet<ExternalTypeInfo> sharedExternalTypes)
    {
        var displayName = GetMemberDisplayName(member);
        writer.WriteHeading(depth + 3, displayName);

        // Emit the C# declaration signature as a fenced code block
        var signature = BuildMemberSignature(member, namespaceName);
        writer.WriteSignature("csharp", signature);

        // Always emit a summary paragraph — use the placeholder when no doc is present
        var summary = xmlDocs.GetSummary(memberId);
        writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : DotNetEmitter.NoDescriptionPlaceholder);

        // Emit parameter table for methods with parameters
        if (member is MethodDefinition method && method.HasParameters)
        {
            var paramDocs = xmlDocs.GetParams(memberId);
            var paramHeaders = new[] { "Parameter", "Type", DotNetEmitter.DescriptionColumnHeader };
            var paramRows = method.Parameters.Select(p =>
            {
                var desc = paramDocs.FirstOrDefault(pd => pd.Name == p.Name).Description ?? NoDescriptionPlaceholder;
                var typeName = resolver.Linkify(p.ParameterType, namespaceFolderPath, namespaceName,
                    // Shared throw-away accumulator — generateLinks is false so it is never populated or read
                    sharedExternalTypes);
                return new[] { p.Name, typeName, desc };
            });
            writer.WriteTable(paramHeaders, paramRows);
        }

        var returns = xmlDocs.GetReturns(memberId);
        if (!string.IsNullOrEmpty(returns))
        {
            writer.WriteParagraph($"**Returns:** {returns}");
        }

        // Emit exception table when documented exceptions exist
        var exceptions = xmlDocs.GetExceptionDetails(memberId);
        if (exceptions.Count > 0)
        {
            var exHeaders = new[] { "Exception", DotNetEmitter.DescriptionColumnHeader };
            var exRows = exceptions.Select(e => new[] { e.Type, e.Description ?? string.Empty });
            writer.WriteTable(exHeaders, exRows);
        }

        // Emit structured example blocks for this member
        foreach (var (isCode, content) in xmlDocs.GetExampleParts(memberId))
        {
            if (isCode)
            {
                writer.WriteCodeBlock("csharp", content);
            }
            else
            {
                writer.WriteParagraph(content);
            }
        }
    }

    /// <summary>
    ///     Recursively emits H{depth+2} sections for all visible nested types of
    ///     <paramref name="type"/> by calling
    ///     <see cref="WriteSingleFileTypeSections"/> for each nested type.
    /// </summary>
    /// <param name="writer">The shared single-file Markdown writer.</param>
    /// <param name="depth">Top-level heading depth from <see cref="EmitConfig.HeadingDepth"/>.</param>
    /// <param name="namespaceName">Fully qualified namespace name for signature simplification.</param>
    /// <param name="namespaceFolderPath">File-system folder path for the namespace.</param>
    /// <param name="type">The containing type whose nested types are to be emitted.</param>
    /// <param name="resolver">No-link type resolver for parameter type cells.</param>
    /// <param name="sharedExternalTypes">
    ///     Shared throw-away accumulator threaded through all <see cref="TypeLinkResolver.Linkify"/>
    ///     calls for nested type members.
    /// </param>
    private void WriteSingleFileNestedTypes(
        IMarkdownWriter writer,
        int depth,
        string namespaceName,
        string namespaceFolderPath,
        TypeDefinition type,
        TypeLinkResolver resolver,
        SortedSet<ExternalTypeInfo> sharedExternalTypes)
    {
        foreach (var nested in _emitter.GetVisibleNestedTypes(type))
        {
            WriteSingleFileTypeSections(writer, depth, namespaceName, namespaceFolderPath, nested, resolver, sharedExternalTypes);
        }
    }
}
