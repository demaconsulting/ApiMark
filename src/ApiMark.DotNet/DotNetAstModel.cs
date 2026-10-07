// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using Mono.Cecil;

namespace ApiMark.DotNet;

/// <summary>
///     Bundles the per-type-page writing context that is constant across all member
///     pages generated for a single type. Used to reduce parameter counts on the
///     helper methods that emit individual member pages and table rows.
/// </summary>
/// <param name="Factory">Factory for creating per-file Markdown writers.</param>
/// <param name="NamespaceName">Full namespace name of the type being documented.</param>
/// <param name="NamespaceFolderPath">Pre-computed file-system folder path for the namespace.</param>
/// <param name="Type">Type definition whose pages are being generated.</param>
/// <param name="XmlDocs">Documentation index for member-level lookups.</param>
/// <param name="Resolver">Type link resolver for table cell generation.</param>
/// <param name="LinkContext">
///     Cross-reference resolution context for <c>&lt;see cref&gt;</c>/<c>&lt;seealso cref&gt;</c>
///     linking, scoped to this type's own page folder (<see cref="NamespaceFolderPath"/>). Member
///     pages living in a deeper folder derive their own context from this one via a
///     <see langword="with"/> expression overriding <see cref="CrefLinkContext.CurrentFolder"/>.
/// </param>
internal sealed record TypePageWriteContext(
    IMarkdownWriterFactory Factory,
    string NamespaceName,
    string NamespaceFolderPath,
    TypeDefinition Type,
    XmlDocReader XmlDocs,
    TypeLinkResolver Resolver,
    CrefLinkContext LinkContext);

/// <summary>
///     Bundles the per-method documentation writing context passed to
///     <see cref="DotNetEmitterGradualDisclosure"/> so that callers do not need to
///     thread five constant parameters through each call site.
/// </summary>
/// <param name="NamespaceName">Namespace of the type that owns the method.</param>
/// <param name="XmlDocs">Documentation index for member-level lookups.</param>
/// <param name="Resolver">Type link resolver for table cell generation.</param>
/// <param name="CurrentFolder">Folder path of the containing Markdown file, relative to the documentation output root.</param>
/// <param name="ExternalTypes">Mutable accumulator for external type references found during table cell generation.</param>
/// <param name="LinkContext">Cross-reference resolution context for <c>&lt;see cref&gt;</c>/<c>&lt;seealso cref&gt;</c> linking, scoped to <see cref="CurrentFolder"/>.</param>
internal sealed record MethodDocContext(
    string NamespaceName,
    XmlDocReader XmlDocs,
    TypeLinkResolver Resolver,
    string CurrentFolder,
    ISet<ExternalTypeInfo> ExternalTypes,
    CrefLinkContext LinkContext);

/// <summary>
///     Bundles the namespace-level documentation sourced from a NamespaceDoc carrier
///     class, carrying the summary, remarks, and structured example parts so that all
///     three surface on namespace output in the same way they do for types.
/// </summary>
/// <param name="Summary">Single-line namespace summary, or <c>null</c> when absent.</param>
/// <param name="Remarks">Namespace remarks text, or <c>null</c> when absent.</param>
/// <param name="ExampleParts">
///     Structured example parts, each flagged as code or prose; empty when no
///     <c>&lt;example&gt;</c> is present on the carrier.
/// </param>
internal sealed record NamespaceDescription(
    string? Summary,
    string? Remarks,
    IReadOnlyList<(bool IsCode, string Content)> ExampleParts);

/// <summary>
///     Bundles the per-assembly namespace documentation context that is constant
///     across all namespace page writes in a single generation run.
/// </summary>
/// <param name="AllNamespaces">All namespace names present in the assembly, ordered alphabetically.</param>
/// <param name="ByNamespace">Visible types grouped by their namespace name.</param>
/// <param name="RootNamespaces">Root namespaces identified during parse.</param>
/// <param name="NamespaceDescriptions">Optional namespace descriptions sourced from NamespaceDoc carriers.</param>
/// <param name="XmlDocs">Documentation index for namespace-level lookups.</param>
/// <param name="Resolver">Type link resolver for namespace page table cells.</param>
internal sealed record NamespaceDocContext(
    IReadOnlyList<string> AllNamespaces,
    IReadOnlyDictionary<string, IReadOnlyList<TypeDefinition>> ByNamespace,
    IReadOnlyList<string> RootNamespaces,
    IReadOnlyDictionary<string, NamespaceDescription> NamespaceDescriptions,
    XmlDocReader XmlDocs,
    TypeLinkResolver Resolver);

/// <summary>
///     Bundles all constructor arguments for <see cref="DotNetAstModel"/> so that the parsed
///     assembly data produced by <see cref="DotNetGenerator.Parse"/> can be passed as a single
///     value rather than as individual parameters.
/// </summary>
/// <param name="Assembly">Assembly definition; ownership is transferred to the model.</param>
/// <param name="AssemblyResolver">
///     Mono.Cecil assembly resolver used to resolve externally referenced assemblies during
///     inheritance analysis and lazy metadata resolution; ownership is transferred to the model
///     so it can be disposed together with <paramref name="Assembly"/> once emit completes.
/// </param>
/// <param name="XmlDocs">Pre-built XML documentation reader.</param>
/// <param name="AllNamespaces">All namespace names in alphabetical order.</param>
/// <param name="ByNamespace">Visible types grouped by namespace.</param>
/// <param name="RootNamespaces">Root namespaces identified during parse.</param>
/// <param name="NamespaceDescriptions">Namespace descriptions from NamespaceDoc carriers.</param>
/// <param name="Resolver">Type link resolver for gradual-disclosure output.</param>
/// <param name="Options">Generator configuration options.</param>
internal sealed record DotNetAstModelArgs(
    AssemblyDefinition Assembly,
    IAssemblyResolver AssemblyResolver,
    XmlDocReader XmlDocs,
    IReadOnlyList<string> AllNamespaces,
    IReadOnlyDictionary<string, IReadOnlyList<TypeDefinition>> ByNamespace,
    IReadOnlyList<string> RootNamespaces,
    IReadOnlyDictionary<string, NamespaceDescription> NamespaceDescriptions,
    TypeLinkResolver Resolver,
    DotNetGeneratorOptions Options);

/// <summary>
///     Holds all pre-parsed .NET assembly data needed during the emit phase.
/// </summary>
/// <remarks>
///     Created exclusively by <see cref="DotNetGenerator.Parse"/> and passed to
///     <see cref="DotNetEmitter"/>. All properties are read-only after construction.
/// </remarks>
internal sealed class DotNetAstModel
{
    /// <summary>
    ///     Initializes a new <see cref="DotNetAstModel"/> with all data required for emission.
    /// </summary>
    /// <param name="args">Bundled constructor arguments; see <see cref="DotNetAstModelArgs"/>.</param>
    internal DotNetAstModel(DotNetAstModelArgs args)
    {
        Assembly = args.Assembly;
        AssemblyResolver = args.AssemblyResolver;
        XmlDocs = args.XmlDocs;
        AllNamespaces = args.AllNamespaces;
        ByNamespace = args.ByNamespace;
        RootNamespaces = args.RootNamespaces;
        NamespaceDescriptions = args.NamespaceDescriptions;
        Resolver = args.Resolver;
        Options = args.Options;

        // Visibility-agnostic — needs only the assembly, so it can be built eagerly here rather
        // than deferred to a post-construction step (unlike MemberPageIndex below).
        CrefTargets = new CrefTargetResolver(args.Assembly);
    }

    /// <summary>Gets the assembly definition held open for the duration of emit.</summary>
    internal AssemblyDefinition Assembly { get; }

    /// <summary>
    ///     Gets the Mono.Cecil assembly resolver used to resolve externally referenced assemblies;
    ///     held open for the duration of emit and disposed together with <see cref="Assembly"/>.
    /// </summary>
    internal IAssemblyResolver AssemblyResolver { get; }

    /// <summary>Gets the XML documentation reader for member-level lookups.</summary>
    internal XmlDocReader XmlDocs { get; }

    /// <summary>Gets all namespace names present in the assembly, ordered alphabetically.</summary>
    internal IReadOnlyList<string> AllNamespaces { get; }

    /// <summary>Gets the visible types grouped by their namespace name.</summary>
    internal IReadOnlyDictionary<string, IReadOnlyList<TypeDefinition>> ByNamespace { get; }

    /// <summary>Gets the root namespaces identified in the assembly.</summary>
    internal IReadOnlyList<string> RootNamespaces { get; }

    /// <summary>Gets the optional namespace descriptions sourced from NamespaceDoc carriers.</summary>
    internal IReadOnlyDictionary<string, NamespaceDescription> NamespaceDescriptions { get; }

    /// <summary>Gets the type link resolver for gradual-disclosure output.</summary>
    internal TypeLinkResolver Resolver { get; }

    /// <summary>Gets the generator configuration options.</summary>
    internal DotNetGeneratorOptions Options { get; }

    /// <summary>
    ///     Gets the <c>cref</c>-target resolution index for this assembly, used to resolve a raw
    ///     <c>&lt;see cref&gt;</c>/<c>&lt;seealso cref&gt;</c> value to the type or member it
    ///     names when that symbol is declared in this assembly.
    /// </summary>
    internal CrefTargetResolver CrefTargets { get; }

    /// <summary>
    ///     Gets the global member-to-page index used by <see cref="TypeLinkResolver.LinkifyResolvedMember"/>
    ///     to resolve a member <c>cref</c> target to its gradual-disclosure page path. Empty until
    ///     <see cref="SetMemberPageIndex"/> is called.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="CrefTargets"/>, this cannot be computed eagerly in the constructor:
    ///     it depends on visibility rules that are instance methods on <see cref="DotNetEmitter"/>,
    ///     which is constructed from this model (and therefore does not yet exist while this
    ///     constructor runs). <c>DotNetGenerator.Parse</c> instead calls <see cref="SetMemberPageIndex"/>
    ///     once, immediately after constructing the <see cref="DotNetEmitter"/> that owns this model.
    /// </remarks>
    internal IReadOnlyDictionary<string, string> MemberPageIndex { get; private set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    ///     Gets the set of XML-doc type identifiers (see <see cref="DotNetEmitter.BuildTypeId"/>)
    ///     for every type that will actually be emitted as a page in gradual-disclosure mode
    ///     (top-level visible types plus all of their visible nested types, transitively). Empty
    ///     until <see cref="SetMemberPageIndex"/> is called.
    /// </summary>
    internal IReadOnlySet<string> EmittedTypeIds { get; private set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    ///     Sets <see cref="MemberPageIndex"/> and <see cref="EmittedTypeIds"/>. Called exactly
    ///     once, by <c>DotNetGenerator.Parse</c>, after the owning <see cref="DotNetEmitter"/> has
    ///     been constructed.
    /// </summary>
    /// <param name="memberPageIndex">The computed member-to-page index.</param>
    /// <param name="emittedTypeIds">The computed set of emitted type identifiers.</param>
    internal void SetMemberPageIndex(
        IReadOnlyDictionary<string, string> memberPageIndex,
        IReadOnlySet<string> emittedTypeIds)
    {
        MemberPageIndex = memberPageIndex;
        EmittedTypeIds = emittedTypeIds;
    }
}
