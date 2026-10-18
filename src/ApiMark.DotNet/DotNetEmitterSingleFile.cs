// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using Mono.Cecil;
using static ApiMark.DotNet.DotNetEmitter;

namespace ApiMark.DotNet;

/// <summary>
///     Bundles the per-member rendering context that is constant across all member sections
///     written for a single type in single-file output. Used to reduce the parameter count on
///     <see cref="DotNetEmitterSingleFile"/>'s member-section helper.
/// </summary>
/// <param name="Writer">The shared single-file Markdown writer.</param>
/// <param name="Depth">Top-level heading depth from <see cref="EmitConfig.HeadingDepth"/>.</param>
/// <param name="XmlDocs">XML documentation reader for lookups.</param>
/// <param name="Resolver">No-link type resolver for parameter type cells.</param>
/// <param name="NamespaceName">Fully qualified namespace name for signature simplification.</param>
/// <param name="NamespaceFolderPath">File-system folder path for the namespace.</param>
/// <param name="SharedExternalTypes">
///     Shared accumulator passed to all <see cref="TypeLinkResolver.Linkify(TypeReference, string, string, ISet{ExternalTypeInfo}, bool)"/> calls. Non-System
///     external types are still tracked into it regardless of <c>generateLinks</c>, but the
///     single-file emitter never reads it back; reused across all parameters and members to
///     avoid allocating a new set per <c>Linkify</c> call.
/// </param>
/// <param name="Visibility">Visibility tier used to hide non-visible property accessors in signatures.</param>
internal sealed record SingleFileMemberContext(
    IMarkdownWriter Writer,
    int Depth,
    XmlDocReader XmlDocs,
    TypeLinkResolver Resolver,
    string NamespaceName,
    string NamespaceFolderPath,
    SortedSet<ExternalTypeInfo> SharedExternalTypes,
    ApiVisibility Visibility = ApiVisibility.All);

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
    /// <summary>Column headers for the inherited-members tables.</summary>
    private static readonly string[] InheritedMemberHeaders = ["Member", "Kind"];

    /// <summary>Fenced code block / signature language identifier for C# content.</summary>
    private const string CSharpLanguageId = "csharp";

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
    ///     meaningless inside a single document. The assembly title is followed by an
    ///     introductory description paragraph resolved via
    ///     <see cref="DotNetEmitter.GetAssemblyDescription"/>, which prefers an explicitly-supplied
    ///     <see cref="DotNetGeneratorOptions.LibraryDescription"/> option over the compiled
    ///     <see cref="System.Reflection.AssemblyDescriptionAttribute"/>.
    /// </remarks>
    private void EmitSingleFile(IMarkdownWriterFactory factory, EmitConfig config)
    {
        var depth = config.HeadingDepth;

        // All output goes into a single api.md writer — no per-namespace or per-type files
        using var writer = factory.CreateMarkdown("", "api");
        writer.WriteHeading(depth, _model.Assembly.Name.Name + " API Reference");

        // Prefer an explicitly-supplied LibraryDescription option over the compiled
        // AssemblyDescriptionAttribute, falling back to the attribute only when the option
        // isn't supplied
        var assemblyDescription = GetAssemblyDescription(_model.Assembly, _model.Options.LibraryDescription);

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
            writer.WriteHeading(depth + 1, GetNamespaceDisplayName(namespaceName));

            // Emit the namespace documentation when one was supplied via the NamespaceDoc
            // convention — summary, then remarks, then structured example parts, mirroring
            // the type-level rendering in WriteSingleFileTypeSections
            if (_model.NamespaceDescriptions.TryGetValue(namespaceName, out var nsDescription))
            {
                WriteNamespaceDescription(writer, nsDescription, _model.XmlDocs);
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
    ///     Emits the summary, remarks, and structured example parts sourced from a NamespaceDoc
    ///     carrier class, mirroring the type-level rendering in <see cref="WriteSingleFileTypeSections"/>.
    /// </summary>
    /// <remarks>
    ///     No <see cref="CrefLinkContext"/> is supplied — single-file output never resolves
    ///     <c>&lt;see cref&gt;</c> to links (see the <c>noLinkResolver</c> remark in
    ///     <see cref="EmitSingleFile"/>), so cross-references render as plain code-span text.
    /// </remarks>
    /// <param name="writer">The shared single-file Markdown writer.</param>
    /// <param name="nsDescription">The namespace description to render.</param>
    /// <param name="xmlDocs">Documentation index used to fetch the summary/remarks/example text.</param>
    private static void WriteNamespaceDescription(IMarkdownWriter writer, NamespaceDescription nsDescription, XmlDocReader xmlDocs)
    {
        var summary = nsDescription.GetSummary(xmlDocs);
        if (!string.IsNullOrEmpty(summary))
        {
            writer.WriteParagraph(summary);
        }

        var remarks = nsDescription.GetRemarks(xmlDocs);
        if (!string.IsNullOrEmpty(remarks))
        {
            writer.WriteParagraph(remarks);
        }

        WriteExampleParts(writer, nsDescription.GetExampleParts(xmlDocs));
    }

    /// <summary>
    ///     Emits a sequence of structured example parts, rendering code parts as fenced code
    ///     blocks tagged with each part's language (default C#) and prose parts as paragraphs.
    /// </summary>
    /// <param name="writer">The shared single-file Markdown writer.</param>
    /// <param name="parts">The example parts to render, each flagged as code or prose.</param>
    private static void WriteExampleParts(IMarkdownWriter writer, IEnumerable<(bool IsCode, string Content, string Language)> parts)
    {
        foreach (var (isCode, content, language) in parts)
        {
            if (isCode)
            {
                writer.WriteCodeBlock(language, content);
            }
            else
            {
                writer.WriteParagraph(content);
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
    ///     Shared throw-away accumulator passed to all <see cref="TypeLinkResolver.Linkify(TypeReference, string, string, ISet{ExternalTypeInfo}, bool)"/> calls.
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
        writer.WriteHeading(depth + 2, EscapeMarkdownHeading(BuildTypeDisplayName(type)));

        // Note the parent class for nested types so readers have context
        if (type.IsNested)
        {
            var parentTypeName = BuildTypeDisplayName(type.DeclaringType);
            writer.WriteParagraph($"Nested type of `{parentTypeName}`.");
        }

        // Emit the C# declaration signature as a fenced code block
        var typeSignature = BuildTypeSignature(type, namespaceName);
        writer.WriteSignature(CSharpLanguageId, typeSignature);

        var typeMemberId = BuildTypeId(type);

        // Fetch remarks first so the placeholder decision below can account for it —
        // only show the placeholder when BOTH summary and remarks are absent, since a
        // remarks-only type already has visible description content immediately below.
        var typeSummary = _model.XmlDocs.GetEffectiveSummaryMarkdown(typeMemberId);
        var typeRemarks = _model.XmlDocs.GetRemarks(typeMemberId);
        if (!string.IsNullOrEmpty(typeSummary))
        {
            writer.WriteParagraph(typeSummary);
        }
        else if (string.IsNullOrEmpty(typeRemarks))
        {
            writer.WriteParagraph(DotNetEmitter.NoDescriptionPlaceholder);
        }

        if (!string.IsNullOrEmpty(typeRemarks))
        {
            writer.WriteParagraph(typeRemarks);
        }

        // Emit structured example blocks for this type
        WriteExampleParts(writer, _model.XmlDocs.GetExampleBlocks(typeMemberId));

        // Document each generic type parameter the type declares (name and <typeparam> text)
        var ownTypeParameters = GetOwnGenericParameters(type);
        if (ownTypeParameters.Count > 0)
        {
            writer.WriteTable(
                TypeParameterTableHeaders,
                BuildTypeParameterRows(ownTypeParameters, _model.XmlDocs.GetTypeParameters(typeMemberId)));
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
                var memberSummary = GetMemberSummary(member, _model.XmlDocs, null, multiLine: false) ?? DotNetEmitter.NoDescriptionPlaceholder;
                var memberDisplayName = EscapeMarkdownHeading(GetMemberRowName(member));
                return $"- **{memberDisplayName}**: {memberSummary}";
            });
            writer.WriteParagraph(string.Join("\n", bulletLines));

            // H{depth+3} section for each member
            var memberContext = new SingleFileMemberContext(
                writer,
                depth,
                _model.XmlDocs,
                resolver,
                namespaceName,
                namespaceFolderPath,
                sharedExternalTypes,
                _model.Options.Visibility);
            foreach (var member in allMembers)
            {
                var memberId = BuildMemberId(member);
                WriteSingleFileMemberSection(memberContext, member, memberId);
            }
        }

        // List inherited members (display-only; no member sections are written for them)
        WriteSingleFileInheritedMembers(writer, depth, namespaceName, namespaceFolderPath, type, resolver, sharedExternalTypes);

        // Recursively emit visible nested types
        WriteSingleFileNestedTypes(writer, depth, namespaceName, namespaceFolderPath, type, resolver, sharedExternalTypes);
    }

    /// <summary>
    ///     Emits an "Inherited Members" section for <paramref name="type"/>, with one table per
    ///     base type, mirroring the gradual-disclosure output. Nothing is emitted when the type
    ///     inherits no listable members.
    /// </summary>
    /// <param name="writer">The shared single-file Markdown writer.</param>
    /// <param name="depth">Top-level heading depth from <see cref="EmitConfig.HeadingDepth"/>.</param>
    /// <param name="namespaceName">Fully qualified namespace name for name simplification.</param>
    /// <param name="namespaceFolderPath">File-system folder path for the namespace.</param>
    /// <param name="type">The derived type.</param>
    /// <param name="resolver">No-link type resolver used to render base type names.</param>
    /// <param name="sharedExternalTypes">Throw-away external-type accumulator.</param>
    private void WriteSingleFileInheritedMembers(
        IMarkdownWriter writer,
        int depth,
        string namespaceName,
        string namespaceFolderPath,
        TypeDefinition type,
        TypeLinkResolver resolver,
        SortedSet<ExternalTypeInfo> sharedExternalTypes)
    {
        var groups = _emitter.CollectInheritedMembers(type);
        if (groups.Count == 0)
        {
            return;
        }

        writer.WriteHeading(depth + 3, "Inherited Members");
        foreach (var group in groups)
        {
            var baseName = resolver.Linkify(group.DeclaringType, namespaceFolderPath, namespaceName, sharedExternalTypes);
            if (group.IsUnresolved)
            {
                writer.WriteParagraph(
                    $"Members inherited from {baseName} are not listed because its assembly could not be resolved. " +
                    "Supply the assembly with `--reference-paths` to include them.");
                continue;
            }

            writer.WriteParagraph($"**Inherited from {baseName}**");
            writer.WriteTable(
                InheritedMemberHeaders,
                group.Members.Select(m => new[] { EscapeMarkdownHeading(m.Name), m.Kind }));
        }
    }

    /// <summary>
    ///     Emits an H{depth+3} section for a single member, including the C# signature,
    ///     summary, parameter table (for methods), returns, exceptions, and example blocks.
    /// </summary>
    /// <param name="context">Shared single-file rendering context constant across all members.</param>
    /// <param name="member">The member definition to document.</param>
    /// <param name="memberId">Pre-computed XML doc member identifier for <paramref name="member"/>.</param>
    private static void WriteSingleFileMemberSection(
        SingleFileMemberContext context,
        IMemberDefinition member,
        string memberId)
    {
        var displayName = GetMemberDisplayName(member);
        context.Writer.WriteHeading(context.Depth + 3, EscapeMarkdownHeading(displayName));

        // Emit the C# declaration signature as a fenced code block
        var signature = BuildMemberSignature(member, context.NamespaceName, context.Visibility);
        context.Writer.WriteSignature(CSharpLanguageId, signature);

        // Fetch remarks first so the placeholder decision below can account for it —
        // only show the placeholder when BOTH summary and remarks are absent, since a
        // remarks-only member already has visible description content immediately below.
        // Uses the multi-line rendering because this is a member-detail-page body, not a
        // table cell — list content renders as a real Markdown list rather than inline markers.
        var summary = GetMemberSummary(member, context.XmlDocs, null, multiLine: true);
        var remarks = context.XmlDocs.GetRemarks(memberId);
        if (!string.IsNullOrEmpty(summary))
        {
            context.Writer.WriteParagraph(summary);
        }
        else if (string.IsNullOrEmpty(remarks))
        {
            context.Writer.WriteParagraph(DotNetEmitter.NoDescriptionPlaceholder);
        }

        if (!string.IsNullOrEmpty(remarks))
        {
            context.Writer.WriteParagraph(remarks);
        }

        // Document the type parameters of a generic method
        if (member is MethodDefinition { HasGenericParameters: true } genericMethod)
        {
            context.Writer.WriteTable(
                TypeParameterTableHeaders,
                BuildTypeParameterRows(genericMethod.GenericParameters, context.XmlDocs.GetTypeParameters(memberId)));
        }

        // Emit parameter table for methods with parameters
        if (member is MethodDefinition method && method.HasParameters)
        {
            var paramDocs = context.XmlDocs.GetParams(memberId);
            var paramHeaders = new[] { "Parameter", "Type", DotNetEmitter.DescriptionColumnHeader };
            var paramRows = method.Parameters.Select(p =>
            {
                var desc = paramDocs.FirstOrDefault(pd => pd.Name == p.Name).Description ?? NoDescriptionPlaceholder;
                var typeName = GetRefKindKeyword(p) + GetParamsKeyword(p) + context.Resolver.Linkify(p.ParameterType, context.NamespaceFolderPath, context.NamespaceName,
                    // Shared throw-away accumulator — generateLinks is false so it is never populated or read
                    context.SharedExternalTypes, CreateNullability(p.CustomAttributes, method));
                return new[] { WithDefault(p.Name, p), typeName, desc };
            });
            context.Writer.WriteTable(paramHeaders, paramRows);
        }

        var returns = context.XmlDocs.GetReturns(memberId);
        if (!string.IsNullOrEmpty(returns))
        {
            context.Writer.WriteParagraph($"**Returns:** {returns}");
        }

        // Emit exception table when documented exceptions exist
        var exceptions = context.XmlDocs.GetExceptionDetails(memberId);
        if (exceptions.Count > 0)
        {
            var exHeaders = new[] { "Exception", DotNetEmitter.DescriptionColumnHeader };
            var exRows = exceptions.Select(e => new[] { e.Type, e.Description ?? string.Empty });
            context.Writer.WriteTable(exHeaders, exRows);
        }

        // Emit structured example blocks for this member
        WriteExampleParts(context.Writer, context.XmlDocs.GetExampleBlocks(memberId));
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
    ///     Shared throw-away accumulator threaded through all <see cref="TypeLinkResolver.Linkify(TypeReference, string, string, ISet{ExternalTypeInfo}, bool)"/>
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
